using System.Collections.Generic;
using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8034: stat and skill gains of a character.
    ///
    /// <code>
    /// uint32 BE   character id
    /// UC size 1, 4 bytes 0      (unknown)
    /// UC size    stats, 6 bytes each: byte stat (0 strength, 1 spirit, 2 luck), int32 BE gain, byte 0
    /// UC size    skills, 7 bytes each: uint16 BE skill id, int32 BE gain in 0.1 points, byte 0
    /// UC size 0
    /// </code>
    /// Layout from the official captures (spirit_up_1.pcap, Weapon_Manipulation_0.1___Ambac_0.1_(MS06RP).pcap).
    /// A combat skill's id is its place in the player info's combat skill list (0x04 space engagement,
    /// 0x11 AMBAC, 0x14 emergency repair).
    /// </summary>
    public class SM_SKILL_GAIN : UCPacket<GSOpcode>
    {
        public SM_SKILL_GAIN(uint characterID, IList<KeyValuePair<byte, int>> stats, IList<KeyValuePair<ushort, int>> skills)
        {
            this.ID = GSOpcode.SM_SKILL_GAIN;

            this.PutUIntBE(characterID);
            this.PutSize(1);
            this.PutIntBE(0);
            this.PutSize(stats.Count);
            foreach (var s in stats)
            {
                this.PutByte(s.Key);
                this.PutIntBE(s.Value);
                this.PutByte(0);
            }
            this.PutSize(skills.Count);
            foreach (var s in skills)
            {
                this.PutUShortBE(s.Key);
                this.PutIntBE(s.Value);
                this.PutByte(0);
            }
            this.PutSize(0);
        }
    }
}
