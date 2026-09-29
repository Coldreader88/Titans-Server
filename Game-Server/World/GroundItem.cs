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
    /// UC size    common values: none, or for a battle town's laser communication tower one uint32 BE, the city
    ///            id in the top 16 bits and the tower's number below (the client reads the city from it, 0x4360d5)
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
        public int Placed { get; set; }
        public ushort Counter { get; set; }

        public bool IsVehicle { get { return Node.Format == ItemNode.Multi; } }

        /// <summary>
        /// The wreck of a vehicle destroyed in combat: nobody can board it, it is lost to its owner and it lies
        /// for <see cref="Combat.WreckLifetime"/> seconds.
        /// </summary>
        public bool IsWreck { get; set; }

        /// <summary>
        /// For a battle town's laser communication tower (see <see cref="Occupation"/>): the city it guards; 0 for
        /// everything else. Towers belong to nobody, are not saved with the ground and cannot be taken.
        /// </summary>
        public int CityID { get; set; }

        /// <summary>
        /// The tower's number in its city (0 to 2).
        /// </summary>
        public int TowerIndex { get; set; }

        public bool IsTower { get { return CityID != 0; } }

        /// <summary>
        /// Gives it to another player (0x25), or to nobody (FFFFFFFF).
        /// </summary>
        public void ChangeOwner(uint ownerID)
        {
            OwnerID = ownerID;
        }

        /// <summary>
        /// A vehicle destroyed where it stood: from now on a wreck of whoever destroyed it, lying for its own
        /// ten minutes.
        /// </summary>
        public void BecomeWreck(uint killerID)
        {
            IsWreck = true;
            OwnerID = killerID;
            Placed = GameWorld.UnixTime();
        }

        /// <summary>
        /// Items disappear after <see cref="Lifetime"/>, wrecks after <see cref="Combat.WreckLifetime"/>.
        /// Vehicles stay until their owner takes them (or logs in again, which puts them back in the hangar), so
        /// their expiry keeps moving forward.
        /// </summary>
        public int Expires
        {
            get
            {
                if (IsWreck)
                {
                    return Placed + Combat.WreckLifetime;
                }
                return IsVehicle ? GameWorld.UnixTime() + Lifetime : Placed + Lifetime;
            }
        }

        /// <summary>
        /// Whether it can still lie on the ground: vehicles (not wrecks) never expire.
        /// </summary>
        public bool CanExpire { get { return !IsVehicle || IsWreck; } }

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
            if (IsTower)
            {
                p.PutSize(1);
                p.PutUIntBE((uint)(CityID << 16 | TowerIndex));
            }
            else
            {
                p.PutSize(0);
            }
        }
    }
}
