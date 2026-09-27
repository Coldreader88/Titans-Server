using Common.Network.Packets;

namespace TitansUC.CmsServer.Network.Packets.Server
{
    /// <summary>
    /// 0x801E (added) and 0x801F (deleted): a change to the player's friend list. The 28 byte record
    /// (kind 0x30 or 0x32, the friend's character id), then an empty UC size.
    /// Layout from I Requested to add ALaron as Friend and he accepted.pcap and Delete Friend (I delete light).pcap.
    /// </summary>
    public class SM_FRIEND_CHANGE : CmsPacket
    {
        public SM_FRIEND_CHANGE(CMSOpcode opcode, ushort result, uint friendID)
        {
            this.ID = opcode;

            this.PutRecord(opcode == CMSOpcode.SM_FRIEND_ADDED ? (ushort)0x30 : (ushort)0x32, result, 0, 0, friendID);
            this.PutSize(0);
        }
    }
}
