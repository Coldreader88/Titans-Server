using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8015: group chat opened. The 28 byte record (kind 0x29, group chat id, creation time).
    /// Layout from Invite Group Chat Fail (Target Already in Chat).pcap.
    /// </summary>
    public class SM_GROUP_CHAT_CREATE : CmsPacket
    {
        public SM_GROUP_CHAT_CREATE(uint chatID, uint created)
        {
            this.ID = CMSOpcode.SM_GROUP_CHAT_CREATE;

            this.PutRecord(0x29, ResultYes, chatID, created, 0);
        }
    }
}
