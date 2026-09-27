using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x800E (joined), 0x800F (left) and 0x8011 (kicked): a team member change, sent to the whole team
    /// including the member it is about. The 28 byte record (kind 0x1D, 0x20 or 0x1F, team id, character
    /// id), then UC string name and a 0 byte.
    /// Layout from Team Invite (He Accepted).pcap, Team Member (He Leaves).pcap and Team Kick (I kick).pcap.
    /// </summary>
    public class SM_TEAM_MEMBER : CmsPacket
    {
        public SM_TEAM_MEMBER(CMSOpcode opcode, int teamID, uint characterID, string name)
        {
            this.ID = opcode;

            ushort kind = opcode == CMSOpcode.SM_TEAM_MEMBER_JOINED ? (ushort)0x1D
                : opcode == CMSOpcode.SM_TEAM_MEMBER_KICKED ? (ushort)0x1F : (ushort)0x20;
            this.PutRecord(kind, ResultYes, (uint)teamID, 0, characterID);
            this.PutUCString(name);
            this.PutByte(0);
        }
    }
}
