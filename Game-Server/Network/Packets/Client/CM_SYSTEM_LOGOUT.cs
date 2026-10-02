using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x3F SystemLogout (UC_LogoutMsg, 16 bytes: player id, section 1, ticket, empty name, nationality, empty IP):
    /// the client quits after 0x42; it waits up to 90 s for 0x803F, then shows "disconnected" (specs/re-unknown-packets.md 1.1).
    /// </summary>
    public class CM_SYSTEM_LOGOUT : UCPacket<GSOpcode>
    {
        public CM_SYSTEM_LOGOUT()
        {
            this.ID = GSOpcode.CM_SYSTEM_LOGOUT;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_SYSTEM_LOGOUT();
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
            ((UCGameSession)client).OnSystemLogout(this);
        }
    }
}
