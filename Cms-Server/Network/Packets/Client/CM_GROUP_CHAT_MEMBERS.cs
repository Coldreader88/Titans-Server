using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x18: asks who is in a group chat.
    ///
    /// <code>
    /// uint32 BE   0
    /// uint32 BE   group chat id
    /// byte        0xFF
    /// </code>
    /// Layout from I invite Alaron to G chat.pcap; Java: RequestGCJoinChannel.java (unused).
    /// </summary>
    public class CM_GROUP_CHAT_MEMBERS : CmsPacket
    {
        public CM_GROUP_CHAT_MEMBERS()
        {
            this.ID = CMSOpcode.CM_GROUP_CHAT_MEMBERS;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_GROUP_CHAT_MEMBERS();
        }

        public uint Zero { get; private set; }

        public uint ChatID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnGroupChatMembers(this);
        }

        public void Read()
        {
            Zero = this.GetUIntBE();
            ChatID = this.GetUIntBE();
        }
    }
}
