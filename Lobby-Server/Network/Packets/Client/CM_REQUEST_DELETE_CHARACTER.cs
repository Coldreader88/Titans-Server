using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x30004: the client deletes a character.
    ///
    /// <code>
    /// uint32 BE   character id (as sent in 0x38001)
    /// uint32 BE   account id
    /// byte        0
    /// </code>
    /// Java reference: mina_loginserver RequestDeleteCharacter.java; layout checked against the official
    /// request in "char delete.txt" (UCGO Packet Logs.zip).
    /// </summary>
    public class CM_REQUEST_DELETE_CHARACTER : UCPacket<LSOpcode>
    {
        public CM_REQUEST_DELETE_CHARACTER()
        {
            this.ID = LSOpcode.CM_REQUEST_DELETE_CHARACTER;
        }

        public override Packet<LSOpcode> New()
        {
            return new CM_REQUEST_DELETE_CHARACTER();
        }

        public uint CharacterID { get; private set; }

        public uint AccountID { get; private set; }

        public override void OnProcess(Session<LSOpcode> client)
        {
            Read();

            ((UCLobbySession)client).OnRequestDeleteCharacter(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
            AccountID = this.GetUIntBE();
        }
    }
}
