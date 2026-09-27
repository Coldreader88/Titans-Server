using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x55: the player logs out (uint32 BE character id). Java reference: RequestLogoutGS.java.
    /// </summary>
    public class CM_LOGOUT_GS : UCPacket<GSOpcode>
    {
        public CM_LOGOUT_GS()
        {
            this.ID = GSOpcode.CM_LOGOUT_GS;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_LOGOUT_GS();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnLogoutGS(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
        }
    }
}
