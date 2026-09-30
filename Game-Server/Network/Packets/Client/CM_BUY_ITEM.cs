using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x21: buys from a shop.
    ///
    /// <code>
    /// uint16 BE   service: 1 = shop (items), 2 = shop (seen once, same layout), 3 = a shuttle,
    ///             4 = a car; vehicles bought into weared are ridden away at once
    /// uint16 BE   0
    /// uint32 BE   character id
    /// 8 bytes    0
    /// uint32 BE   destination container unique id, format
    /// uint32 BE   0
    /// uint32 BE   amount
    /// uint32 BE   item static id
    /// uint32 BE   FFFFFFFF, 1
    /// uint32 BE   town (TOWNINFOTEMPLATE; FFFFFFFF for the shuttles and cars of services 3 and 4)
    /// uint32 BE   shop (SHOPINFOTEMPLATE; FFFFFFFF or 0 for services 3 and 4)
    /// 13 bytes   (FF.., 0)
    /// byte       0x81, then 0 and int32 BE x, y, z of the player (vehicles), FFFF
    /// byte       1 or 2, byte shop kind, 3 bytes 0
    /// </code>
    /// Java reference: RequestBuyItem.java / BuyItem.java; layout from the official captures (75mm_MG.pcap,
    /// Alumina (410).pcap, BATTLE_1.pcap, Earth_To_Space.pcap).
    /// </summary>
    public class CM_BUY_ITEM : UCPacket<GSOpcode>
    {
        public const int ServiceItems = 1;
        public const int ServiceItems2 = 2;
        public const int ServiceShuttle = 3;
        public const int ServiceCar = 4;

        public CM_BUY_ITEM()
        {
            this.ID = GSOpcode.CM_BUY_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_BUY_ITEM();
        }

        public int Service { get; private set; }
        public uint CharacterID { get; private set; }
        public uint DestUniqueID { get; private set; }
        public int DestFormat { get; private set; }
        public int Amount { get; private set; }
        public int StaticID { get; private set; }

        /// <summary>
        /// The town and shop the player buys at (51 and 7 for the Zeon weapon shop of ZSSAEO 3 in 75mm_MG.pcap).
        /// </summary>
        public int Town { get; private set; }
        public int Shop { get; private set; }

        /// <summary>
        /// Bytes 52-59 and the position after the 0x81 (bytes 66-77): for a shuttle, what the player info
        /// carries on arrival (see Common.Characters.Transport).
        /// </summary>
        public Common.Characters.Transport Transport { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 36)
            {
                return;
            }
            var body = this.GetBytes((ushort)this.Remaining);
            Service = Bytes.U16(body, 0);
            CharacterID = Bytes.U32(body, 4);
            DestUniqueID = Bytes.U32(body, 16);
            DestFormat = (int)Bytes.U32(body, 20);
            Amount = (int)Bytes.U32(body, 28);
            StaticID = (int)Bytes.U32(body, 32);
            Town = body.Length >= 52 ? (int)Bytes.U32(body, 44) : -1;
            Shop = body.Length >= 52 ? (int)Bytes.U32(body, 48) : -1;
            if (body.Length >= 78)
            {
                Transport = new Common.Characters.Transport
                {
                    Launch = (int)Bytes.U32(body, 52),
                    Town = (int)Bytes.U32(body, 56),
                    X = (int)Bytes.U32(body, 66),
                    Y = (int)Bytes.U32(body, 70),
                    Z = (int)Bytes.U32(body, 74),
                };
            }

            ((UCGameSession)client).OnBuyItem(this);
        }
    }
}
