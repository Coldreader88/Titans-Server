using System.Collections.Generic;
using Common.Characters;
using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8034 NotifyChangeStatus: changes the client adds to its character's rank, statuses, skills and medals.
    ///
    /// <code>
    /// uint32 BE   character id
    /// UC size 1, int32 BE rank change (the client adds the low byte, signed)
    /// UC size    statuses, 6 bytes each: byte status (0 strength, 1 spirit, 2 luck), int32 BE change, byte ignore arrows
    /// UC size    skills, 7 bytes each: byte table, byte index (see <see cref="SkillTables"/>), int32 BE change in
    ///            tenths, byte ignore arrows
    /// UC size    medals, 5 bytes each: byte medal (0 Richmond, 1 Newman), int32 BE change in points
    /// </code>
    /// Layout from the client (reader 0x78d04c) and the official captures (spirit_up_1.pcap, TOMINO_X_Ambaq_.3_raise.pcap).
    /// The client adds the changes as they are, without caps or arrows, and shows each in the Skill/Status
    /// journal; "ignore arrows" marks a change that bypassed the arrows (a GM's), and the client ignores it.
    /// Only ever sent to the character's own player.
    /// </summary>
    public class SM_SKILL_GAIN : UCPacket<GSOpcode>
    {
        public SM_SKILL_GAIN(uint characterID, IList<KeyValuePair<Skill, int>> changes, bool ignoreManagement = false, int rankChange = 0,
            IList<KeyValuePair<byte, int>> medals = null)
        {
            this.ID = GSOpcode.SM_SKILL_GAIN;

            var statuses = new List<KeyValuePair<Skill, int>>();
            var skills = new List<KeyValuePair<Skill, int>>();
            foreach (var c in changes)
            {
                byte type, index;
                if (SkillTables.IsStatus(c.Key))
                {
                    statuses.Add(c);
                }
                else if (SkillTables.TryFind(c.Key, out type, out index))
                {
                    skills.Add(c);
                }
            }

            byte flag = ignoreManagement ? (byte)1 : (byte)0;
            this.PutUIntBE(characterID);
            this.PutSize(1);
            this.PutIntBE(rankChange);
            this.PutSize(statuses.Count);
            foreach (var s in statuses)
            {
                this.PutByte((byte)System.Array.IndexOf(SkillTables.Statuses, s.Key));
                this.PutIntBE(s.Value);
                this.PutByte(flag);
            }
            this.PutSize(skills.Count);
            foreach (var s in skills)
            {
                byte type, index;
                SkillTables.TryFind(s.Key, out type, out index);
                this.PutByte(type);
                this.PutByte(index);
                this.PutIntBE(s.Value);
                this.PutByte(flag);
            }
            this.PutSize(medals != null ? medals.Count : 0);
            if (medals != null)
            {
                foreach (var m in medals)
                {
                    this.PutByte(m.Key);
                    this.PutIntBE(m.Value);
                }
            }
        }
    }
}
