using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x22: sells an item (or a vehicle from the hangar) to a shop.
    ///
    /// <code>
    /// uint16 BE   1, uint16 BE 0
    /// uint32 BE   character id
    /// uint32 BE   7
    /// uint32 BE   item static id
    /// uint32 BE   item unique id, format
    /// uint32 BE   container unique id, format, static id
    /// uint32 BE   0x0B, 0
    /// uint32 BE   amount
    /// uint32 BE   0
    /// uint32 BE   price (0 in the request)
    /// uint32 BE   town, shop (SHOPINFOTEMPLATE), faction
    /// </code>
    /// Layout from the official captures (Alumina (121).pcap, Sold_RGM-79.pcap and others).
    /// </summary>
    public class CM_SELL_ITEM : UCPacket<GSOpcode>
    {
        public const int PriceOffset = 52;
        public const int ResultOffset = 8;

        public CM_SELL_ITEM()
        {
            this.ID = GSOpcode.CM_SELL_ITEM;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SELL_ITEM();
        }

        public byte[] Body { get; private set; }
        public uint CharacterID { get; private set; }
        public int StaticID { get; private set; }
        public uint ItemUniqueID { get; private set; }
        public uint ContainerUniqueID { get; private set; }
        public int Amount { get; private set; }
        public int Town { get; private set; }
        public int Shop { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < PriceOffset + 4)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(Body, 4);
            StaticID = (int)Bytes.U32(Body, 12);
            ItemUniqueID = Bytes.U32(Body, 16);
            ContainerUniqueID = Bytes.U32(Body, 24);
            Amount = (int)Bytes.U32(Body, 44);
            Town = Body.Length >= 64 ? (int)Bytes.U32(Body, 56) : -1;
            Shop = Body.Length >= 64 ? (int)Bytes.U32(Body, 60) : -1;

            ((UCGameSession)client).OnSellItem(this);
        }
    }
}
