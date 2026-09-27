using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x04: keep-alive (body: 0xFFFFFFFF, 0).
    /// Java reference: RequestCMSHeartbeat.java; layout from chat.pcap.
    /// </summary>
    public class CM_HEARTBEAT : CmsPacket
    {
        public CM_HEARTBEAT()
        {
            this.ID = CMSOpcode.CM_HEARTBEAT;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_HEARTBEAT();
        }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            ((UCCmsSession)client).OnHeartbeat(this);
        }
    }
}
