using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x30005: the player picked a character and asks where the game server is.
    ///
    /// <code>
    /// uint32 BE   session key (from 0x38000)
    /// uint32 BE   account id
    /// uint32 BE   character id
    /// 00 01 FF FF 80
    /// </code>
    /// Java reference: mina_loginserver RequestGameServerIP.java (which ignored the key); layout from the
    /// official request in UCGOZone-Login.pcap.
    /// </summary>
    public class CM_REQUEST_GAME_SERVER : UCPacket<LSOpcode>
    {
        public CM_REQUEST_GAME_SERVER()
        {
            this.ID = LSOpcode.CM_REQUEST_GAME_SERVER;
        }

        public override Packet<LSOpcode> New()
        {
            return new CM_REQUEST_GAME_SERVER();
        }

        public uint SessionKey { get; private set; }

        public uint AccountID { get; private set; }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<LSOpcode> client)
        {
            Read();

            ((UCLobbySession)client).OnRequestGameServer(this);
        }

        public void Read()
        {
            SessionKey = this.GetUIntBE();
            AccountID = this.GetUIntBE();
            CharacterID = this.GetUIntBE();
        }
    }
}
