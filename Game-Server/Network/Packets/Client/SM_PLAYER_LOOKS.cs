using Common.Network.Packets;
using Common.Characters;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x800A: how a player on foot looks.
    ///
    /// <code>
    /// uint32 BE   0x00030002
    /// uint32 BE   character id
    /// uint16 BE   9
    /// byte       gender
    /// looks (see <see cref="CharacterLooks"/>)
    /// </code>
    /// Java reference: PlayerLooksBuff.java; matches the official reply in UCGOZone-Login.pcap. Players in a vehicle
    /// (0x00040002 with the armaments) are not implemented yet.
    /// </summary>
    public class SM_PLAYER_LOOKS : UCPacket<GSOpcode>
    {
        public SM_PLAYER_LOOKS(Character character)
        {
            this.ID = GSOpcode.SM_PLAYER_LOOKS;

            this.PutUIntBE(0x00030002);
            this.PutUIntBE(character.ClientID);
            this.PutUShortBE(0x0009);
            this.PutByte((byte)character.Gender);
            CharacterLooks.Write(this, character);
        }
    }
}
