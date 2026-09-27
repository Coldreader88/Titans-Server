using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8042: confirms leaving the game server: uint32 BE 0x00110002, 24 x 00. Java reference: NotifyLogoutGameFE.java.
    /// </summary>
    public class SM_LOGOUT_GAME_FE : UCPacket<GSOpcode>
    {
        public SM_LOGOUT_GAME_FE()
        {
            this.ID = GSOpcode.SM_LOGOUT_GAME_FE;

            this.PutUIntBE(0x00110002);
            this.PutBytes(new byte[24]);
        }
    }
}
