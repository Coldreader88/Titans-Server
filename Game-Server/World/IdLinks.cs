using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The client's IDLink.bin (DB/Templates/IDLink.bin): which model the client draws for a template id. It is
    /// zlib data with a 32-byte trailer; inflated, it is little-endian: u16 count, then per record u32 id,
    /// string .mef model ("dummy" for hand-held equipment), u16 kind, u32 (unused by the client), string .DET
    /// mesh, string .rsb, string .AET motion (strings: u16 char count + UTF-16LE). Kind 0 = equipment (weapons
    /// and shields), 1 = mobile suit, 3 = ground vehicle, 4 = aircraft or ship. The 349 ids are template ids;
    /// all but 460006/460007 (placeholder tanks, model "------------") are also in a template table.
    /// </summary>
    public static class IdLinks
    {
        public class Link
        {
            public int ID { get; set; }
            public string Model { get; set; }
            public int Kind { get; set; }
            public string Mesh { get; set; }

            public bool IsVehicle { get { return Kind != 0; } }

            /// <summary>
            /// The model's file name without folder and extension (MS06_ZAKU2), the mesh's for equipment.
            /// </summary>
            public string ShortName
            {
                get
                {
                    var file = Model == "dummy" || string.IsNullOrEmpty(Model) ? Mesh : Model;
                    return Path.GetFileNameWithoutExtension((file ?? "").Replace('\\', '/'));
                }
            }
        }

        private static readonly object loadLock = new object();
        private static Dictionary<int, Link> links;

        public static Link Get(int id)
        {
            EnsureLoaded();
            Link l;
            return links.TryGetValue(id, out l) ? l : null;
        }

        public static IEnumerable<Link> All
        {
            get
            {
                EnsureLoaded();
                return links.Values;
            }
        }

        internal static void EnsureLoaded()
        {
            if (links != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (links != null)
                {
                    return;
                }
                var result = new Dictionary<int, Link>();
                try
                {
                    byte[] d;
                    using (var file = File.OpenRead(CharacterData.FindFile("Templates", "IDLink.bin")))
                    {
                        // Skip the 2-byte zlib header; the 32-byte trailer after the deflate data is never read.
                        file.ReadByte();
                        file.ReadByte();
                        using (var deflate = new DeflateStream(file, CompressionMode.Decompress))
                        using (var mem = new MemoryStream())
                        {
                            deflate.CopyTo(mem);
                            d = mem.ToArray();
                        }
                    }
                    int p = 0;
                    int count = U16(d, ref p);
                    for (int i = 0; i < count && p < d.Length; i++)
                    {
                        var l = new Link { ID = (int)U32(d, ref p), Model = Str(d, ref p), Kind = U16(d, ref p) };
                        p += 4;
                        l.Mesh = Str(d, ref p);
                        Str(d, ref p);
                        Str(d, ref p);
                        result[l.ID] = l;
                    }
                    Logger.ShowInfo(string.Format("Loaded {0} model links (IDLink.bin).", result.Count));
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load IDLink.bin: " + ex.Message);
                }
                links = result;
            }
        }

        private static int U16(byte[] d, ref int p)
        {
            int v = d[p] | (d[p + 1] << 8);
            p += 2;
            return v;
        }

        private static uint U32(byte[] d, ref int p)
        {
            uint v = BitConverter.ToUInt32(d, p);
            p += 4;
            return v;
        }

        private static string Str(byte[] d, ref int p)
        {
            int n = U16(d, ref p);
            var s = Encoding.Unicode.GetString(d, p, n * 2);
            p += n * 2;
            return s;
        }
    }
}
