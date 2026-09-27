using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x1A: leaves a group chat.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   group chat id
    /// </code>
    /// Layout from Group invite and chat.pcap; Java: RequestGCLeave.java (unused).
    /// </summary>
    public class CM_GROUP_CHAT_LEAVE : CmsPacket
    {
        public CM_GROUP_CHAT_LEAVE()
        {
            this.ID = CMSOpcode.CM_GROUP_CHAT_LEAVE;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_GROUP_CHAT_LEAVE();
        }

        public uint CharacterID { get; private set; }

        public uint ChatID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnGroupChatLeave(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
            ChatID = this.GetUIntBE();
        }
    }
}
