using Common.Network.Packets;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8012: a player's chat card, sent to the player and their team when they log in.
    /// uint32 BE character id, int32 BE team id, uint32 BE 2, UC string name, byte gender, byte rank.
    /// Java reference: NotifyChatInfo.java; layout from Login GM.pcap and Team Member Log On (+TeamChatNotice).pcap.
    /// </summary>
    public class SM_CHAT_INFO : CmsPacket
    {
        public SM_CHAT_INFO(Member member)
        {
            this.ID = CMSOpcode.SM_CHAT_INFO;

            this.PutUIntBE(member.ClientID);
            this.PutIntBE(member.TeamID);
            this.PutUIntBE(2);
            this.PutUCString(member.Name);
            this.PutByte(member.Gender);
            this.PutByte(member.Rank);
        }
    }
}
