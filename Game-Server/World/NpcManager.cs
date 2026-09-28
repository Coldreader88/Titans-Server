using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Common.Characters;
using Common.Network.Packets;
using SmartEngine.Core;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.Network.Packets.Client;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Loads the NPCs (DB/Npcs/npcs.csv) and runs them: squads fire back at players who attack them, destroyed
    /// NPCs leave a wreck and come back after <see cref="RespawnSeconds"/>.
    ///
    /// What the official server did (see <see cref="Npc"/> for the captures):
    /// <list type="bullet">
    /// <item>An NPC fires with a fire effect (0x803B: 2, FF x 16, weapon template, the target's position) to
    /// everyone near, locks on (0x8010: 0, NPC id, 1) the first time, and about a second later sends the result:
    /// 0x800F to the target (weapon 0, 0, template; durability and rounds used 0) and 0x8036 to everyone near the
    /// target. One shot about every 8 seconds.</item>
    /// <item>NPCs only fired after being attacked, and the whole squad answered.</item>
    /// <item>A destroyed NPC leaves a wreck (0x8035 action 1: owner FFFFFFFF, actor the NPC, health 0), is left out
    /// of the position lists and comes back at its spawn point with the same id and the update counter raised
    /// by 2 (within 354 seconds in the captures).</item>
    /// </list>
    /// The captured NPCs stood still (all on Earth, and ships in space; mobile suits in space drifted slowly), so
    /// these do not move. The hit rules are the same as between players (<see cref="Combat"/>).
    /// </summary>
    public class NpcManager
    {
        public const int RespawnSeconds = 300;
        public const int ShotDelayMs = 1000;
        public const int MinShotIntervalMs = 6000;
        public const int MaxShotIntervalMs = 9000;

        /// <summary>
        /// The squad gives up on a target farther away than this (both axes, like the view square).
        /// </summary>
        public const int ChaseDistance = 8000;

        private const int TickMs = 250;
        private const int DefaultHealth = 50000;

        private static readonly NpcManager instance = new NpcManager();
        public static NpcManager Instance { get { return instance; } }

        private readonly Dictionary<uint, Npc> npcs = new Dictionary<uint, Npc>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private Timer timer;
        private int ticking;

        public int Count { get { return npcs.Count; } }

        private long Now { get { return clock.ElapsedMilliseconds; } }

        public void Start()
        {
            Load();
            timer = new Timer(Tick, null, TickMs, TickMs);
        }

        public Npc Get(uint id)
        {
            Npc npc;
            return npcs.TryGetValue(id, out npc) ? npc : null;
        }

        /// <summary>
        /// Position records of the live NPCs within <paramref name="radius"/> of a point (for 0x8003).
        /// </summary>
        public List<CoordData> Visible(ushort zone, int x, int y, int radius)
        {
            var result = new List<CoordData>();
            foreach (var npc in npcs.Values)
            {
                lock (npc)
                {
                    if (npc.Alive && npc.Zone == zone && Math.Abs((long)npc.X - x) <= radius && Math.Abs((long)npc.Y - y) <= radius)
                    {
                        result.Add(npc.ToCoord());
                    }
                }
            }
            return result;
        }

        private void Load()
        {
            string path;
            try
            {
                path = CharacterData.FindFile("Npcs", "npcs.csv");
            }
            catch (FileNotFoundException ex)
            {
                Logger.ShowWarning(ex.Message + " No NPCs.");
                return;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("id,"))
                {
                    continue;
                }
                try
                {
                    var npc = Parse(line.Split(','));
                    npcs[npc.ID] = npc;
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning(string.Format("npcs.csv: cannot read \"{0}\": {1}", line, ex.Message));
                }
            }
            Logger.ShowInfo(string.Format("Loaded {0} NPCs.", npcs.Count));
        }

        private static Npc Parse(string[] f)
        {
            var npc = new Npc
            {
                ID = Npc.IDBase + uint.Parse(f[0]),
                Name = f[1],
                Faction = byte.Parse(f[2]),
                TemplateID = int.Parse(f[3]),
                Squad = int.Parse(f[4]),
                Zone = ushort.Parse(f[5]),
                X = int.Parse(f[6]),
                Y = int.Parse(f[7]),
                Z = int.Parse(f[8]),
                Tilt = short.Parse(f[9]),
                Roll = short.Parse(f[10]),
                Direction = short.Parse(f[11]),
                Rank = byte.Parse(f[12]),
                Action = byte.Parse(f[13]),
                Armaments = f[14].Split('/').Select(int.Parse).Concat(Enumerable.Repeat(-1, 4)).Take(4).ToArray(),
                MaxHealth = int.Parse(f[15]),
                Alive = true,
            };
            if (npc.MaxHealth <= 0)
            {
                var template = VehicleTemplates.Get(npc.TemplateID);
                npc.MaxHealth = template != null && template.Health > 0 ? template.Health : DefaultHealth;
            }
            npc.Vehicle = new ItemNode(PlayerInventory.NewUniqueID(), ItemNode.Multi, npc.TemplateID)
            {
                Name = npc.Name,
                Health = npc.MaxHealth,
                MaxHealth = npc.MaxHealth,
            };

            // Fires its longest-range gun (melee weapons have no magazine).
            var gun = npc.Armaments.Select(ItemTemplates.Get)
                .Where(t => t != null && t.IsWeapon && t.Magazine > 0 && t.Range > 0)
                .OrderByDescending(t => t.Range).FirstOrDefault();
            if (gun != null && !npc.IsVendor)
            {
                npc.Weapon = new ItemNode(0, 0, gun.ID) { Name = gun.Name };
            }
            return npc;
        }

        /// <summary>
        /// A player's attack on an NPC (0x0F, 0x67); null when it cannot be attacked (a vendor, or dead).
        /// The caller sends the results, then calls <see cref="AfterAttack"/>.
        /// </summary>
        public HitResult Attack(Npc npc, ItemNode weapon, int distance)
        {
            if (npc.IsVendor)
            {
                return null;
            }
            lock (npc)
            {
                if (!npc.Alive)
                {
                    return null;
                }
                var r = Combat.Attack(weapon, npc.Vehicle, null, distance);
                if (r.Result != Combat.ResultMiss)
                {
                    npc.AttackNumber = (int)r.AttackNumber;
                    npc.Damage = r.Percent;
                }
                return r;
            }
        }

        /// <summary>
        /// After the results of an attack on <paramref name="npc"/> went out: the squad turns on the attacker,
        /// and a destroyed NPC leaves its wreck.
        /// </summary>
        public void AfterAttack(Npc npc, HitResult r, UCGameSession attacker)
        {
            if (r.Destroyed)
            {
                Destroy(npc, attacker);
                return;
            }
            long now = Now;
            foreach (var member in npcs.Values.Where(n => n.Squad == npc.Squad && n.Zone == npc.Zone && n.Weapon != null))
            {
                lock (member)
                {
                    if (member.Alive && member.Target == 0)
                    {
                        member.Target = attacker.CharacterID;
                        member.NextShot = now + Next(ShotDelayMs, 3 * ShotDelayMs);
                    }
                }
            }
        }

        private void Destroy(Npc npc, UCGameSession attacker)
        {
            GroundItem wreck;
            lock (npc)
            {
                if (!npc.Alive)
                {
                    return;
                }
                npc.Alive = false;
                npc.Target = 0;
                npc.ShotTarget = 0;
                npc.LockedOn.Clear();
                npc.UpdateCounter++;
                npc.RespawnAt = Now + RespawnSeconds * 1000L;
                var node = new ItemNode(PlayerInventory.NewUniqueID(), ItemNode.Multi, npc.TemplateID)
                {
                    Name = npc.Name,
                    Health = 0,
                    MaxHealth = npc.MaxHealth,
                };
                wreck = new GroundItem(node, npc.Zone, npc.X, npc.Y, npc.Z, new byte[6], 0xFFFFFFFF) { IsWreck = true };
            }
            Logger.ShowInfo(string.Format("{0} destroyed NPC {1} ({2}).", attacker.Character.Name, npc.Name, npc.TemplateID));
            GameWorld.Instance.Place(wreck);
            GameWorld.Instance.SendNear(wreck.ClusterID, wreck.X, wreck.Y, UCGameSession.BroadcastDistance,
                () => new SM_UPDATE_ITEM_INFO(SM_UPDATE_ITEM_INFO.ItemDropped, wreck, npc.ID));
        }

        private void Tick(object state)
        {
            if (Interlocked.Exchange(ref ticking, 1) == 1)
            {
                return;
            }
            try
            {
                long now = Now;
                foreach (var npc in npcs.Values)
                {
                    if (!npc.Alive)
                    {
                        if (now >= npc.RespawnAt)
                        {
                            Respawn(npc);
                        }
                        continue;
                    }
                    if (npc.Weapon != null && (npc.Target != 0 || npc.ShotTarget != 0))
                    {
                        lock (npc)
                        {
                            Fight(npc, now);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
            finally
            {
                Interlocked.Exchange(ref ticking, 0);
            }
        }

        private void Respawn(Npc npc)
        {
            lock (npc)
            {
                npc.Alive = true;
                npc.Damage = 0;
                npc.UpdateCounter++;
                PlayerInventory.SetHealth(npc.Vehicle, npc.MaxHealth);
            }
        }

        /// <summary>
        /// The target if it can still be fought: in the game, in a vehicle, in the same zone and near.
        /// </summary>
        private static UCGameSession Valid(Npc npc, uint targetID)
        {
            var target = targetID != 0 ? GameWorld.Instance.Get(targetID) : null;
            var coord = target != null && target.InGame ? target.Coord : null;
            if (coord == null || target.Inventory.Piloting == null || coord.ClusterID != npc.Zone ||
                Math.Abs((long)coord.X - npc.X) > ChaseDistance || Math.Abs((long)coord.Y - npc.Y) > ChaseDistance)
            {
                return null;
            }
            return target;
        }

        /// <summary>
        /// The distance as the client reports it in 0x0F (about a quarter of the world distance).
        /// </summary>
        private static int Distance(Npc npc, CoordData c)
        {
            double dx = c.X - (double)npc.X, dy = c.Y - (double)npc.Y, dz = c.Z - (double)npc.Z;
            return (int)(Math.Sqrt(dx * dx + dy * dy + dz * dz) / 4);
        }

        private void Fight(Npc npc, long now)
        {
            // A shot on its way: send its result.
            if (npc.ShotTarget != 0 && now >= npc.ShotDue)
            {
                uint id = npc.ShotTarget;
                npc.ShotTarget = 0;
                var target = Valid(npc, id);
                if (target != null)
                {
                    Hit(npc, target);
                }
            }

            if (npc.Target == 0 || npc.ShotTarget != 0 || now < npc.NextShot)
            {
                return;
            }
            var t = Valid(npc, npc.Target);
            if (t == null)
            {
                lock (npc)
                {
                    npc.Target = 0;
                    npc.LockedOn.Clear();
                }
                return;
            }
            var c = t.Coord;
            var template = ItemTemplates.Get(npc.Weapon.StaticID);
            if (Distance(npc, c) > template.Range)
            {
                npc.NextShot = now + 1000;
                return;
            }

            // Fire: the effect to everyone near, the lock on to the target the first time.
            var effect = new byte[36];
            effect[3] = 2;
            for (int i = 4; i < 20; i++)
            {
                effect[i] = 0xFF;
            }
            Bytes.PutU32(effect, 20, (uint)npc.Weapon.StaticID);
            Bytes.PutU32(effect, 24, (uint)c.X);
            Bytes.PutU32(effect, 28, (uint)c.Y);
            Bytes.PutU32(effect, 32, (uint)c.Z);
            GameWorld.Instance.SendNear(npc.Zone, c.X, c.Y, UCGameSession.BroadcastDistance,
                () => new SM_RAW((uint)GSOpcode.SM_BROADCAST, effect));
            if (npc.LockedOn.Add(t.CharacterID))
            {
                var lockOn = new byte[9];
                Bytes.PutU32(lockOn, 4, npc.ID);
                lockOn[8] = 1;
                t.Network.SendPacket(new SM_RAW((uint)GSOpcode.SM_LOCK_ON, lockOn));
            }
            npc.ShotTarget = t.CharacterID;
            npc.ShotDue = now + ShotDelayMs;
            npc.NextShot = now + Next(MinShotIntervalMs, MaxShotIntervalMs);
        }

        private void Hit(Npc npc, UCGameSession target)
        {
            // NPC guns never run dry or wear out.
            npc.Weapon.Loaded = 1 << 20;
            var r = target.TakeHit(npc.Weapon, Distance(npc, target.Coord));
            if (r == null)
            {
                return;
            }
            r.DurabilityUsed = 0;
            r.RoundsUsed = 0;
            target.Network.SendPacket(new SM_ATTACK_RESULT(npc.ID, target.CharacterID, r));
            var c = target.Coord;
            GameWorld.Instance.SendNear(c.ClusterID, c.X, c.Y, UCGameSession.BroadcastDistance,
                () => new SM_ATTACK_RESULT_NEAR(npc.ID, target.CharacterID, r));
            target.ApplyHit(r);
            if (r.Destroyed)
            {
                Logger.ShowInfo(string.Format("NPC {0} destroyed {1}'s {2}.", npc.Name, target.Character.Name, r.DamagedItem.Name));
                lock (npc)
                {
                    npc.Target = 0;
                    npc.LockedOn.Remove(target.CharacterID);
                }
            }
        }

        private int Next(int min, int max)
        {
            lock (random)
            {
                return random.Next(min, max);
            }
        }
    }
}
