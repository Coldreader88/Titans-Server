using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8013: a team member came online or went offline.
    /// Online: uint32 BE 0, character id, 1. Offline: uint32 BE 2, character id, 0xFFFFFFFF.
    /// Java reference: NotifyTeamRegisterOnline.java; layout from Team Member Log On / Log Off (+TeamChatNotice).pcap.
    /// </summary>
    public class SM_TEAM_ONLINE : CmsPacket
    {
        public SM_TEAM_ONLINE(uint characterID, bool online)
        {
            this.ID = CMSOpcode.SM_TEAM_ONLINE;

            this.PutUIntBE(online ? 0u : 2u);
            this.PutUIntBE(characterID);
            this.PutUIntBE(online ? 1u : 0xFFFFFFFF);
        }
    }
}
