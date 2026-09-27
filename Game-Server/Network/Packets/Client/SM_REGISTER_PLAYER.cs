using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8038: confirms 0x38.
    ///
    /// <code>
    /// uint16 BE   account level (GM tag, 0x0A for a player)
    /// uint16 BE   2
    /// 8 x 00
    /// uint32 BE   character id
    /// 12 x 00
    /// </code>
    /// Java reference: NotifyRegisterPlayer.java; matches UCGOZone-Login.pcap.
    /// </summary>
    public class SM_REGISTER_PLAYER : UCPacket<GSOpcode>
    {
        public SM_REGISTER_PLAYER(byte accountLevel, uint characterID)
        {
            this.ID = GSOpcode.SM_REGISTER_PLAYER;

            this.PutUShortBE(accountLevel);
            this.PutUShortBE(0x0002);
            this.PutBytes(new byte[8]);
            this.PutUIntBE(characterID);
            this.PutBytes(new byte[12]);
        }
    }
}
