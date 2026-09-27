using System;

namespace Common.Network
{
    /// <summary>
    /// The 64 byte header that starts every UCGO packet. All fields are little-endian.
    ///
    /// <code>
    /// 0x00  "head" magic (0x64616568)
    /// 0x04  XOR key seed (16 bits, written by the encryption)
    /// 0x08  unused
    /// 0x0C  sequence number
    /// 0x10  XOR size: length of the body
    /// 0x14  Blowfish size: body length rounded up to a multiple of 8
    /// 0x18  opcode
    /// 0x1C  unused (zero)
    /// 0x3C  "tail" magic (0x6C696174)
    /// </code>
    /// </summary>
    public class UCHeader
    {
        public const int Size = 64;

        public const uint HEAD = 0x64616568;
        public const uint TAIL = 0x6C696174;

        public const int HeadOffset = 0x00;
        public const int SeedOffset = 0x04;
        public const int SequenceOffset = 0x0C;
        public const int XORSizeOffset = 0x10;
        public const int BlowfishSizeOffset = 0x14;
        public const int OpcodeOffset = 0x18;
        public const int TailOffset = 0x3C;

        private uint? blowfishSize;

        public uint Head { get; set; }

        /// <summary>
        /// The XOR key seed found at offset 4. Only meaningful for received packets; outgoing
        /// packets get a fresh random seed from <see cref="Encryption.UCEncryption"/>.
        /// </summary>
        public uint XORKey { get; set; }

        public uint Sequence { get; set; }

        public uint Opcode { get; set; }

        /// <summary>
        /// Length of the packet body in bytes.
        /// </summary>
        public uint XORSize { get; set; }

        /// <summary>
        /// Length of the (padded) body on the wire. For headers read from the network this is the
        /// value that was received; otherwise it is <see cref="XORSize"/> rounded up to 8.
        /// </summary>
        public uint BlowfishSize
        {
            get { return blowfishSize ?? CalculateBlowfishSize(XORSize); }
            set { blowfishSize = value; }
        }

        public uint Tail { get; set; }

        /// <summary>
        /// True when the head and tail magic values are present.
        /// </summary>
        public bool IsValid
        {
            get { return Head == HEAD && Tail == TAIL && XORSize <= BlowfishSize; }
        }

        public UCHeader()
        {
            Head = HEAD;
            Tail = TAIL;
        }

        public UCHeader(uint packetLen, uint xorKey, uint opcode, uint sequence) : this()
        {
            this.XORKey = xorKey;
            this.Sequence = sequence;
            this.XORSize = packetLen;
            this.Opcode = opcode;
        }

        /// <summary>
        /// Rounds a body length up to the next multiple of 8 (the Blowfish block size).
        /// </summary>
        public static uint CalculateBlowfishSize(uint xorSize)
        {
            return (xorSize + 7) & ~7u;
        }

        /// <summary>
        /// Writes the header into a new 64 byte array.
        /// </summary>
        public byte[] Create()
        {
            var buffer = new byte[Size];
            WriteTo(buffer, 0);
            return buffer;
        }

        /// <summary>
        /// Writes the header into <paramref name="buffer"/> at <paramref name="offset"/>.
        /// </summary>
        public void WriteTo(byte[] buffer, int offset)
        {
            Array.Clear(buffer, offset, Size);
            WriteUInt32(HEAD, buffer, offset + HeadOffset);
            WriteUInt32(XORKey & 0xFFFF, buffer, offset + SeedOffset);
            WriteUInt32(Sequence, buffer, offset + SequenceOffset);
            WriteUInt32(XORSize, buffer, offset + XORSizeOffset);
            WriteUInt32(BlowfishSize, buffer, offset + BlowfishSizeOffset);
            WriteUInt32(Opcode, buffer, offset + OpcodeOffset);
            WriteUInt32(TAIL, buffer, offset + TailOffset);
        }

        /// <summary>
        /// Reads a decrypted header from the start of <paramref name="packet"/>.
        /// </summary>
        public static UCHeader Read(byte[] packet)
        {
            return Read(packet, 0);
        }

        /// <summary>
        /// Reads a decrypted header from <paramref name="packet"/> at <paramref name="offset"/>.
        /// Returns null when fewer than 64 bytes are available.
        /// </summary>
        public static UCHeader Read(byte[] packet, int offset)
        {
            if (packet == null || packet.Length - offset < Size)
            {
                return null;
            }

            return new UCHeader
            {
                Head = ReadUInt32(packet, offset + HeadOffset),
                XORKey = ReadUInt32(packet, offset + SeedOffset) & 0xFFFF,
                Sequence = ReadUInt32(packet, offset + SequenceOffset),
                XORSize = ReadUInt32(packet, offset + XORSizeOffset),
                BlowfishSize = ReadUInt32(packet, offset + BlowfishSizeOffset),
                Opcode = ReadUInt32(packet, offset + OpcodeOffset),
                Tail = ReadUInt32(packet, offset + TailOffset),
            };
        }

        public override string ToString()
        {
            return string.Format("Opcode=0x{0:X5} Seq={1} XORSize={2} BlowfishSize={3} Seed=0x{4:X4}",
                Opcode, Sequence, XORSize, BlowfishSize, XORKey);
        }

        private static uint ReadUInt32(byte[] buf, int ofs)
        {
            return (uint)(buf[ofs] | (buf[ofs + 1] << 8) | (buf[ofs + 2] << 16) | (buf[ofs + 3] << 24));
        }

        private static void WriteUInt32(uint value, byte[] buf, int ofs)
        {
            buf[ofs] = (byte)value;
            buf[ofs + 1] = (byte)(value >> 8);
            buf[ofs + 2] = (byte)(value >> 16);
            buf[ofs + 3] = (byte)(value >> 24);
        }
    }
}
