using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x0D: a query the client sends now and then with the body 00 00 00. Its meaning is not known; the only
    /// official one (TEST_Z_GUNDAM.pcap, in Space) was answered with 0x800D: uint16 BE 0x001A, uint16 BE 2
    /// (done), then 24 zero bytes. The same answer is sent back so the client is not left waiting.
    /// </summary>
    public class CM_UNKNOWN_0D : UCPacket<GSOpcode>
    {
        private static readonly byte[] OfficialReply = { 0x00, 0x1A, 0x00, 0x02,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public CM_UNKNOWN_0D()
        {
            this.ID = GSOpcode.CM_UNKNOWN_0D;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_UNKNOWN_0D();
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
            ((UCGameSession)client).Network.SendPacket(new SM_RAW((uint)GSOpcode.SM_UNKNOWN_0D, OfficialReply));
        }
    }
}
