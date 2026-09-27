using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x800C: team created. The 28 byte record (kind 0x1C, team id, creation time), then UC string
    /// team name and a 0 byte. Layout from EF Team Create.pcap and Create Team.pcap.
    /// </summary>
    public class SM_CREATE_TEAM : CmsPacket
    {
        public SM_CREATE_TEAM(ushort result, int teamID, uint created, string name)
        {
            this.ID = CMSOpcode.SM_CREATE_TEAM;

            this.PutRecord(0x1C, result, (uint)teamID, created, 0);
            this.PutUCString(name);
            this.PutByte(0);
        }
    }
}
