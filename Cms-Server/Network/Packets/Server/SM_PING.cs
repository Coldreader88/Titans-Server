using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x00: empty keep-alive. The official CMS sent one right after each 0x8005 heartbeat reply
    /// (186 of them against 187 heartbeats across the captures in UCGO Packet Logs.zip). The client does
    /// not answer it.
    /// </summary>
    public class SM_PING : CmsPacket
    {
        public SM_PING()
        {
            this.ID = CMSOpcode.SM_PING;
        }
    }
}
