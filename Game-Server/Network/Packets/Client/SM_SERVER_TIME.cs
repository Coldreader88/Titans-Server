using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8013: uint32 BE 0x00270002, uint32 BE Unix time, 20 x 00.
    /// Java reference: NotifyServerTime.java; matches UCGOZone-Login.pcap.
    /// </summary>
    public class SM_SERVER_TIME : UCPacket<GSOpcode>
    {
        public SM_SERVER_TIME(int unixTime)
        {
            this.ID = GSOpcode.SM_SERVER_TIME;

            this.PutUIntBE(0x00270002);
            this.PutIntBE(unixTime);
            this.PutBytes(new byte[20]);
        }
    }
}
