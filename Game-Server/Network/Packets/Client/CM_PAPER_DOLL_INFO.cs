using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x6F: asks for a player's profile window: uint32 BE, uint32 BE character id.
    /// Java reference: RequestPaperDollInfo.java / PaperDoll.java.
    /// </summary>
    public class CM_PAPER_DOLL_INFO : UCPacket<GSOpcode>
    {
        public CM_PAPER_DOLL_INFO()
        {
            this.ID = GSOpcode.CM_PAPER_DOLL_INFO;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_PAPER_DOLL_INFO();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnPaperDollInfo(this);
        }

        public void Read()
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
        }
    }
}
