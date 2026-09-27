using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8041: the game login result.
    ///
    /// <code>
    /// Accepted:  uint32 BE 0x00100002, 24 x 00
    /// Rejected:  uint32 BE 7
    /// </code>
    /// Java reference: NotifyLoginGame.java; the accepted reply matches UCGOZone-Login.pcap.
    /// </summary>
    public class SM_LOGIN_GAME : UCPacket<GSOpcode>
    {
        public SM_LOGIN_GAME(bool accepted)
        {
            this.ID = GSOpcode.SM_LOGIN_GAME;

            if (accepted)
            {
                this.PutUIntBE(0x00100002);
                this.PutBytes(new byte[24]);
            }
            else
            {
                this.PutUIntBE(0x7);
            }
        }
    }
}
