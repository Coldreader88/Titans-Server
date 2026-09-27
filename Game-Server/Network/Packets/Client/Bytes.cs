namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// Big-endian reads and writes in a request body that is echoed back (0x23, 0x24).
    /// </summary>
    public static class Bytes
    {
        public static ushort U16(byte[] b, int offset)
        {
            return (ushort)((b[offset] << 8) | b[offset + 1]);
        }

        public static uint U32(byte[] b, int offset)
        {
            return ((uint)b[offset] << 24) | ((uint)b[offset + 1] << 16) | ((uint)b[offset + 2] << 8) | b[offset + 3];
        }

        public static void PutU32(byte[] b, int offset, uint value)
        {
            b[offset] = (byte)(value >> 24);
            b[offset + 1] = (byte)(value >> 16);
            b[offset + 2] = (byte)(value >> 8);
            b[offset + 3] = (byte)value;
        }

        public static byte[] Slice(byte[] b, int offset, int count)
        {
            var result = new byte[count];
            System.Array.Copy(b, offset, result, 0, count);
            return result;
        }
    }
}
