using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8055: confirms a logout: uint32 BE character id, 24 x 00. Java reference: NotifyLogoutGS.java.
    /// </summary>
    public class SM_LOGOUT_GS : UCPacket<GSOpcode>
    {
        public SM_LOGOUT_GS(uint characterID)
        {
            this.ID = GSOpcode.SM_LOGOUT_GS;

            this.PutUIntBE(characterID);
            this.PutBytes(new byte[24]);
        }
    }
}
