using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Mobile suit and tank templates from DB/Templates/MSTEMPLATE.DAT and TANKTEMPLATE.DAT: name, price,
    /// engine and health.
    ///
    /// <code>
    /// uint32 BE (skipped), UC size = number of templates, then per template:
    /// uint32 BE id, uint32 BE 0, 3 x UC size + UTF-16LE (comment, model code, name),
    /// 4 bytes, uint32 BE price, 116 bytes, uint32 BE engine id (0 = none), 60 bytes, uint32 BE health,
    /// then 30 bytes (37 for a few ids, see <see cref="LongRecords"/>)
    /// </code>
    /// Java reference: mina_common template/MSTemplate.java (same skips); checked to parse both files to
    /// their last byte. Other vehicles (cars, fighters, ships) are not loaded and use defaults.
    /// </summary>
    public static class VehicleTemplates
    {
        public const int DefaultHealth = 2000;

        /// <summary>
        /// Engine the Java server gave vehicles whose template names none (Engine.DEFAULT_ID).
        /// </summary>
        public const int DefaultEngine = 290012;

        private static readonly HashSet<int> LongRecords = new HashSet<int> { 410011, 410020, 410012, 410013, 410044, 410056, 410061 };

        private static readonly object loadLock = new object();
        private static Dictionary<int, VehicleTemplate> templates;

        public static VehicleTemplate Get(int id)
        {
            EnsureLoaded();
            VehicleTemplate t;
            return templates.TryGetValue(id, out t) ? t : null;
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return templates.Count;
            }
        }

        private static void EnsureLoaded()
        {
            if (templates != null)
            {
                return;
            }

            lock (loadLock)
            {
                if (templates != null)
                {
                    return;
                }

                var result = new Dictionary<int, VehicleTemplate>();
                foreach (var file in new[] { "MSTEMPLATE.DAT", "TANKTEMPLATE.DAT" })
                {
                    try
                    {
                        Load(File.ReadAllBytes(CharacterData.FindFile("Templates", file)), result);
                    }
                    catch (Exception ex)
                    {
                        Logger.ShowWarning(string.Format("Could not load {0}: {1}", file, ex.Message));
                    }
                }

                Logger.ShowInfo(string.Format("Loaded {0} vehicle templates.", result.Count));
                templates = result;
            }
        }

        private static void Load(byte[] d, Dictionary<int, VehicleTemplate> result)
        {
            int p = 4;
            int count = ReadSize(d, ref p);
            for (int i = 0; i < count; i++)
            {
                var t = new VehicleTemplate();
                t.ID = ReadInt(d, ref p);
                p += 4;
                ReadString(d, ref p);
                t.Model = ReadString(d, ref p);
                t.Name = ReadString(d, ref p);
                p += 4;
                t.Price = ReadInt(d, ref p);
                int m = p + 75;
                t.CombatValue = ReadInt(d, ref m);
                p += 116;
                t.EngineID = ReadInt(d, ref p);
                if (t.EngineID == 0)
                {
                    t.EngineID = DefaultEngine;
                }
                p += 60;
                t.Health = ReadInt(d, ref p);
                p += LongRecords.Contains(t.ID) ? 37 : 30;
                result[t.ID] = t;
            }
        }

        /// <summary>
        /// UC size: 7 bits per byte, low bits first; the last byte has the high bit set.
        /// </summary>
        private static int ReadSize(byte[] d, ref int p)
        {
            int value = 0, shift = 0;
            while (true)
            {
                byte b = d[p++];
                value |= (b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) != 0)
                {
                    return value;
                }
            }
        }

        private static int ReadInt(byte[] d, ref int p)
        {
            int v = (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
            p += 4;
            return v;
        }

        private static string ReadString(byte[] d, ref int p)
        {
            int n = ReadSize(d, ref p) * 2;
            var s = Encoding.Unicode.GetString(d, p, n);
            p += n;
            return s;
        }
    }

    public class VehicleTemplate
    {
        public int ID { get; set; }
        public string Model { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public int EngineID { get; set; }
        public int Health { get; set; }

        /// <summary>
        /// The int32 at 75 bytes after the price: what the official attack results (0x800F) carried as their
        /// unknown "m" value for this target (GM/ZAKU/ACGUY 1000, ZOGOK/GOGG 1250, DOM 1400, RX-79G 1700).
        /// </summary>
        public int CombatValue { get; set; }
    }
}
