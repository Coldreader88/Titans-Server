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
    /// A vehicle without weapons is "80 80 80" (official captures, e.g. BATTLE 1.pcap). With weapons the first
    /// list holds each armament slot's template (-1 empty) and the second one FF per slot.
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

        /// <summary>
        /// The looks counter the official server sent for NPCs most often (6..0x0B seen).
        /// </summary>
        public const ushort NpcCounter = 6;

        /// <summary>
        /// A vehicle: its template, the template of each armament slot (-1 empty) and one byte per slot (FF
        /// for players, 00 for NPCs, as in the captures).
        /// </summary>
        public SM_PLAYER_LOOKS(uint characterID, int vehicleTemplateID, int[] armaments, ushort counter, byte slotByte = 0xFF)
        {
            this.ID = GSOpcode.SM_PLAYER_LOOKS;

            this.PutUIntBE(0x00040002);
            this.PutUIntBE(characterID);
            this.PutIntBE(vehicleTemplateID);
            this.PutSize(armaments.Length);
            foreach (var template in armaments)
            {
                this.PutIntBE(template);
            }
            this.PutSize(armaments.Length);
            foreach (var template in armaments)
            {
                this.PutByte(slotByte);
            }
            this.PutSize(0);
            this.PutUShortBE(counter);
        }
    }
}
