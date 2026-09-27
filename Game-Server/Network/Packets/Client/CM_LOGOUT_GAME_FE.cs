using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x42: the client leaves the game server. Java reference: RequestLogoutGameFE.java.
    /// </summary>
    public class CM_LOGOUT_GAME_FE : UCPacket<GSOpcode>
    {
        public CM_LOGOUT_GAME_FE()
        {
            this.ID = GSOpcode.CM_LOGOUT_GAME_FE;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_LOGOUT_GAME_FE();
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
            ((UCGameSession)client).OnLogoutGameFE(this);
        }
    }
}
