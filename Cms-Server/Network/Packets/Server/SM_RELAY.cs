using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// A packet one client sent another through 0x07 (CM_RELAY), passed on unchanged under its own opcode.
    /// </summary>
    public class SM_RELAY : CmsPacket
    {
        public SM_RELAY(CMSOpcode opcode, byte[] body)
        {
            this.ID = opcode;

            if (body.Length > 0)
            {
                this.PutBytes(body);
            }
        }
    }
}
