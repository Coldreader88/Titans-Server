using Common.Characters;
using Common.Network.Packets;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38002: everything the character select screen shows about one character (see
    /// <see cref="PlayerInfoWriter"/>).
    /// </summary>
    public class SM_PLAYER_INFO : UCPacket<LSOpcode>
    {
        public SM_PLAYER_INFO(uint accountID, Character c)
        {
            this.ID = LSOpcode.SM_PLAYER_INFO;

            PlayerInfoWriter.Write(this, accountID, c);
        }
    }
}
