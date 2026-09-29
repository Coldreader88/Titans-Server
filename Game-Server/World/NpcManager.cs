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
    /// Movement: the captured NPCs on Earth and the ships stood still, mobile suits in space drifted about
    /// 45-60 units a second around their squad's area. Here space mobile suits patrol around their spawn point at
    /// that speed and Earth ones wander near theirs, pausing at each point; any mobile suit chases its target
    /// into weapon range (not beyond <see cref="LeashDistance"/> from home), circles it while firing, now and
    /// then closes in to strike with its melee weapon, and walks back home afterwards. Their new positions reach clients through 0x8003, like the
    /// official ones. Hostile NPCs also attack pilots of the other faction within
    /// NpcAggroRange (GameServer.xml) (0 = only fire back). Destroyed NPCs drop loot.
    /// The hit rules are the same as between players (<see cref="Combat"/>).
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

        /// <summary>
        /// Units per second: patrolling (official space NPCs, ~50) and chasing or going home.
        /// </summary>
        public const int PatrolSpeed = 50;
        public const int ChaseSpeed = 150;
        public const int PatrolRadius = 1500;

        /// <summary>
        /// On Earth NPCs wander this far from their spawn point, stopping a while at each point.
        /// </summary>
        public const int GroundPatrolRadius = 600;

        /// <summary>
        /// Melee: the reach in the client's distance units (a quarter of world units) when the weapon names
        /// none, and the chance an NPC with a gun and a melee weapon picks melee each time it rethinks
        /// (every <see cref="ModeSeconds"/>).
        /// </summary>
        public const int DefaultMeleeReach = 12;
        public const int MeleeChancePercent = 40;
        public const int ModeSeconds = 12;
        public const int MeleeIntervalMs = 2500;

        /// <summary>
        /// An NPC gives up the chase this far from its spawn point.
        /// </summary>
        public const int LeashDistance = 6000;
        private const int DefaultHealth = 50000;

        private static readonly NpcManager instance = new NpcManager();
        public static NpcManager Instance { get { return instance; } }

        /// <summary>
        /// Replaced, never changed, once the server runs (#spawn npc adds NPCs while the AI reads it).
        /// </summary>
        private volatile Dictionary<uint, Npc> npcs = new Dictionary<uint, Npc>();
        private readonly object addLock = new object();
        private uint nextSpawnedID = 90000000;
        private int nextSpawnedSquad = 90000000;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private Timer timer;
        private int ticking;

        public int Count { get { return npcs.Count; } }

        private long Now { get { return clock.ElapsedMilliseconds; } }

        /// <summary>
        /// Seconds until a destroyed NPC respawns (0 when it is alive or does not come back).
        /// </summary>
        public int SecondsToRespawn(Npc npc)
        {
            return npc.Alive || npc.Temporary ? 0 : (int)Math.Max(0, (npc.RespawnAt - Now + 999) / 1000);
        }

        public void Start()
        {
            Load();
            timer = new Timer(Tick, null, TickMs, TickMs);
        }

        /// <summary>
        /// Every NPC (alive or waiting to respawn).
        /// </summary>
        public ICollection<Npc> All { get { return npcs.Values; } }

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

        /// <summary>
        /// Default armaments of a spawned NPC mobile suit: the official GM and ZAKU II guns (as the NPCs in
        /// the captures carried them).
        /// </summary>
        private static readonly int[] EfArmaments = { 280003, -1, 280000, 280000 };
        private static readonly int[] ZeonArmaments = { 280006, -1, 280006, 280006 };

        /// <summary>
        /// A GM's #spawn npc: a hostile NPC of <paramref name="faction"/> at a point. It fights like the others
        /// but does not come back once destroyed.
        /// </summary>
        public Npc Spawn(int templateID, byte faction, ushort zone, int x, int y, int z, short direction, int[] armaments = null)
        {
            lock (addLock)
            {
                armaments = armaments ?? (faction == 1 ? EfArmaments : ZeonArmaments);
                var npc = Parse(new[]
                {
                    (nextSpawnedID++).ToString(), "Spawned", faction.ToString(), templateID.ToString(), (nextSpawnedSquad++).ToString(),
                    zone.ToString(), x.ToString(), y.ToString(), z.ToString(), "0", "0", direction.ToString(), "6", "48",
                    string.Join("/", armaments), "0",
                });
                npc.Temporary = true;
                var copy = new Dictionary<uint, Npc>(npcs);
                copy[npc.ID] = npc;
                npcs = copy;
                return npc;
            }
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
                    // Each server runs one side: Earth NPCs on the Earth server, Space NPCs on the Space one.
                    if (npc.Zone == TitansUC.GameServer.Configuration.Instance.Zone)
                    {
                        npcs[npc.ID] = npc;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning(string.Format("npcs.csv: cannot read \"{0}\": {1}", line, ex.Message));
                }
            }
            Logger.ShowInfo(string.Format("Loaded {0} NPCs.", npcs.Count));
            npcs = new Dictionary<uint, Npc>(npcs);
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
            npc.SpawnX = npc.X;
            npc.SpawnY = npc.Y;
            npc.SpawnZ = npc.Z;
            npc.SpawnDirection = npc.Direction;
            npc.BaseAction = npc.Action;
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
            var blade = npc.Armaments.Select(ItemTemplates.Get)
                .FirstOrDefault(t => t != null && t.IsWeapon && t.Magazine <= 0 && Loadouts.IsMelee(t.Name));
            if (blade != null && !npc.IsVendor)
            {
                npc.Melee = new ItemNode(0, 0, blade.ID) { Name = blade.Name };
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
            SquadAttack(npc, attacker.CharacterID);
        }

        /// <summary>
        /// Every member of <paramref name="npc"/>'s squad without a target turns on the player.
        /// </summary>
        private void SquadAttack(Npc npc, uint playerID)
        {
            long now = Now;
            foreach (var member in npcs.Values.Where(n => n.Squad == npc.Squad && n.Zone == npc.Zone && n.Armed))
            {
                lock (member)
                {
                    if (member.Alive && member.Target == 0)
                    {
                        member.Target = playerID;
                        member.NextShot = now + Next(ShotDelayMs, 3 * ShotDelayMs);
                        SetAction(member, Npc.ActionFighting);
                    }
                }
            }
        }

        private static void SetAction(Npc npc, byte action)
        {
            if (npc.Action != action)
            {
                npc.Action = action;
                npc.UpdateCounter++;
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
                wreck = new GroundItem(node, npc.Zone, npc.X, npc.Y, npc.Z, new byte[6], attacker.CharacterID) { IsWreck = true };
            }
            Logger.ShowInfo(string.Format("{0} destroyed NPC {1} ({2}).", attacker.Character.Name, npc.Name, npc.TemplateID));
            // Warships drop their loot on the ground first, then the wreck, as the official Magellans did. Mobile
            // suits, armours and fighters keep it in the wreck's cargo, for whoever opens it (0x26/0x27).
            var drops = Loot(npc);
            // Not every NPC leaves a wreck; without one its loot lies on the ground.
            bool leavesWreck = Combat.LeavesWreck();
            if (npc.IsMobile && leavesWreck)
            {
                var cargo = wreck.Node.Add(new ItemNode(PlayerInventory.NewUniqueID(), ItemNode.Multi, PlayerInventory.VehicleInventory)
                {
                    Name = "inventory", Modified = -1, Created = -1,
                });
                foreach (var loot in drops)
                {
                    cargo.Add(loot.Node);
                }
                drops.Clear();
            }
            foreach (var loot in drops)
            {
                var item = loot;
                GameWorld.Instance.Place(item);
                GameWorld.Instance.SendNear(item.ClusterID, item.X, item.Y, UCGameSession.BroadcastDistance,
                    () => new SM_UPDATE_ITEM_INFO(SM_UPDATE_ITEM_INFO.ItemDropped, item, npc.ID));
            }
            if (!leavesWreck)
            {
                return;
            }
            GameWorld.Instance.Place(wreck);
            GameWorld.Instance.SendNear(wreck.ClusterID, wreck.X, wreck.Y, UCGameSession.BroadcastDistance,
                () => new SM_UPDATE_ITEM_INFO(SM_UPDATE_ITEM_INFO.ItemDropped, wreck, npc.ID));
        }

        /// <summary>
        /// What a destroyed NPC drops, scattered within 200 of it (or put in its wreck); anyone can take it. Warships drop what
        /// the official Magellans dropped (fine lunatitanium alloy 510020, lunatitanium alloy, MR tool kit 280174,
        /// emergency tool kit 310013, MS junk parts); mobile suits, armours and fighters drop MS junk parts,
        /// cartridges and sometimes their gun. The official loot table is not known.
        /// </summary>
        private List<GroundItem> Loot(Npc npc)
        {
            var drops = new List<KeyValuePair<int, int>>();
            lock (random)
            {
                int range = npc.TemplateID / 10000;
                if (range == 103)
                {
                    drops.Add(new KeyValuePair<int, int>(510020, random.Next(50, 201)));
                    drops.Add(new KeyValuePair<int, int>(510003, random.Next(20, 101)));
                    drops.Add(new KeyValuePair<int, int>(510019, random.Next(2, 6)));
                    if (random.Next(2) == 0) drops.Add(new KeyValuePair<int, int>(280174, 1));
                    if (random.Next(2) == 0) drops.Add(new KeyValuePair<int, int>(310013, 1));
                }
                else if (npc.IsMobile)
                {
                    drops.Add(new KeyValuePair<int, int>(510019, random.Next(1, 4)));
                    if (random.Next(2) == 0) drops.Add(new KeyValuePair<int, int>(540000, random.Next(20, 61)));
                    if (npc.Weapon != null && random.Next(10) == 0) drops.Add(new KeyValuePair<int, int>(npc.Weapon.StaticID, 1));
                }
            }

            var result = new List<GroundItem>();
            foreach (var drop in drops)
            {
                var template = ItemTemplates.Get(drop.Key);
                if (template == null)
                {
                    continue;
                }
                int dx, dy;
                lock (random)
                {
                    dx = random.Next(-200, 201);
                    dy = random.Next(-200, 201);
                }
                var node = PlayerInventory.NewItem(template.ID, template.Stacks ? drop.Value : 1, template.Name);
                result.Add(new GroundItem(node, npc.Zone, npc.X + dx, npc.Y + dy, npc.Z, new byte[6], 0xFFFFFFFF));
            }
            return result;
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
                        if (!npc.Temporary && now >= npc.RespawnAt)
                        {
                            Respawn(npc);
                        }
                        continue;
                    }
                    lock (npc)
                    {
                        Think(npc, now, TickMs / 1000.0);
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

        /// <summary>
        /// One AI step: look for enemies, fight, move.
        /// </summary>
        private void Think(Npc npc, long now, double seconds)
        {
            if (!npc.Armed)
            {
                return;
            }
            if (npc.Target == 0 && now >= npc.NextAggroCheck)
            {
                npc.NextAggroCheck = now + 1000;
                var enemy = FindEnemy(npc);
                if (enemy != null)
                {
                    Monitor.Exit(npc);
                    try
                    {
                        SquadAttack(npc, enemy.CharacterID);
                    }
                    finally
                    {
                        Monitor.Enter(npc);
                    }
                }
            }
            if (npc.Target != 0 || npc.ShotTarget != 0)
            {
                Fight(npc, now);
            }
            if (npc.IsMobile && npc.Alive)
            {
                Move(npc, seconds);
            }
            if (npc.Target == 0 && npc.Action == Npc.ActionFighting)
            {
                SetAction(npc, npc.BaseAction);
            }
        }

        /// <summary>
        /// The nearest pilot of the other faction within the aggro range, or null.
        /// </summary>
        private static UCGameSession FindEnemy(Npc npc)
        {
            int range = TitansUC.GameServer.Configuration.Instance.NpcAggroRange;
            if (range <= 0)
            {
                return null;
            }
            UCGameSession best = null;
            double bestDistance = double.MaxValue;
            foreach (var player in GameWorld.Instance.Players)
            {
                var c = player.InGame ? player.Coord : null;
                if (c == null || c.ClusterID != npc.Zone || player.Inventory.Piloting == null ||
                    (byte)player.Character.Faction == npc.Faction)
                {
                    continue;
                }
                double d = WorldDistance(npc, c.X, c.Y, c.Z);
                if (d <= range && d < bestDistance)
                {
                    best = player;
                    bestDistance = d;
                }
            }
            return best;
        }

        private static double WorldDistance(Npc npc, int x, int y, int z)
        {
            double dx = x - (double)npc.X, dy = y - (double)npc.Y, dz = z - (double)npc.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Chase the target into weapon range; without one go home, and in space patrol around home.
        /// </summary>
        private void Move(Npc npc, double seconds)
        {
            double fromHome = WorldDistance(npc, npc.SpawnX, npc.SpawnY, npc.SpawnZ);
            var target = npc.Target != 0 ? Valid(npc, npc.Target) : null;
            if (target != null && fromHome > LeashDistance)
            {
                npc.Target = 0;
                npc.LockedOn.Clear();
                target = null;
            }

            if (target != null)
            {
                var c = target.Coord;
                double distance = WorldDistance(npc, c.X, c.Y, c.Z);
                if (npc.MeleeMode || npc.Weapon == null)
                {
                    // Close in to strike: stop just short of the target.
                    double reach = MeleeReach(npc) * 4 * 0.6;
                    if (distance > reach)
                    {
                        Step(npc, c.X, c.Y, c.Z, Math.Min(ChaseSpeed * 1.3 * seconds, distance - reach * 0.8));
                    }
                    else
                    {
                        Face(npc, c.X - (double)npc.X, c.Y - (double)npc.Y);
                    }
                }
                else
                {
                    var template = ItemTemplates.Get(npc.Weapon.StaticID);
                    double keep = Math.Max(300, Math.Min(template.Range * 4 * 0.6, 3000));
                    if (distance > keep)
                    {
                        Step(npc, c.X, c.Y, c.Z, ChaseSpeed * seconds);
                    }
                    else
                    {
                        Strafe(npc, c, seconds);
                    }
                }
                npc.HasWaypoint = false;
                return;
            }

            bool space = npc.Zone == (ushort)Common.Characters.Zone.SPACE;
            int radius = space ? PatrolRadius : GroundPatrolRadius;
            if (fromHome > radius + 100)
            {
                // Back from a chase.
                Step(npc, npc.SpawnX, npc.SpawnY, npc.SpawnZ, ChaseSpeed * seconds);
                npc.HasWaypoint = false;
                return;
            }

            // Patrol: to a random point around home, in space without stopping, on Earth with a pause at each
            // point and at the height it spawned at.
            if (Now < npc.IdleUntil)
            {
                return;
            }
            if (!npc.HasWaypoint)
            {
                lock (random)
                {
                    npc.WaypointX = npc.SpawnX + random.Next(-radius, radius);
                    npc.WaypointY = npc.SpawnY + random.Next(-radius, radius);
                    npc.WaypointZ = space ? npc.SpawnZ + random.Next(-radius / 3, radius / 3) : npc.SpawnZ;
                }
                npc.HasWaypoint = true;
            }
            if (Step(npc, npc.WaypointX, npc.WaypointY, npc.WaypointZ, PatrolSpeed * seconds))
            {
                npc.HasWaypoint = false;
                if (!space)
                {
                    npc.IdleUntil = Now + Next(3000, 10000);
                }
            }
        }

        /// <summary>
        /// Circles the target at its distance while firing, turning the other way now and then.
        /// </summary>
        private void Strafe(Npc npc, CoordData c, double seconds)
        {
            long now = Now;
            if (now >= npc.NextStrafeChange)
            {
                npc.StrafeSign = Next(0, 2) == 0 ? -1 : 1;
                npc.NextStrafeChange = now + Next(3000, 7000);
            }
            double dx = c.X - (double)npc.X, dy = c.Y - (double)npc.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= 1)
            {
                double step = PatrolSpeed * 1.5 * seconds;
                npc.X += (int)Math.Round(-dy / d * step * npc.StrafeSign);
                npc.Y += (int)Math.Round(dx / d * step * npc.StrafeSign);
            }
            Face(npc, c.X - (double)npc.X, c.Y - (double)npc.Y);
        }

        /// <summary>
        /// How close (in the client's distance units) it must be to strike with its melee weapon.
        /// </summary>
        private static int MeleeReach(Npc npc)
        {
            var template = npc.Melee != null ? ItemTemplates.Get(npc.Melee.StaticID) : null;
            return template != null && template.Range > 0 ? template.Range : DefaultMeleeReach;
        }

        /// <summary>
        /// Chooses between closing in with the melee weapon and firing from a distance.
        /// </summary>
        private void ChooseMode(Npc npc, long now)
        {
            if (now < npc.NextModeChange)
            {
                return;
            }
            npc.NextModeChange = now + ModeSeconds * 1000L;
            npc.MeleeMode = npc.Melee != null && (npc.Weapon == null || Next(0, 100) < MeleeChancePercent);
        }

        /// <summary>
        /// Moves up to <paramref name="step"/> toward a point, facing it; true once there.
        /// </summary>
        private static bool Step(Npc npc, int x, int y, int z, double step)
        {
            double dx = x - (double)npc.X, dy = y - (double)npc.Y, dz = z - (double)npc.Z;
            double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (d <= step || d < 1)
            {
                npc.X = x;
                npc.Y = y;
                npc.Z = z;
                return true;
            }
            Face(npc, dx, dy);
            npc.X += (int)Math.Round(dx / d * step);
            npc.Y += (int)Math.Round(dy / d * step);
            npc.Z += (int)Math.Round(dz / d * step);
            return false;
        }

        /// <summary>
        /// Turns toward a direction. In the captures the heading of a move was the direction value + 90 degrees
        /// (32768 = 180 degrees).
        /// </summary>
        private static void Face(Npc npc, double dx, double dy)
        {
            if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
            {
                return;
            }
            double degrees = Math.Atan2(dy, dx) * 180 / Math.PI - 90;
            while (degrees < -180) degrees += 360;
            while (degrees >= 180) degrees -= 360;
            npc.Direction = (short)Math.Round(degrees * 32768 / 180);
        }

        private void Respawn(Npc npc)
        {
            lock (npc)
            {
                npc.X = npc.SpawnX;
                npc.Y = npc.SpawnY;
                npc.Z = npc.SpawnZ;
                npc.Direction = npc.SpawnDirection;
                npc.Action = npc.BaseAction;
                npc.HasWaypoint = false;
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
            ChooseMode(npc, now);
            int distance = Distance(npc, c);
            if (npc.Melee != null && distance <= MeleeReach(npc))
            {
                // Within reach: strike at once (the official melee had no fire effect, only the lock on and
                // the result, hand_to_hand_0.1.pcap).
                LockOn(npc, t);
                Hit(npc, t, npc.Melee);
                npc.NextShot = now + MeleeIntervalMs;
                return;
            }
            if (npc.Weapon == null)
            {
                npc.NextShot = now + 500;
                return;
            }
            var template = ItemTemplates.Get(npc.Weapon.StaticID);
            if (distance > template.Range)
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
            LockOn(npc, t);
            npc.ShotTarget = t.CharacterID;
            npc.ShotDue = now + ShotDelayMs;
            npc.NextShot = now + Next(MinShotIntervalMs, MaxShotIntervalMs);
        }

        private static void LockOn(Npc npc, UCGameSession t)
        {
            if (npc.LockedOn.Add(t.CharacterID))
            {
                var lockOn = new byte[9];
                Bytes.PutU32(lockOn, 4, npc.ID);
                lockOn[8] = 1;
                t.Network.SendPacket(new SM_RAW((uint)GSOpcode.SM_LOCK_ON, lockOn));
            }
        }

        private void Hit(Npc npc, UCGameSession target)
        {
            Hit(npc, target, npc.Weapon);
        }

        private void Hit(Npc npc, UCGameSession target, ItemNode weapon)
        {
            // NPC weapons never run dry or wear out.
            weapon.Loaded = 1 << 20;
            var r = target.TakeHit(weapon, Distance(npc, target.Coord));
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
            target.ApplyHit(r, npc.ID);
            if (r.Destroyed)
            {
                target.AddScore(npc.Faction == (byte)target.Character.Faction ? ScoreSlot.DeathsByFriendlyNpc : ScoreSlot.DeathsByEnemyNpc);
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
