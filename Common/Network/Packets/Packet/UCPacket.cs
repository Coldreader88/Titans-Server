using System;
using System.Text;
using SmartEngine.Network;

namespace Common.Network.Packets
{
    public enum PacketType
    {
        UNKNOWN,
        SERVER,
        CLIENT,
    }

    /// <summary>
    /// A UCGO packet body.
    ///
    /// The buffer holds only the body; the 64 byte header is described by <see cref="Header"/> and
    /// is built by <see cref="ToWire"/> when the packet is sent. The opcode lives in <see cref="ID"/>.
    /// Replies use the request opcode with 0x8000 added (0x30000 -> 0x38000).
    ///
    /// UCGO bodies mostly use big-endian integers, "UC sizes" (a length byte with the high bit set)
    /// and UTF-16LE strings prefixed with a UC size. The helpers below follow the Java reference
    /// (mina_common net/UCBuffer.java).
    /// </summary>
    public class UCPacket<T> : Packet<T>
    {
        /// <summary>
        /// Added to a request opcode to get the reply opcode.
        /// </summary>
        public const uint ReplyFlag = 0x8000;

        public UCHeader Header { get; set; }

        public PacketType Type { get; set; }

        /// <summary>
        /// The complete decrypted packet (header and body) this packet was read from, if any.
        /// </summary>
        public byte[] OriginalPacket { get; set; }

        /// <summary>
        /// Opcode
        /// </summary>
        public override T ID { get; set; }

        /// <summary>
        /// The opcode as a number.
        /// </summary>
        public uint Opcode
        {
            get { return Convert.ToUInt32(ID); }
        }

        /// <summary>
        /// Number of body bytes left to read from the current position.
        /// </summary>
        public int Remaining
        {
            get { return Math.Max(0, (int)Length - (int)Position); }
        }

        public UCPacket() : base()
        {
            this.Type = PacketType.UNKNOWN;
            this.Header = new UCHeader();
        }

        /// <summary>
        /// Creates a packet holding <paramref name="body"/>, positioned at the start of the body.
        /// </summary>
        public UCPacket(byte[] body, UCHeader header = null)
            : base()
        {
            this.Type = PacketType.UNKNOWN;
            this.Header = header ?? new UCHeader();

            if (body != null && body.Length > 0)
            {
                this.PutBytes(body, 0);
            }
            this.Position = 0;
        }

        #region Reading

        public int GetIntBE()
        {
            return (int)GetUIntBE();
        }

        public uint GetUIntBE()
        {
            EnsureReadable(4);
            var b = GetBytes(4);
            return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
        }

        public short GetShortBE()
        {
            return (short)GetUShortBE();
        }

        public ushort GetUShortBE()
        {
            EnsureReadable(2);
            var b = GetBytes(2);
            return (ushort)((b[0] << 8) | b[1]);
        }

        /// <summary>
        /// Reads a UC size. One byte with the high bit set holds sizes up to 0x7F; larger sizes use
        /// two bytes: the low 7 bits, then the count of 0x80s with the high bit set.
        /// </summary>
        public int GetSize()
        {
            EnsureReadable(1);
            byte first = GetByte();
            if ((first & 0x80) != 0)
            {
                return first & 0x7F;
            }

            EnsureReadable(1);
            byte second = GetByte();
            return ((second & 0x7F) * 0x80) + first;
        }

        /// <summary>
        /// Reads a UC string: a UC size holding the number of characters, then UTF-16LE text.
        /// </summary>
        public string GetUCString()
        {
            int chars = GetSize();
            EnsureReadable(chars * 2);
            return Encoding.Unicode.GetString(GetBytes((ushort)(chars * 2)));
        }

        /// <summary>
        /// Reads a UC size holding a byte count, then that many raw bytes.
        /// </summary>
        public byte[] GetUCBytes()
        {
            int count = GetSize();
            EnsureReadable(count);
            return GetBytes((ushort)count);
        }

        #endregion

        #region Writing

        public void PutIntBE(int value)
        {
            PutUIntBE((uint)value);
        }

        public void PutUIntBE(uint value)
        {
            PutBytes((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }

        public void PutShortBE(short value)
        {
            PutUShortBE((ushort)value);
        }

        public void PutUShortBE(ushort value)
        {
            PutBytes((byte)(value >> 8), (byte)value);
        }

        /// <summary>
        /// Writes a UC size (see <see cref="GetSize"/>).
        /// </summary>
        public void PutSize(int size)
        {
            if (size < 0 || size >= 0x80 * 0x80)
            {
                throw new ArgumentOutOfRangeException("size", size, "A UC size must be between 0 and 0x3FFF.");
            }

            if (size <= 0x7F)
            {
                PutByte((byte)(0x80 | size));
            }
            else
            {
                PutByte((byte)(size % 0x80));
                PutByte((byte)(0x80 | (size / 0x80)));
            }
        }

        /// <summary>
        /// Writes a UC string: a UC size holding the number of characters, then UTF-16LE text.
        /// </summary>
        public void PutUCString(string str)
        {
            str = str ?? string.Empty;
            PutSize(str.Length);
            PutBytes(Encoding.Unicode.GetBytes(str));
        }

        #endregion

        /// <summary>
        /// Builds the unencrypted wire form of this packet: the 64 byte header followed by the body
        /// padded with zeros to a multiple of 8 bytes.
        /// </summary>
        public byte[] ToWire(uint sequence)
        {
            var body = ToArray();

            var header = new UCHeader((uint)body.Length, 0, Opcode, sequence);
            var wire = new byte[UCHeader.Size + header.BlowfishSize];

            header.WriteTo(wire, 0);
            Array.Copy(body, 0, wire, UCHeader.Size, body.Length);

            this.Header = header;
            return wire;
        }

        /// <summary>
        /// Hex dump of the body, 16 bytes per line.
        /// </summary>
        public string DumpData2()
        {
            var sb = new StringBuilder();
            var bytes = ToArray();

            for (int i = 0; i < bytes.Length; i++)
            {
                sb.AppendFormat("{0:X2} ", bytes[i]);
                if ((i + 1) % 16 == 0)
                {
                    sb.Append("\r\n");
                }
            }
            return sb.ToString();
        }

        private void EnsureReadable(int count)
        {
            if (count > Remaining)
            {
                throw new InvalidOperationException(string.Format(
                    "Packet 0x{0:X5} needs {1} more bytes but only {2} are left.", Opcode, count, Remaining));
            }
        }
    }

    public class UCLoginPacket : UCPacket<Network.Packets.ISOpcode>
    {

        public UCLoginPacket() : base()
        {

        }

        public UCLoginPacket(byte[] buffer)
            : base(buffer)
        {

        }
    }
}
