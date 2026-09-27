using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0E: asks for a team's name, leader and members.
    ///
    /// <code>
    /// uint32 BE   0
    /// uint32 BE   team id
    /// byte        0xFF
    /// </code>
    /// Layout from EF Team Create.pcap; Java: RequestTeamInfo.java (unused).
    /// </summary>
    public class CM_TEAM_INFO : CmsPacket
    {
        public CM_TEAM_INFO()
        {
            this.ID = CMSOpcode.CM_TEAM_INFO;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_TEAM_INFO();
        }

        public uint Zero { get; private set; }

        public int TeamID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnTeamInfo(this);
        }

        public void Read()
        {
            Zero = this.GetUIntBE();
            TeamID = this.GetIntBE();
        }
    }
}
