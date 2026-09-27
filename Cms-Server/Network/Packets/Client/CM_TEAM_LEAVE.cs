using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x10: leaves the team.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   team id
    /// </code>
    /// Layout from Light Leave Team.pcap.
    /// </summary>
    public class CM_TEAM_LEAVE : CmsPacket
    {
        public CM_TEAM_LEAVE()
        {
            this.ID = CMSOpcode.CM_TEAM_LEAVE;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_TEAM_LEAVE();
        }

        public uint CharacterID { get; private set; }

        public int TeamID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnTeamLeave(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
            TeamID = this.GetIntBE();
        }
    }
}
