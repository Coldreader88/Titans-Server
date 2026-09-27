using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8007: UC string team name, int32 BE team id, uint16 BE 2.
    /// Java reference: NotifyTeamName.java; matches xenolog.pcap.
    /// </summary>
    public class SM_TEAM_NAME : CmsPacket
    {
        public SM_TEAM_NAME(string name, int teamID)
        {
            this.ID = CMSOpcode.SM_TEAM_NAME;

            this.PutUCString(name);
            this.PutIntBE(teamID);
            this.PutUShortBE(2);
        }
    }
}
