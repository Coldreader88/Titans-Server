using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x30001: the client asks for its character list after a successful login.
    /// The body (9 bytes: 0, account id, 0) is not needed; the session already knows the account.
    /// Java reference: mina_loginserver RequestCharacterList.java.
    /// </summary>
    public class CM_REQUEST_CHARACTER_LIST : UCPacket<LSOpcode>
    {
        public CM_REQUEST_CHARACTER_LIST()
        {
            this.ID = LSOpcode.CM_REQUEST_CHARACTER_LIST;
        }

        public override Packet<LSOpcode> New()
        {
            return new CM_REQUEST_CHARACTER_LIST();
        }

        public override void OnProcess(Session<LSOpcode> client)
        {
            ((UCLobbySession)client).OnRequestCharacterList(this);
        }
    }
}
