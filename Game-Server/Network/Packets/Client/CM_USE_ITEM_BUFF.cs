using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x1C: use an item on the piloted vehicle (an ER kit repairs it): uint16 BE 6, uint16 BE 0, uint32 BE
    /// character id, item unique id and format, container unique id and format, uint32 BE 0, uint32 BE amount.
    /// Layout from MS_ER_1_Success.pcap and MS_ER_1_Fail.pcap.
    /// </summary>
    public class CM_USE_ITEM_BUFF : UCPacket<GSOpcode>
    {
        public CM_USE_ITEM_BUFF()
        {
            this.ID = GSOpcode.CM_USE_ITEM_BUFF;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_USE_ITEM_BUFF();
        }

        public byte[] Body { get; private set; }
        public uint CharacterID { get; private set; }
        public uint ItemUID { get; private set; }
        public uint ContainerUID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 32)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(Body, 4);
            ItemUID = Bytes.U32(Body, 8);
            ContainerUID = Bytes.U32(Body, 16);

            ((UCGameSession)client).OnUseItemBuff(this);
        }
    }
}
