using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8052: tells the client to log out (maintenance): uint32 BE character id, 24 x 00.
    /// Java reference: NotifyForceLogout.java.
    /// </summary>
    public class SM_FORCE_LOGOUT : UCPacket<GSOpcode>
    {
        public SM_FORCE_LOGOUT(uint characterID)
        {
            this.ID = GSOpcode.SM_FORCE_LOGOUT;

            this.PutUIntBE(characterID);
            this.PutBytes(new byte[24]);
        }
    }
}
