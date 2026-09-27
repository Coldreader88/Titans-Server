using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x8021: a friend came online (uint32 BE 2, character id, 1) or went offline (2, character id, 0xFFFFFFFF).
    /// Java reference: NotifyFriendsRegisterOnline.java; layout from Team Member Log On / Log Off (+TeamChatNotice).pcap.
    /// </summary>
    public class SM_FRIEND_ONLINE : CmsPacket
    {
        public SM_FRIEND_ONLINE(uint characterID, bool online)
        {
            this.ID = CMSOpcode.SM_FRIEND_ONLINE;

            this.PutUIntBE(2);
            this.PutUIntBE(characterID);
            this.PutUIntBE(online ? 1u : 0xFFFFFFFF);
        }
    }
}
