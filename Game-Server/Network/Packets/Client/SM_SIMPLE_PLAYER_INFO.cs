using Common.Network.Packets;
using Common.Characters;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8006: name and faction of a player.
    ///
    /// <code>
    /// uint32 BE   account id (-1 for an NPC)
    /// uint32 BE   id
    /// byte       0
    /// byte       faction
    /// UC string   name
    /// </code>
    /// For an unknown id the Java server sent -1, -1. Java reference: NotifySimplePlayerInfo.java; matches UCGOZone-Login.pcap.
    /// </summary>
    public class SM_SIMPLE_PLAYER_INFO : UCPacket<GSOpcode>
    {
        public SM_SIMPLE_PLAYER_INFO(uint accountID, Character character)
        {
            this.ID = GSOpcode.SM_SIMPLE_PLAYER_INFO;

            if (character == null)
            {
                this.PutIntBE(-1);
                this.PutIntBE(-1);
                return;
            }

            this.PutUIntBE(accountID);
            this.PutUIntBE(character.ClientID);
            this.PutByte(0);
            this.PutByte((byte)character.Faction);
            this.PutUCString(character.Name);
        }
    }
}
