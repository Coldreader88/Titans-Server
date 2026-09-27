using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x12: the leader removes a member.
    ///
    /// <code>
    /// uint32 BE   leader character id
    /// uint32 BE   removed character id
    /// uint32 BE   team id
    /// </code>
    /// Layout from Team Kick (I kick).pcap.
    /// </summary>
    public class CM_TEAM_KICK : CmsPacket
    {
        public CM_TEAM_KICK()
        {
            this.ID = CMSOpcode.CM_TEAM_KICK;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_TEAM_KICK();
        }

        public uint LeaderID { get; private set; }

        public uint MemberID { get; private set; }

        public int TeamID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnTeamKick(this);
        }

        public void Read()
        {
            LeaderID = this.GetUIntBE();
            MemberID = this.GetUIntBE();
            TeamID = this.GetIntBE();
        }
    }
}
