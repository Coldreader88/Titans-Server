using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8005: uint32 BE 0, 0xFFFFFFFF, 0xFFFFFFFF.
    /// Java reference: NotifyCMSHeartbeat.java; matches chat.pcap.
    /// </summary>
    public class SM_HEARTBEAT : CmsPacket
    {
        public SM_HEARTBEAT()
        {
            this.ID = CMSOpcode.SM_HEARTBEAT;

            this.PutUIntBE(0);
            this.PutUIntBE(0xFFFFFFFF);
            this.PutUIntBE(0xFFFFFFFF);
        }
    }
}
