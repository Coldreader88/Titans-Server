using System;
using Common.Network.Encryption.UCGO;

namespace Common.Network.Encryption
{
    /// <summary>
    /// UCGO packet encryption.
    ///
    /// Every packet is a 64 byte header followed by the body padded to a multiple of 8 bytes.
    /// Sending: XOR-mask the header and body (64 + XORSize bytes) with a key built from a random
    /// 16 bit seed, write the seed at header offset 4, then Blowfish-encrypt the whole packet with
    /// the key "chrTCPPassword". Receiving runs the same steps backwards.
    ///
    /// Ported from the Java reference (mina_common crypto/Crypto.java) and the original C++
    /// PacketHandler (Framework/Encryption/PacketHandler/PacketHandler.cpp).
    /// </summary>
    public class UCEncryption : SmartEngine.Network.Encryption
    {
        public const string BlowfishKey = "chrTCPPassword";

        private readonly UCGO.Blowfish.Blowfish blowfish;
        private readonly Random rand;

        public UCEncryption() : this(BlowfishKey)
        {
        }

        public UCEncryption(string password)
        {
            blowfish = new UCGO.Blowfish.Blowfish(password);
            rand = new Random(Guid.NewGuid().GetHashCode());
        }

        public override SmartEngine.Network.Encryption Create()
        {
            return new UCEncryption();
        }

        /// <summary>
        /// Encrypts a complete packet in place. The header's XORSize (offset 16) must already be set
        /// and <paramref name="len"/> must be 64 + BlowfishSize.
        /// </summary>
        public override void Encrypt(byte[] src, int offset, int len)
        {
            ushort seed;
            lock (rand)
            {
                seed = (ushort)rand.Next(65536);
            }
            Encrypt(src, offset, len, seed);
        }

        /// <summary>
        /// Encrypts a complete packet in place with a given XOR seed.
        /// </summary>
        public void Encrypt(byte[] src, int offset, int len, ushort seed)
        {
            if (len < UCHeader.Size)
            {
                throw new ArgumentException("A UCGO packet is at least 64 bytes.", "len");
            }

            int xorSize = ReadInt32(src, offset + UCHeader.XORSizeOffset);
            if (xorSize < 0 || xorSize > len - UCHeader.Size)
            {
                throw new ArgumentException(string.Format("XORSize {0} does not fit in a {1} byte packet.", xorSize, len), "src");
            }

            uint key = XORMask.Keygen(seed);
            XORMask.Encrypt(src, offset, UCHeader.Size + xorSize, key);
            WriteInt32(seed, src, offset + UCHeader.SeedOffset);
            blowfish.Encrypt(src, offset, len);
        }

        /// <summary>
        /// Decrypts a complete packet (header and body) in place.
        /// </summary>
        public override void Decrypt(byte[] src, int offset, int len)
        {
            if (len < UCHeader.Size)
            {
                return;
            }

            uint key = DecryptHeader(src, offset);
            DecryptBody(src, offset, len, key);
        }

        /// <summary>
        /// Decrypts the 64 byte header at <paramref name="offset"/> in place and returns the XOR key
        /// needed for the body. Afterwards offset 4 holds the plain seed.
        /// </summary>
        public uint DecryptHeader(byte[] src, int offset)
        {
            blowfish.Decrypt(src, offset, UCHeader.Size);

            ushort seed = (ushort)(src[offset + UCHeader.SeedOffset] | (src[offset + UCHeader.SeedOffset + 1] << 8));
            uint key = XORMask.Keygen(seed);

            XORMask.Decrypt(src, offset, UCHeader.Size, key);
            WriteInt32(seed, src, offset + UCHeader.SeedOffset);

            return key;
        }

        /// <summary>
        /// Decrypts the body of a packet whose header was already decrypted with
        /// <see cref="DecryptHeader"/>. <paramref name="len"/> is the full packet length (64 + BlowfishSize).
        /// </summary>
        public void DecryptBody(byte[] src, int offset, int len, uint key)
        {
            int bodyLength = len - UCHeader.Size;
            if (bodyLength <= 0)
            {
                return;
            }

            int xorSize = ReadInt32(src, offset + UCHeader.XORSizeOffset);
            if (xorSize < 0 || xorSize > bodyLength)
            {
                xorSize = bodyLength;
            }

            blowfish.Decrypt(src, offset + UCHeader.Size, bodyLength);
            XORMask.Decrypt(src, offset + UCHeader.Size, xorSize, key);
        }

        private static int ReadInt32(byte[] buf, int ofs)
        {
            return buf[ofs] | (buf[ofs + 1] << 8) | (buf[ofs + 2] << 16) | (buf[ofs + 3] << 24);
        }

        private static void WriteInt32(int value, byte[] buf, int ofs)
        {
            buf[ofs] = (byte)value;
            buf[ofs + 1] = (byte)(value >> 8);
            buf[ofs + 2] = (byte)(value >> 16);
            buf[ofs + 3] = (byte)(value >> 24);
        }
    }
}
