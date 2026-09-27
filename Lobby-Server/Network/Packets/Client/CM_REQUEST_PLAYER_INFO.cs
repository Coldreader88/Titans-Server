using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x30002: the client asks for one character from the character list.
    ///
    /// <code>
    /// uint32 BE   0
    /// uint32 BE   character id (as sent in 0x38001)
    /// byte        1
    /// </code>
    /// Java reference: mina_loginserver RequestPlayerInfo.java.
    /// </summary>
    public class CM_REQUEST_PLAYER_INFO : UCPacket<LSOpcode>
    {
        public CM_REQUEST_PLAYER_INFO()
        {
            this.ID = LSOpcode.CM_REQUEST_PLAYER_INFO;
        }

        public override Packet<LSOpcode> New()
        {
            return new CM_REQUEST_PLAYER_INFO();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<LSOpcode> client)
        {
            Read();

            ((UCLobbySession)client).OnRequestPlayerInfo(this);
        }

        public void Read()
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
        }
    }
}
