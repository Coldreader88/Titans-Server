using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8002: uint32 BE 0, 0xFFFFFFFF, 0xFFFFFFFF.
    /// Java reference: NotifyLogoutCMS.java (which sent 0, 0xFF, 0xFF); layout from MASSIVE END BATTLE LOG.pcap.
    /// </summary>
    public class SM_LOGOUT_CMS : CmsPacket
    {
        public SM_LOGOUT_CMS()
        {
            this.ID = CMSOpcode.SM_LOGOUT_CMS;

            this.PutUIntBE(0);
            this.PutUIntBE(0xFFFFFFFF);
            this.PutUIntBE(0xFFFFFFFF);
        }
    }
}
