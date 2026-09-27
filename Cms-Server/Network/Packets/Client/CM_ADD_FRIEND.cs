using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x21: adds a friend (after they accepted the request).
    ///
    /// <code>
    /// uint32 BE   0xFFFFFFFF
    /// uint32 BE   friend character id
    /// </code>
    /// Layout from I Requested to add ALaron as Friend and he accepted.pcap; Java: RequestAddToFriendsList.java (unused).
    /// </summary>
    public class CM_ADD_FRIEND : CmsPacket
    {
        public CM_ADD_FRIEND()
        {
            this.ID = CMSOpcode.CM_ADD_FRIEND;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_ADD_FRIEND();
        }

        public uint Unknown { get; private set; }

        public uint FriendID { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnAddFriend(this);
        }

        public void Read()
        {
            Unknown = this.GetUIntBE();
            FriendID = this.GetUIntBE();
        }
    }
}
