using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x19: adds a player who accepted a group chat invitation (sent by the one who invited).
    ///
    /// <code>
    /// uint32 BE   0xFFFFFFFF
    /// uint32 BE   new member character id
    /// uint32 BE   group chat id
    /// </code>
    /// Layout from I invite Alaron to G chat.pcap; Java: RequestGCUpdateChannel.java (unused).
    /// </summary>
    public class CM_GROUP_CHAT_ADD_MEMBER : CmsPacket
    {
        public CM_GROUP_CHAT_ADD_MEMBER()
        {
            this.ID = CMSOpcode.CM_GROUP_CHAT_ADD_MEMBER;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_GROUP_CHAT_ADD_MEMBER();
        }

        public uint Unknown { get; private set; }

        public uint MemberID { get; private set; }

        public uint ChatID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnGroupChatAddMember(this);
        }

        public void Read()
        {
            Unknown = this.GetUIntBE();
            MemberID = this.GetUIntBE();
            ChatID = this.GetUIntBE();
        }
    }
}
