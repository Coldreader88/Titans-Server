using System;
using Common.Characters;
using Common.Network.Packets;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Where a player is and what they are doing: the 53 byte record the client sends in 0x00 and 0x02
    /// and the server repeats for every visible player in 0x8003.
    ///
    /// <code>
    /// int32 BE x, y, z
    /// int16 BE tilt, roll, direction
    /// uint32 BE character id
    /// uint16 BE machine id (the client sends 0xFFFF; the official server filled in its own id)
    /// uint16 BE cluster id (zone: 1 = Earth)
    /// uint32 BE vehicle unique id (0 on foot), int32 BE vehicle template id (-1 on foot)
    /// byte rank, byte account level (GM tag), byte action, byte state
    /// byte player state (bit 0: criminal), byte nationality (faction), uint16 BE equipment sum,
    /// uint16 BE update counter
    /// int32 BE team id (-1 = none)
    /// byte vehicle damage, int32 BE attack number
    /// </code>
    /// Java reference: mina_gameserver CoordUpdate.java (read) and CoordData.java (write); layout checked
    /// against 0x00, 0x02 and 0x8003 in UCGOZone-Login.pcap.
    /// </summary>
    public class CoordData
    {
        public const int Size = 53;

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public short Tilt { get; set; }
        public short Roll { get; set; }
        public short Direction { get; set; }
        public uint CharacterID { get; set; }
        public ushort MachineID { get; set; }
        public ushort ClusterID { get; set; }
        public uint VehicleUniqueID { get; set; }
        public int VehicleTemplateID { get; set; }
        public byte Rank { get; set; }
        public byte AccountLevel { get; set; }
        public byte Action { get; set; }
        public byte State { get; set; }
        /// <summary>
        /// Player state (high byte; bit 0 = criminal, which the client draws in the criminal colours) and
        /// nationality (low byte).
        /// </summary>
        public ushort Faction { get; set; }

        /// <summary>
        /// The <see cref="Faction"/> value of a character: its nationality, and the criminal bit while its
        /// criminal count is above 0. The server always sets it (the client's own value is not trusted).
        /// </summary>
        public static ushort StateAndNationality(Character c)
        {
            return (ushort)((c.IsCriminal ? 0x100 : 0) | ((byte)c.Faction));
        }
        public ushort EquipSum { get; set; }
        public ushort UpdateCounter { get; set; }
        public int TeamID { get; set; }
        public byte Damage { get; set; }
        public int AttackNumber { get; set; }

        /// <summary>
        /// The record for a character that has not sent its own yet, built from the database row.
        /// </summary>
        public static CoordData FromCharacter(Character c, byte accountLevel)
        {
            return new CoordData
            {
                X = c.X,
                Y = c.Y,
                Z = c.Z,
                Tilt = (short)c.RotY,
                Roll = (short)c.RotX,
                Direction = (short)c.Direction,
                CharacterID = c.ClientID,
                MachineID = 0,
                ClusterID = (ushort)c.Zone,
                VehicleUniqueID = 0,
                VehicleTemplateID = -1,
                Rank = (byte)c.Rank,
                AccountLevel = accountLevel,
                Faction = StateAndNationality(c),
                TeamID = c.TeamID,
                Damage = 0xFF,
            };
        }

        public static CoordData Read<T>(UCPacket<T> p)
        {
            return new CoordData
            {
                X = p.GetIntBE(),
                Y = p.GetIntBE(),
                Z = p.GetIntBE(),
                Tilt = p.GetShortBE(),
                Roll = p.GetShortBE(),
                Direction = p.GetShortBE(),
                CharacterID = p.GetUIntBE(),
                MachineID = p.GetUShortBE(),
                ClusterID = p.GetUShortBE(),
                VehicleUniqueID = p.GetUIntBE(),
                VehicleTemplateID = p.GetIntBE(),
                Rank = p.GetByte(),
                AccountLevel = p.GetByte(),
                Action = p.GetByte(),
                State = p.GetByte(),
                Faction = p.GetUShortBE(),
                EquipSum = p.GetUShortBE(),
                UpdateCounter = p.GetUShortBE(),
                TeamID = p.GetIntBE(),
                Damage = p.GetByte(),
                AttackNumber = p.GetIntBE(),
            };
        }

        public void Write<T>(UCPacket<T> p)
        {
            p.PutIntBE(X);
            p.PutIntBE(Y);
            p.PutIntBE(Z);
            p.PutShortBE(Tilt);
            p.PutShortBE(Roll);
            p.PutShortBE(Direction);
            p.PutUIntBE(CharacterID);
            p.PutUShortBE(MachineID);
            p.PutUShortBE(ClusterID);
            p.PutUIntBE(VehicleUniqueID);
            p.PutIntBE(VehicleTemplateID);
            p.PutByte(Rank);
            p.PutByte(AccountLevel);
            p.PutByte(Action);
            p.PutByte(State);
            p.PutUShortBE(Faction);
            p.PutUShortBE(EquipSum);
            p.PutUShortBE(UpdateCounter);
            p.PutIntBE(TeamID);
            p.PutByte(Damage);
            p.PutIntBE(AttackNumber);
        }

        /// <summary>
        /// True when <paramref name="other"/> is within <paramref name="radius"/> on the ground plane
        /// (Java reference: Position.isNearby).
        /// </summary>
        public bool IsNear(CoordData other, int radius)
        {
            return Math.Abs((long)X - other.X) <= radius && Math.Abs((long)Y - other.Y) <= radius;
        }
    }
}
