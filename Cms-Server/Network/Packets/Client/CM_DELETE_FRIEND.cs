using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x22: removes a friend.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   friend character id
    /// </code>
    /// Layout from Delete Friend (I delete light).pcap.
    /// </summary>
    public class CM_DELETE_FRIEND : CmsPacket
    {
        public CM_DELETE_FRIEND()
        {
            this.ID = CMSOpcode.CM_DELETE_FRIEND;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_DELETE_FRIEND();
        }

        public uint CharacterID { get; private set; }

        public uint FriendID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnDeleteFriend(this);
        }

        public void Read()
        {
            CharacterID = this.GetUIntBE();
            FriendID = this.GetUIntBE();
        }
    }
}
