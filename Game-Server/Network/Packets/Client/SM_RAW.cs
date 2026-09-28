using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// A packet a client asked to have sent to other players (0x39, 0x3A), body unchanged.
    /// </summary>
    public class SM_RAW : UCPacket<GSOpcode>
    {
        public SM_RAW(uint opcode, byte[] body)
        {
            this.ID = (GSOpcode)opcode;
            this.PutBytes(body);
        }
    }
}
