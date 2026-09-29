using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x1B: releases a group chat channel.
    ///
    /// <code>
    /// uint32 BE   (unknown)
    /// uint32 BE   (unknown)
    /// uint32 BE   group chat id
    /// </code>
    /// Java reference: RequestGCChannelRelease.java (registered but commented out in CMSOpcodeMap). No
    /// official capture has it: the client leaves with 0x1A. Handled like 0x1A, so the player leaves the chat
    /// and the chat goes away once it is empty.
    /// </summary>
    public class CM_GROUP_CHAT_RELEASE : CmsPacket
    {
        public CM_GROUP_CHAT_RELEASE()
        {
            this.ID = CMSOpcode.CM_GROUP_CHAT_RELEASE;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_GROUP_CHAT_RELEASE();
        }

        public uint ChatID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            if (this.Remaining < 12)
            {
                return;
            }
            this.GetUIntBE();
            this.GetUIntBE();
            ChatID = this.GetUIntBE();

            ((UCCmsSession)client).OnGroupChatRelease(this);
        }
    }
}
