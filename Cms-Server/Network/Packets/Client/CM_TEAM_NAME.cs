using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x08: asks for the name of a team the client sees on a player or NPC.
    ///
    /// <code>
    /// uint32 BE   0
    /// uint32 BE   team id
    /// byte        0xFF
    /// </code>
    /// Java reference: RequestTeamName.java; layout from xenolog.pcap.
    /// </summary>
    public class CM_TEAM_NAME : CmsPacket
    {
        public CM_TEAM_NAME()
        {
            this.ID = CMSOpcode.CM_TEAM_NAME;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_TEAM_NAME();
        }

        public uint Zero { get; private set; }

        public int TeamID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnTeamName(this);
        }

        public void Read()
        {
            Zero = this.GetUIntBE();
            TeamID = this.GetIntBE();
        }
    }
}
