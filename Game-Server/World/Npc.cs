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

        public bool Alive { get; set; }
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
                AccountLevel = NpcTag,
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
