using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8004: a chat message.
    ///
    /// <code>
    /// uint32 BE   sender character id (0 for system messages)
    /// UC string   message
    /// uint32 BE   chat type (see ChatType)
    /// uint32 BE   0
    /// UC size     recipient count
    /// uint32 BE   recipient character id, for each
    /// </code>
    /// The official server sent each recipient its own copy listing only that recipient, and echoed the
    /// message to the sender listing the sender. System messages (type 7) list nobody.
    /// Java reference: NotifyChatMSG.java; layout from chat.pcap and Reavo Blocks + Server announcement.pcap.
    /// </summary>
    public class SM_CHAT_MSG : CmsPacket
    {
        public SM_CHAT_MSG(uint senderID, string message, uint chatType, params uint[] recipients)
        {
            this.ID = CMSOpcode.SM_CHAT_MSG;

            this.PutUIntBE(senderID);
            this.PutUCString(message);
            this.PutUIntBE(chatType);
            this.PutUIntBE(0);
            this.PutIDs(recipients);
        }
    }
}
