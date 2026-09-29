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
    /// 4 bytes, uint32 BE price, 116 bytes, uint32 BE engine id (0 = none), 4 bytes, UC size modifications
    /// (7 bytes each), 1 byte, UC size lists (UC size int32s each), 12 bytes (speed, hover speed, -1),
    /// uint32 BE health, then 30 bytes (wreckage id and others). Full layout: client-data-csv VEHICLE_TEMPLATES.
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
                p += 4;
                // Modifications: UC size, 7 bytes each (int16, byte, int32). Most vehicles have one; GUNTANK,
                // GOUF, GIGAN, ZAKUTANK and a few others have two.
                int mods = ReadSize(d, ref p);
                p += mods * 7 + 1;
                // Two resistance lists of int32.
                int lists = ReadSize(d, ref p);
                for (int k = 0; k < lists; k++)
                {
                    int values = ReadSize(d, ref p);
                    p += values * 4;
                }
                p += 12;
                t.Health = ReadInt(d, ref p);
                p += 30;
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

    /// <summary>
    /// Engine names from DB/Templates/engine.dat, the text file the Java server read ("ID: 0x046CD0",
    /// "Code: ...", "Name: MS/MA thermonuclear rocket engine typeA lv.1", ...). Engines are not in the
    /// *TEMPLATE.DAT files, so <see cref="ItemTemplates"/> does not know them.
    /// </summary>
    public static class EngineTemplates
    {
        private static readonly object loadLock = new object();
        private static Dictionary<int, string> names;

        public static string Name(int id)
        {
            string name;
            return Names.TryGetValue(id, out name) ? name : null;
        }

        /// <summary>
        /// An engine by id, or by name (exact, else the first containing it); 0 when there is none.
        /// </summary>
        public static int Find(string idOrName)
        {
            int id;
            if (int.TryParse(idOrName, out id))
            {
                return Names.ContainsKey(id) ? id : 0;
            }
            var all = Names;
            foreach (var e in all)
            {
                if (string.Equals(e.Value, idOrName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return e.Key;
                }
            }
            foreach (var e in all)
            {
                if (e.Value.IndexOf(idOrName.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return e.Key;
                }
            }
            return 0;
        }

        private static Dictionary<int, string> Names
        {
            get
            {
                if (names != null)
                {
                    return names;
                }
                lock (loadLock)
                {
                    if (names == null)
                    {
                        var result = new Dictionary<int, string>();
                        try
                        {
                            int id = 0;
                            foreach (var line in File.ReadAllLines(CharacterData.FindFile("Templates", "engine.dat")))
                            {
                                if (line.StartsWith("ID:"))
                                {
                                    id = Convert.ToInt32(line.Substring(3).Trim(), 16);
                                }
                                else if (line.StartsWith("Name:") && id > 0)
                                {
                                    result[id] = line.Substring(5).Trim();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.ShowWarning("Could not load engine.dat: " + ex.Message);
                        }
                        names = result;
                    }
                    return names;
                }
            }
        }
    }
}
