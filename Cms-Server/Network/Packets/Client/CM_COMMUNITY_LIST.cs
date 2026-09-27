using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x20: asks for the friend list.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   0
    /// </code>
    /// Java reference: RequestCommunityList.java; layout from UCGOZone-Login.pcap.
    /// </summary>
    public class CM_COMMUNITY_LIST : CmsPacket
    {
        public CM_COMMUNITY_LIST()
        {
            this.ID = CMSOpcode.CM_COMMUNITY_LIST;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_COMMUNITY_LIST();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnCommunityList(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
        }
    }
}
