using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8025: the vehicle's owner was changed. Every official reply (Transfer_Oggo, Empty_Oggo, LOG3,
    /// TEST_2 and TEST_Z_GUNDAM pcaps) was the same: uint32 BE 1, 2, 0.
    /// </summary>
    public class SM_CHANGE_MACHINE_OWNER : UCPacket<GSOpcode>
    {
        public SM_CHANGE_MACHINE_OWNER()
        {
            this.ID = GSOpcode.SM_CHANGE_MACHINE_OWNER;

            this.PutIntBE(1);
            this.PutIntBE(2);
            this.PutIntBE(0);
        }
    }
}
