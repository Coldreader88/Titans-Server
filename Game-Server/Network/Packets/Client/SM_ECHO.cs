using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// A reply that is the request with 2 in bytes 2-3, as the official server answered 0x15 (0x8015) and
    /// 0x19 (0x8019).
    /// </summary>
    public class SM_ECHO : UCPacket<GSOpcode>
    {
        public SM_ECHO(GSOpcode opcode, byte[] request)
        {
            this.ID = opcode;

            var body = (byte[])request.Clone();
            body[2] = 0;
            body[3] = 2;
            this.PutBytes(body);
        }
    }
}
