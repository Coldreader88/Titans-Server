using System.Collections.Generic;
using Common.Characters;
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
    /// per character, in slot order: uint32 BE account id, uint32 BE character id (client id),
    ///             uint32 BE -1, uint32 BE -1
    /// </code>
    /// The client then asks for each character with 0x30002.
    /// Java reference: mina_loginserver NotifyCharacterList.java; matches the official server's reply
    /// in UCGOCharCreation.pcap.
    /// </summary>
    public class SM_CHARACTER_LIST : UCPacket<LSOpcode>
    {
        public SM_CHARACTER_LIST(UCLobbySession client, IList<Character> characters)
        {
            this.ID = LSOpcode.SM_CHARACTER_LIST;

            this.PutUIntBE(client.account.AccountID);
            this.PutByte(0);
            this.PutSize(characters.Count);

            foreach (var character in characters)
            {
                this.PutUIntBE(client.account.AccountID);
                this.PutUIntBE(character.ClientID);
                this.PutIntBE(-1);
                this.PutIntBE(-1);
            }
        }
    }
}
