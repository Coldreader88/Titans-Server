using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x17: opens a new group chat to invite someone to.
    ///
    /// <code>
    /// 28 byte record (see CmsRecord), with the character to invite at offset 12
    /// </code>
    /// Layout from Invite Group Chat Fail (Target Already in Chat).pcap; Java: RequestGCInitiate.java (unused).
    /// </summary>
    public class CM_GROUP_CHAT_CREATE : CmsPacket
    {
        public CM_GROUP_CHAT_CREATE()
        {
            this.ID = CMSOpcode.CM_GROUP_CHAT_CREATE;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_GROUP_CHAT_CREATE();
        }

        public uint InviteeID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnGroupChatCreate(this);
        }

        public void Read()
        {
            InviteeID = ReadInvitee(this);
        }
    }
}
