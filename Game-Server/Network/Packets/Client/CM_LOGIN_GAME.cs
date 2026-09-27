using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x41: the client logs in to the game server with the character picked in the Lobby.
    ///
    /// <code>
    /// uint32 BE   0x00010000
    /// uint32 BE   character id
    /// uint32 BE   session key (from the Lobby login reply 0x38000)
    /// uint32 BE   0
    /// UC string   character name
    /// </code>
    /// Java reference: mina_gameserver RequestLoginGame.java (which ignored the key); layout from UCGOZone-Login.pcap.
    /// </summary>
    public class CM_LOGIN_GAME : UCPacket<GSOpcode>
    {
        public CM_LOGIN_GAME()
        {
            this.ID = GSOpcode.CM_LOGIN_GAME;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_LOGIN_GAME();
        }

        public uint CharacterID { get; private set; }

        public uint SessionKey { get; private set; }

        public string Name { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            Read();

            ((UCGameSession)client).OnLoginGame(this);
        }

        public void Read()
        {
            this.GetUIntBE();
            CharacterID = this.GetUIntBE();
            SessionKey = this.GetUIntBE();
            this.GetUIntBE();
            Name = this.Remaining > 0 ? this.GetUCString() : string.Empty;
        }
    }
}
