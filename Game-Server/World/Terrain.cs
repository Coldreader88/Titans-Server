using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The ground height of Earth (Australia), from the client's terrain file object/terrain/au.tr (DB/Terrain/au.tr).
    ///
    /// The file is zlib data (78 DA) and a 32-byte trailer (int32 LE: two unknowns, the compressed size, the
    /// uncompressed size 54000000, ...). Unpacked it is a 6000 x 4500 grid of int16 LE heights in metres, row 0 the
    /// north edge, one cell per map unit (4000 position units, about 1/120 degree). The client's own grid origin
    /// (uc.exe 0x6397b9: 0xa0d960 = 13300, 0xa0d964 = -12000; width/height at 0xa1f4b8) puts cell
    /// (x / 4000 - 13300, -y / 4000 - 12000) under position (x, y).
    ///
    /// A position's z is 4 per metre: the captured NPCs (0x8003) stand at 4 x the height between the 4 cells
    /// around them (bilinear), e.g. 1841 where the grid says 460.3 m. The client's *_NPC.LST files use 100 per metre
    /// instead. Towns are levelled, so their z can differ from the grid; below 0 is sea.
    /// </summary>
    public static class Terrain
    {
        public const int Width = 6000;
        public const int Height = 4500;
        public const int CellSize = 4000;
        public const int OriginX = 13300;
        public const int OriginY = 12000;
        public const int ZPerMetre = 4;

        private static short[] heights;
        private static int loading;

        public static bool Loaded { get { return heights != null; } }

        /// <summary>
        /// Reads DB/Terrain/au.tr (about a second; the Earth server does it at start). Without the file every
        /// lookup gives null and callers keep the heights they have.
        /// </summary>
        public static void Load()
        {
            if (Interlocked.Exchange(ref loading, 1) == 1)
            {
                return;
            }
            try
            {
                string path = CharacterData.FindFile("Terrain", "au.tr");
                var grid = new short[Width * Height];
                var bytes = new byte[Width * Height * 2];
                using (var file = File.OpenRead(path))
                {
                    // Skip the 2-byte zlib header; DeflateStream reads the raw deflate data after it.
                    file.ReadByte();
                    file.ReadByte();
                    using (var deflate = new DeflateStream(file, CompressionMode.Decompress))
                    {
                        int done = 0;
                        while (done < bytes.Length)
                        {
                            int n = deflate.Read(bytes, done, bytes.Length - done);
                            if (n <= 0)
                            {
                                throw new InvalidDataException("the terrain ends after " + done + " bytes");
                            }
                            done += n;
                        }
                    }
                }
                Buffer.BlockCopy(bytes, 0, grid, 0, bytes.Length);
                heights = grid;
                Logger.ShowInfo("Loaded the terrain of Earth (6000 x 4500 heights).");
            }
            catch (Exception ex)
            {
                Logger.ShowWarning("No terrain heights (DB/Terrain/au.tr): " + ex.Message);
            }
        }

        /// <summary>
        /// The ground's height in metres at a position, between the 4 grid cells around it; null off the grid or
        /// without the terrain file.
        /// </summary>
        public static double? Metres(int x, int y)
        {
            var grid = heights;
            if (grid == null)
            {
                return null;
            }
            double c = x / (double)CellSize - OriginX;
            double r = -y / (double)CellSize - OriginY;
            if (c < 0 || r < 0 || c > Width - 1 || r > Height - 1)
            {
                return null;
            }
            int c0 = Math.Min((int)c, Width - 2), r0 = Math.Min((int)r, Height - 2);
            double fc = c - c0, fr = r - r0;
            int i = r0 * Width + c0;
            return grid[i] * (1 - fc) * (1 - fr) + grid[i + 1] * fc * (1 - fr) +
                grid[i + Width] * (1 - fc) * fr + grid[i + Width + 1] * fc * fr;
        }

        /// <summary>
        /// The z of the ground at a position (<see cref="ZPerMetre"/> per metre), or null as <see cref="Metres"/>.
        /// </summary>
        public static int? GroundZ(int x, int y)
        {
            var m = Metres(x, y);
            return m.HasValue ? (int?)(int)Math.Round(m.Value * ZPerMetre) : null;
        }

        /// <summary>
        /// Whether the position is over the sea (ground below 0 m); false off the grid.
        /// </summary>
        public static bool IsSea(int x, int y)
        {
            var m = Metres(x, y);
            return m.HasValue && m.Value < 0;
        }
    }
}
