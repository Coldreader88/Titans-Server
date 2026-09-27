using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0D: creates a team.
    ///
    /// <code>
    /// UC string   team name
    /// uint32 BE   creator character id
    /// </code>
    /// Layout from EF Team Create.pcap and Create Team.pcap; Java: RequestCreateTeam.java (unused).
    /// </summary>
    public class CM_CREATE_TEAM : CmsPacket
    {
        public CM_CREATE_TEAM()
        {
            this.ID = CMSOpcode.CM_CREATE_TEAM;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_CREATE_TEAM();
        }

        public string Name { get; private set; }

        public uint CreatorID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnCreateTeam(this);
        }

        public void Read()
        {
            Name = this.GetUCString();
            CreatorID = this.GetUIntBE();
        }
    }
}
