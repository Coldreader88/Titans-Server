using Common.Network.Packets;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38001: the character list.
    ///
    /// <code>
    /// uint32 BE   account id
    /// byte        0
    /// UC size     number of characters (0x80 = none)
    /// per character: uint32 BE account id, uint32 BE character id, uint32 BE -1, uint32 BE -1
    /// </code>
    /// Characters are not stored yet, so the list is always empty.
    /// Java reference: mina_loginserver NotifyCharacterList.java.
    /// </summary>
    public class SM_CHARACTER_LIST : UCPacket<LSOpcode>
    {
        public SM_CHARACTER_LIST(UCLobbySession client)
        {
            this.ID = LSOpcode.SM_CHARACTER_LIST;

            this.PutUIntBE(client.account.AccountID);
            this.PutByte(0);
            this.PutSize(0);
        }
    }
}
