using System.Collections.Generic;
using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8009 (team members) and 0x800A (friends): which of the asked characters are online.
    ///
    /// <code>
    /// UC size     count
    /// uint32 BE   character id, then uint32 BE status (1 = online, 0 = offline), for each
    /// </code>
    /// The official server also answered 0xFFFFFFFF for some characters; what that meant is unknown.
    /// Layout from Add Friend (He Accepts).pcap and Team Invite (He Accepted).pcap. Java: NotifyOnlineFriends.java.
    /// </summary>
    public class SM_ONLINE_STATUS : CmsPacket
    {
        public SM_ONLINE_STATUS(CMSOpcode opcode, IList<KeyValuePair<uint, bool>> statuses)
        {
            this.ID = opcode;

            this.PutSize(statuses.Count);
            foreach (var status in statuses)
            {
                this.PutUIntBE(status.Key);
                this.PutUIntBE(status.Value ? 1u : 0u);
            }
        }
    }
}
