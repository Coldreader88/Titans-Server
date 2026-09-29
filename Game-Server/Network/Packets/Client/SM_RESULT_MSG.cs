using Common.Network.Packets;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// UC_ResultMsg, the client's plain reply (28 bytes): uint16 BE section, uint16 BE code (2 = done), six
    /// int32 BE values. Used for 0x8008 (value 0 = change of the criminal count), 0x800B and 0x800C (skill and
    /// status arrows) and 0x800D (the official one: section 0x1A, code 2, all zero).
    /// </summary>
    public class SM_RESULT_MSG : UCPacket<GSOpcode>
    {
        public const ushort Done = 2;

        public SM_RESULT_MSG(GSOpcode opcode, ushort section, ushort code, int value = 0)
        {
            this.ID = opcode;

            this.PutUShortBE(section);
            this.PutUShortBE(code);
            this.PutIntBE(value);
            for (int i = 1; i < 6; i++)
            {
                this.PutIntBE(0);
            }
        }
    }
}
