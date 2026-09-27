using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x03: a chat message, or a GM command when it starts with #.
    ///
    /// <code>
    /// uint32 BE   sender character id
    /// UC string   message
    /// uint32 BE   chat type (see ChatType)
    /// uint32 BE   0
    /// UC size     recipient count
    /// uint32 BE   recipient character id, for each
    /// </code>
    /// The client picks the recipients itself (players near it, the team, the group chat, the tell target).
    /// Java reference: RequestChatMSG.java; layout from chat.pcap and Chat Zeon Ef.pcap.
    /// </summary>
    public class CM_CHAT_MSG : CmsPacket
    {
        public CM_CHAT_MSG()
        {
            this.ID = CMSOpcode.CM_CHAT_MSG;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_CHAT_MSG();
        }

        public uint SenderID { get; private set; }

        public string Message { get; private set; }

        public uint ChatType { get; private set; }

        public uint Unknown { get; private set; }

        public List<uint> Recipients { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnChatMsg(this);
        }

        public void Read()
        {
            SenderID = this.GetUIntBE();
            Message = this.GetUCString();
            ChatType = this.Remaining >= 4 ? this.GetUIntBE() : 0;
            Unknown = this.Remaining >= 4 ? this.GetUIntBE() : 0;
            Recipients = ReadIDs(this);
        }
    }
}
