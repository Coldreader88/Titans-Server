using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x07: a packet for other players (invitations and their answers).
    ///
    /// <code>
    /// uint32 BE   opcode the recipients get it under (see CMSOpcode RELAY_*)
    /// UC size     recipient count
    /// uint32 BE   recipient character id, for each
    /// ...         the packet body, passed on unchanged
    /// </code>
    /// The official server passed the body on to each recipient (friend request 0x25 in
    /// "I Requested to add ALaron as Friend and he accepted.pcap", group chat invitation 0x1E, team
    /// invitation 0x15, and the answers 0x8022, 0x801C, 0x8014). Java reference: RequestInvite.java and
    /// CMSHandler.java, which handled a few of these cases one by one.
    /// </summary>
    public class CM_RELAY : CmsPacket
    {
        public CM_RELAY()
        {
            this.ID = CMSOpcode.CM_RELAY;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_RELAY();
        }

        public uint Opcode { get; private set; }

        public List<uint> Recipients { get; private set; }

        public byte[] Body { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnRelay(this);
        }

        public void Read()
        {
            Opcode = this.GetUIntBE();
            Recipients = ReadIDs(this);
            Body = this.GetBytes((ushort)this.Remaining);
        }
    }
}
