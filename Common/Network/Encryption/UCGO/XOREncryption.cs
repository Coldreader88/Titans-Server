using System;
using System.Collections.Generic;
using System.IO;
using SmartEngine.Core;

namespace Common.Network.Encryption.UCGO
{
    /// <summary>
    /// The UCGO XOR mask.
    ///
    /// A packet's XOR key is built from a random 16 bit seed: the high word is the seed and the
    /// low word is XORTable[seed]. The data is XORed four bytes at a time with the key in
    /// little-endian order. Ported from the Java reference (mina_common crypto/XORMask.java) and
    /// the original C++ PacketHandler (Framework/Encryption/PacketHandler/XORMask.cpp).
    /// </summary>
    public static class XORMask
    {
        public const string TableFileName = "XORTable.dat";

        private const int TableEntries = 65536;

        private static readonly object tableLock = new object();
        private static ushort[] table;

        /// <summary>
        /// The 65536 entry XOR table, loaded from XORTable.dat on first use.
        /// </summary>
        public static ushort[] Table
        {
            get
            {
                if (table == null)
                {
                    lock (tableLock)
                    {
                        if (table == null)
                        {
                            table = LoadTable(FindTableFile());
                        }
                    }
                }
                return table;
            }
        }

        /// <summary>
        /// Loads the XOR table from a specific file instead of searching the default locations.
        /// </summary>
        public static void Load(string path)
        {
            var loaded = LoadTable(path);
            lock (tableLock)
            {
                table = loaded;
            }
        }

        /// <summary>
        /// Builds the 32 bit XOR key for a seed: (seed &lt;&lt; 16) | XORTable[seed].
        /// </summary>
        public static uint Keygen(ushort seed)
        {
            return ((uint)seed << 16) | Table[seed];
        }

        /// <summary>
        /// XORs <paramref name="length"/> bytes of <paramref name="data"/> starting at
        /// <paramref name="offset"/> with <paramref name="key"/>. The operation is its own inverse.
        /// </summary>
        /// <remarks>
        /// When the length is not a multiple of four, the client only touches the first leftover
        /// byte and XORs it with each remaining key byte in turn (1 left: D, 2 left: D^C,
        /// 3 left: D^C^B). This quirk comes from the client and must be kept as is.
        /// </remarks>
        public static void Apply(byte[] data, int offset, int length, uint key)
        {
            if (length <= 0)
            {
                return;
            }

            byte d = (byte)key;
            byte c = (byte)(key >> 8);
            byte b = (byte)(key >> 16);
            byte a = (byte)(key >> 24);

            int pos = offset;
            int blockEnd = offset + (length / 4) * 4;
            while (pos < blockEnd)
            {
                data[pos++] ^= d;
                data[pos++] ^= c;
                data[pos++] ^= b;
                data[pos++] ^= a;
            }

            int remaining = length & 3;
            for (int i = 0; i < remaining; i++)
            {
                data[pos] ^= (byte)(key >> (i * 8));
            }
        }

        public static void Encrypt(byte[] data, int offset, int length, uint key)
        {
            Apply(data, offset, length, key);
        }

        public static void Decrypt(byte[] data, int offset, int length, uint key)
        {
            Apply(data, offset, length, key);
        }

        private static string FindTableFile()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new List<string>
            {
                TableFileName,
                Path.Combine("DB", "Encryption", TableFileName),
                Path.Combine("..", "DB", "Encryption", TableFileName),
                Path.Combine(baseDir, TableFileName),
                Path.Combine(baseDir, "DB", "Encryption", TableFileName),
                Path.Combine(baseDir, "..", "DB", "Encryption", TableFileName),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException(
                "Could not find " + TableFileName + ". Copy DB/Encryption/" + TableFileName +
                " next to the server executable.", TableFileName);
        }

        private static ushort[] LoadTable(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < TableEntries * 2)
            {
                throw new InvalidDataException(string.Format(
                    "{0} is {1} bytes, expected {2}.", path, bytes.Length, TableEntries * 2));
            }

            var result = new ushort[TableEntries];
            for (int i = 0; i < TableEntries; i++)
            {
                result[i] = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            }

            Logger.ShowInfo("Loaded XOR table from " + Path.GetFullPath(path));
            return result;
        }
    }
}
