using System.Collections.Generic;
using Common.Network.Packets;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x801D: the friend list.
    ///
    /// <code>
    /// UC size     count
    ///   uint32 BE   character id
    ///   UC string   name
    ///   byte        1 (the official server also sent 2; meaning unknown)
    ///   uint16 BE   0
    ///   byte        rank
    ///   UC size     0
    /// </code>
    /// Java reference: FriendsList.java; layout from UCGOZone-Login.pcap and Add Friend (He Accepts).pcap.
    /// </summary>
    public class SM_COMMUNITY_LIST : CmsPacket
    {
        public SM_COMMUNITY_LIST(IList<Member> friends)
        {
            this.ID = CMSOpcode.SM_COMMUNITY_LIST;

            this.PutSize(friends.Count);
            foreach (var f in friends)
            {
                this.PutUIntBE(f.ClientID);
                this.PutUCString(f.Name);
                this.PutByte(1);
                this.PutUShortBE(0);
                this.PutByte(f.Rank);
                this.PutSize(0);
            }
        }
    }
}
