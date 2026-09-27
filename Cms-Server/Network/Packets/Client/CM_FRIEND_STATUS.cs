using System.Collections.Generic;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;

namespace TitansUC.CmsServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0B: asks which friends are online.
    ///
    /// <code>
    /// uint32 BE   0x800A (the reply opcode)
    /// UC size     count
    /// uint32 BE   character id, for each
    /// </code>
    /// Java reference: RequestOnlineFriends.java; layout from Add Friend (He Accepts).pcap.
    /// </summary>
    public class CM_FRIEND_STATUS : CmsPacket
    {
        public CM_FRIEND_STATUS()
        {
            this.ID = CMSOpcode.CM_FRIEND_STATUS;
        }

        public override Packet<CMSOpcode> New()
        {
            return new CM_FRIEND_STATUS();
        }

        public uint ReplyOpcode { get; private set; }

        public List<uint> IDs { get; private set; }

        public override void OnProcess(Session<CMSOpcode> client)
        {
            Read();

            ((UCCmsSession)client).OnFriendStatus(this);
        }

        public void Read()
        {
            ReplyOpcode = this.GetUIntBE();
            IDs = ReadIDs(this);
        }
    }
}
