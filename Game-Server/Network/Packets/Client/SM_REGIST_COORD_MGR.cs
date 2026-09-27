using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x8000: confirms 0x00: uint32 BE 0x00120002, 24 x 00.
    /// Java reference: NotifyRegistCoordMgr.java; matches UCGOZone-Login.pcap.
    /// </summary>
    public class SM_REGIST_COORD_MGR : UCPacket<GSOpcode>
    {
        public SM_REGIST_COORD_MGR()
        {
            this.ID = GSOpcode.SM_REGIST_COORD_MGR;

            this.PutUIntBE(0x00120002);
            this.PutBytes(new byte[24]);
        }
    }
}
