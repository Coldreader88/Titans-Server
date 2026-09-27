using Common.Network.Packets;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8016: who is in a group chat.
    ///
    /// <code>
    /// uint32 BE   group chat id
    /// uint32 BE   creation time
    /// UC size     member count
    ///   uint32 BE   character id
    ///   uint32 BE   group chat id
    ///   uint32 BE   0
    ///   UC string   name
    ///   byte        gender
    ///   byte        rank
    /// </code>
    /// Layout from I invite Alaron to G chat.pcap; Java: GCJoinChannel.java / Channel.java.
    /// </summary>
    public class SM_GROUP_CHAT_MEMBERS : CmsPacket
    {
        public SM_GROUP_CHAT_MEMBERS(GroupChat chat)
        {
            this.ID = CMSOpcode.SM_GROUP_CHAT_MEMBERS;

            this.PutUIntBE(chat.ID);
            this.PutUIntBE(chat.Created);
            this.PutSize(chat.Members.Count);
            foreach (var m in chat.Members)
            {
                this.PutUIntBE(m.ClientID);
                this.PutUIntBE(chat.ID);
                this.PutUIntBE(0);
                this.PutUCString(m.Name);
                this.PutByte(m.Gender);
                this.PutByte(m.Rank);
            }
        }
    }
}
