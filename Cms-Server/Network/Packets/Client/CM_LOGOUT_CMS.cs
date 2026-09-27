using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x02: the client leaves the CMS server.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   0
    /// </code>
    /// Java reference: RequestLogoutCMS.java; layout from MASSIVE END BATTLE LOG.pcap.
    /// </summary>
    public class CM_LOGOUT_CMS : CmsPacket
    {
        public CM_LOGOUT_CMS()
        {
            this.ID = CMSOpcode.CM_LOGOUT_CMS;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_LOGOUT_CMS();
        }

        public uint CharacterID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnLogout(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
        }
    }
}
