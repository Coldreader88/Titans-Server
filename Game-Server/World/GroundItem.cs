using Common.Network.Packets;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// An item or vehicle lying in the world, as the ground item records of 0x8005 and 0x8035 describe it.
    ///
    /// <code>
    /// uint32 BE   unique id, format, static id
    /// int32 BE    x, y, z
    /// 6 bytes    rotation (as the client sent it in 0x23)
    /// uint32 BE   0, amount
    /// uint32 BE   owner character id
    /// UC size    0
    /// uint32 BE   placed (Unix time), health, max health (0, 0 for an item), expires (Unix time; 0 once taken)
    /// uint16 BE   counter, raised by every ground item event
    /// UC size    0
    /// </code>
    /// Layout from the official captures (BATTLE_1.pcap, Alumina (11).pcap, Alaron_Zsaeo_VIMP_Flag2.pcap). The
    /// official server let items lie for two hours (expires = placed + 7200).
    /// </summary>
    public class GroundItem
    {
        public const int Lifetime = 2 * 60 * 60;

        /// <summary>
        /// The lists the client asks for with 0x05 (and the last byte of 0x8035): 0 for money and 24xxxx
        /// items, 2 for vehicles, 1 for everything else (as sorted in the official 0x8035s).
        /// </summary>
        public const byte ListMisc = 0;
        public const byte ListItems = 1;
        public const byte ListVehicles = 2;

        public GroundItem(ItemNode node, ushort clusterID, int x, int y, int z, byte[] rotation, uint ownerID)
        {
            Node = node;
            ClusterID = clusterID;
            X = x;
            Y = y;
            Z = z;
            Rotation = rotation;
            OwnerID = ownerID;
            Placed = GameWorld.UnixTime();
        }

        public ItemNode Node { get; private set; }
        public uint UniqueID { get { return Node.UniqueID; } }
        public ushort ClusterID { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        public byte[] Rotation { get; private set; }
        public uint OwnerID { get; private set; }
        public int Placed { get; private set; }
        public ushort Counter { get; set; }

        public bool IsVehicle { get { return Node.Format == ItemNode.Multi; } }

        /// <summary>
        /// Items disappear after <see cref="Lifetime"/>. Vehicles stay until their owner takes them (or logs
        /// in again, which puts them back in the hangar), so their expiry keeps moving forward.
        /// </summary>
        public int Expires
        {
            get { return IsVehicle ? GameWorld.UnixTime() + Lifetime : Placed + Lifetime; }
        }

        public byte List
        {
            get
            {
                if (IsVehicle)
                {
                    return ListVehicles;
                }
                int range = Node.StaticID / 10000;
                return range == 24 || range == 50 ? ListMisc : ListItems;
            }
        }

        public void WriteRecord<T>(UCPacket<T> p, bool taken)
        {
            p.PutUIntBE(Node.UniqueID);
            p.PutIntBE(Node.Format);
            p.PutIntBE(Node.StaticID);
            p.PutIntBE(X);
            p.PutIntBE(Y);
            p.PutIntBE(Z);
            p.PutBytes(Rotation);
            p.PutIntBE(0);
            p.PutIntBE(IsVehicle ? 1 : Node.Amount);
            p.PutUIntBE(OwnerID);
            p.PutSize(0);
            p.PutIntBE(Placed);
            p.PutIntBE(IsVehicle ? Node.Health : 0);
            p.PutIntBE(IsVehicle ? Node.MaxHealth : 0);
            p.PutIntBE(taken ? 0 : Expires);
            p.PutUShortBE(Counter);
            p.PutSize(0);
        }
    }
}
