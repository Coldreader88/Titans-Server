using System;
using System.Collections.Generic;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// A computer-controlled mobile suit, ship or vendor truck (DB/Npcs/npcs.csv).
    ///
    /// The official server showed NPCs as ordinary records in the position list (0x8003), after the players:
    /// id 1000000000 + n, vehicle unique id 0, a vehicle template, account level 0x0F (the NPC tag), the squad id
    /// as team id, and the damage % and last attack number it keeps itself. The client asks their name (0x06,
    /// account FFFFFFFF) and looks (0x0A, 4 armament slots) like a player's. Layouts from the official captures
    /// (Weapon_Manipulation_0.1___Ambac_0.1_(MS06RP).pcap, space_engagement_0.1.pcap,
    /// megellan_space_destroyed_by_buttercup.pcap and others).
    /// </summary>
    public class Npc
    {
        public const uint IDBase = 1000000000;
        public const byte NpcTag = 0x0F;

        /// <summary>
        /// The action official NPC mobile suits briefly showed (0x20) instead of their usual 0x30 or 0.
        /// </summary>
        public const byte ActionFighting = 0x20;

        /// <summary>
        /// Any value works; the official server used 0x17A2 and similar ones.
        /// </summary>
        public const ushort MachineID = 0x17A2;

        public static bool IsNpcID(uint id)
        {
            return id >= IDBase && id < IDBase + 100000000;
        }

        public uint ID { get; set; }
        public string Name { get; set; }
        public byte Faction { get; set; }
        public int TemplateID { get; set; }
        public int Squad { get; set; }
        public ushort Zone { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public short Tilt { get; set; }
        public short Roll { get; set; }
        public short Direction { get; set; }
        public byte Rank { get; set; }
        public byte Action { get; set; }

        /// <summary>
        /// Templates of the 4 armament slots, -1 for an empty one.
        /// </summary>
        public int[] Armaments { get; set; }

        public int MaxHealth { get; set; }

        /// <summary>
        /// The quest item this NPC always drops (a combat quest's squad leader or spy, DB/Npcs/quest_squads.csv), 0 for none.
        /// </summary>
        public int QuestItem { get; set; }

        /// <summary>
        /// Vendor trucks and PUZOCKs (1000xxx) never fight and cannot be hurt.
        /// </summary>
        public bool IsVendor { get { return TemplateID / 10000 == 100; } }

        /// <summary>
        /// The vehicle attacks are resolved against (unique id kept for the damaged item of 0x800F).
        /// </summary>
        public ItemNode Vehicle { get; set; }

        /// <summary>
        /// The weapon it fires (unique id and format 0, as the official 0x800F of an NPC showed it), or null
        /// when it has no ranged weapon.
        /// </summary>
        public ItemNode Weapon { get; set; }

        /// <summary>
        /// Its melee weapon (heat hawk, beam saber...), or null. It takes it up when its target comes close.
        /// </summary>
        public ItemNode Melee { get; set; }

        /// <summary>
        /// Whether it holds <see cref="Melee"/> (closing in to strike) rather than its gun: chosen by the distance
        /// to its target (NpcManager.ChooseWeapon).
        /// </summary>
        public bool MeleeMode { get; set; }

        /// <summary>
        /// The counter at the end of its looks (0x800A), raised whenever it changes weapons (6 as in the captures).
        /// </summary>
        public ushort LooksCounter { get; set; } = 6;

        /// <summary>
        /// When a player last hit it or its squad: it keeps after an attacker who shoots from afar.
        /// </summary>
        public long LastAttacked { get; set; }

        /// <summary>
        /// <see cref="Armaments"/> as its looks show them: the weapon it holds (melee or gun) in slot 0, the main
        /// hand (0x1B), swapped with whatever was there.
        /// </summary>
        public int[] HeldArmaments
        {
            get
            {
                var held = MeleeMode ? Melee : Weapon;
                int slot = held != null ? Array.IndexOf(Armaments, held.StaticID) : -1;
                if (slot <= 0)
                {
                    return Armaments;
                }
                var result = (int[])Armaments.Clone();
                result[slot] = result[0];
                result[0] = held.StaticID;
                return result;
            }
        }

        /// <summary>
        /// Which way it circles its target while firing (1 or -1), and when it turns the other way.
        /// </summary>
        public int StrafeSign { get; set; } = 1;
        public long NextStrafeChange { get; set; }

        /// <summary>
        /// A patrolling NPC waits at each point it reaches until then.
        /// </summary>
        public long IdleUntil { get; set; }

        /// <summary>
        /// Whether it can fight at all.
        /// </summary>
        public bool Armed { get { return !IsVendor && (Weapon != null || Melee != null); } }

        /// <summary>
        /// Where it spawns and walks back to, and the action it shows while not fighting.
        /// </summary>
        public int SpawnX { get; set; }
        public int SpawnY { get; set; }
        public int SpawnZ { get; set; }
        public short SpawnDirection { get; set; }
        public byte BaseAction { get; set; }

        /// <summary>
        /// Mobile suits, mobile armours and fighters move (chase, go home, patrol in space); ships, trucks and
        /// PUZOCKs stay where they are, as in the captures.
        /// </summary>
        public bool IsMobile
        {
            get
            {
                int range = TemplateID / 10000;
                return !IsVendor && (range == 41 || range == 42 || range == 43);
            }
        }

        /// <summary>
        /// Current patrol point (space only), and when it next looks around for enemies.
        /// </summary>
        public bool HasWaypoint { get; set; }
        public int WaypointX { get; set; }
        public int WaypointY { get; set; }
        public int WaypointZ { get; set; }
        public long NextAggroCheck { get; set; }

        public bool Alive { get; set; }

        /// <summary>
        /// Spawned by a GM: not in npcs.csv, and gone for good once destroyed.
        /// </summary>
        public bool Temporary { get; set; }

        /// <summary>
        /// The account attribute (ACCOUNTATTRIBUTE type id) its 0x8003 record carries, <see cref="NpcTag"/> unless a GM's
        /// #spawn attributes gave it another.
        /// </summary>
        public byte AccountAttribute { get; set; } = NpcTag;

        /// <summary>
        /// Holds its spot (#spawn attributes): fights from where it stands, never chases or patrols.
        /// </summary>
        public bool Stationary { get; set; }
        public long RespawnAt { get; set; }
        public ushort UpdateCounter { get; set; }
        public int AttackNumber { get; set; }
        public byte Damage { get; set; }

        /// <summary>
        /// The player the squad is fighting (0 for none): NPCs only fire back, as in the captures.
        /// </summary>
        public uint Target { get; set; }
        public long NextShot { get; set; }

        /// <summary>
        /// A shot fired (fire effect sent) whose result is due at <see cref="ShotDue"/>, about a second later.
        /// </summary>
        public uint ShotTarget { get; set; }
        public long ShotDue { get; set; }

        /// <summary>
        /// Players it has already sent its lock on (0x8010).
        /// </summary>
        public HashSet<uint> LockedOn { get; } = new HashSet<uint>();

        public CoordData ToCoord()
        {
            return new CoordData
            {
                X = X,
                Y = Y,
                Z = Z,
                Tilt = Tilt,
                Roll = Roll,
                Direction = Direction,
                CharacterID = ID,
                MachineID = MachineID,
                ClusterID = Zone,
                VehicleUniqueID = 0,
                VehicleTemplateID = TemplateID,
                Rank = Rank,
                AccountLevel = AccountAttribute,
                Action = Action,
                State = 0,
                Faction = Faction,
                EquipSum = 0,
                UpdateCounter = UpdateCounter,
                TeamID = Squad,
                Damage = Damage,
                AttackNumber = AttackNumber,
            };
        }
    }
}
