using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x13: the player's chat card, sent after logging in.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   team id (-1 = none)
    /// uint32 BE   0
    /// UC string   name
    /// byte        gender
    /// byte        rank
    /// </code>
    /// Java reference: RequestChatInfo.java; layout from Login GM.pcap.
    /// </summary>
    public class CM_CHAT_INFO : CmsPacket
    {
        public CM_CHAT_INFO()
        {
            this.ID = CMSOpcode.CM_CHAT_INFO;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_CHAT_INFO();
        }

        public uint CharacterID { get; private set; }

        public int TeamID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnChatInfo(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
            TeamID = this.GetIntBE();
        }
    }
}
