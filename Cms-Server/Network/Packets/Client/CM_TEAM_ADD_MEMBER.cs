using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0F: adds a player who accepted a team invitation (sent by the one who invited).
    ///
    /// <code>
    /// uint32 BE   inviting character id
    /// uint32 BE   new member character id
    /// uint32 BE   team id
    /// </code>
    /// Layout from Team Invite (He Accepted).pcap.
    /// </summary>
    public class CM_TEAM_ADD_MEMBER : CmsPacket
    {
        public CM_TEAM_ADD_MEMBER()
        {
            this.ID = CMSOpcode.CM_TEAM_ADD_MEMBER;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_TEAM_ADD_MEMBER();
        }

        public uint InviterID { get; private set; }

        public uint MemberID { get; private set; }

        public int TeamID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnTeamAddMember(this);
        }

        public void Read()
        {
            InviterID = this.GetUIntBE();
            MemberID = this.GetUIntBE();
            TeamID = this.GetIntBE();
        }
    }
}
