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
    /// Java reference: PlayerLooksBuff.java; matches the official reply in UCGOZone-Login.pcap.
    ///
    /// A player in a vehicle looks like the vehicle:
    /// <code>
    /// uint32 BE   0x00040002
    /// uint32 BE   character id
    /// uint32 BE   vehicle template id
    /// UC size    + uint32 BE weapon template id per armament slot (-1 = empty)
    /// UC size    + one byte per slot (00 = the weapon in hand, FF = not)
    /// UC size    0
    /// uint16 BE   counter the official server raised whenever the armaments changed
    /// </code>
    /// A vehicle without weapons is "80 80 80" (official captures, e.g. BATTLE 1.pcap). Weapons are not
    /// implemented yet, so that is what is sent.
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

        public SM_PLAYER_LOOKS(Character character, int vehicleTemplateID)
        {
            this.ID = GSOpcode.SM_PLAYER_LOOKS;

            this.PutUIntBE(0x00040002);
            this.PutUIntBE(character.ClientID);
            this.PutIntBE(vehicleTemplateID);
            this.PutSize(0);
            this.PutSize(0);
            this.PutSize(0);
            this.PutUShortBE(0);
        }
    }
}
