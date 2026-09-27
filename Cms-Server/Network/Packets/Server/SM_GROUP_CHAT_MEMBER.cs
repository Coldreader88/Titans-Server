using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8017 (joined) and 0x8018 (left): a group chat member change, sent to everyone in the chat
    /// including the member it is about. The 28 byte record (kind 0x2A or 0x2D, group chat id, character id),
    /// then UC string name and a 0 byte. Layout from I invite Alaron to G chat.pcap and Alaron Left G chat.pcap.
    /// </summary>
    public class SM_GROUP_CHAT_MEMBER : CmsPacket
    {
        public SM_GROUP_CHAT_MEMBER(CMSOpcode opcode, uint chatID, uint characterID, string name)
        {
            this.ID = opcode;

            this.PutRecord(opcode == CMSOpcode.SM_GROUP_CHAT_MEMBER_JOINED ? (ushort)0x2A : (ushort)0x2D, ResultYes, chatID, 0, characterID);
            this.PutUCString(name);
            this.PutByte(0);
        }
    }
}
