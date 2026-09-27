using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0A: asks which team members are online.
    ///
    /// <code>
    /// uint32 BE   0x8009 (the reply opcode)
    /// UC size     count
    /// uint32 BE   character id, for each
    /// </code>
    /// Layout from Team Invite (He Accepted).pcap; Java: RequestTeamMemberStatus.java (unused).
    /// </summary>
    public class CM_TEAM_MEMBER_STATUS : CmsPacket
    {
        public CM_TEAM_MEMBER_STATUS()
        {
            this.ID = CMSOpcode.CM_TEAM_MEMBER_STATUS;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_TEAM_MEMBER_STATUS();
        }

        public uint ReplyOpcode { get; private set; }

        public List<uint> IDs { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnTeamMemberStatus(this);
        }

        public void Read()
        {
            ReplyOpcode = this.GetUIntBE();
            IDs = ReadIDs(this);
        }
    }
}
