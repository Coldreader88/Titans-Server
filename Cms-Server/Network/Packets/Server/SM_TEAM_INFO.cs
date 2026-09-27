using System.Collections.Generic;
using System.Linq;
using Common.Network.Packets;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x800D: a team's name, leader and members.
    ///
    /// <code>
    /// int32 BE    team id
    /// uint32 BE   creation time
    /// uint32 BE   leader character id
    /// UC string   team name
    /// UC size     member count, leader first
    ///   uint32 BE   character id
    ///   int32 BE    team id
    ///   uint32 BE   creation time for the leader, else 0
    ///   UC string   name
    ///   byte        gender
    ///   byte        rank
    /// </code>
    /// Layout from EF Team Create.pcap and Team Invite (He Accepted).pcap; Java: TeamMemberList.java.
    /// </summary>
    public class SM_TEAM_INFO : CmsPacket
    {
        public SM_TEAM_INFO(Team team, IList<Member> members)
        {
            this.ID = CMSOpcode.SM_TEAM_INFO;

            this.PutIntBE(team.ID);
            this.PutUIntBE(team.Created);
            this.PutUIntBE(team.LeaderID);
            this.PutUCString(team.Name);

            var ordered = members.OrderBy(m => m.ClientID == team.LeaderID ? 0 : 1).ToList();
            this.PutSize(ordered.Count);
            foreach (var m in ordered)
            {
                this.PutUIntBE(m.ClientID);
                this.PutIntBE(team.ID);
                this.PutUIntBE(m.ClientID == team.LeaderID ? team.Created : 0);
                this.PutUCString(m.Name);
                this.PutByte(m.Gender);
                this.PutByte(m.Rank);
            }
        }
    }
}
