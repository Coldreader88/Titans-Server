using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Vehicle templates from every vehicle file in DB/Templates (MS, MA, tanks, fighters, cars, battleships
    /// and their Event twins; they share one record layout): name, price, engine, health and which weapons
    /// fit (<see cref="VehicleTemplate.EquipGroup"/>).
    ///
    /// <code>
    /// uint32 BE (skipped), UC size = number of templates, then per template:
    /// uint32 BE id, uint32 BE 0, 3 x UC size + UTF-16LE (comment, model code, name),
    /// 4 bytes, uint32 BE price, 116 bytes, uint32 BE engine id (0 = none), 4 bytes, UC size required skills
    /// (7 bytes each), 1 byte, UC size lists (UC size int32s each), 12 bytes (speed, hover speed, -1),
    /// uint32 BE health, then 30 bytes: uint32 wreckage id, 1 byte, 3 x uint32, byte cargo slots, uint32
    /// improvement id, uint32 mining ability, uint32 equipment group. Full layout: client-data-csv
    /// STRUCTURES.md (vehicle part).
    /// </code>
    /// Java reference: mina_common template/MSTemplate.java (same skips); checked to parse all nine files to
    /// their last byte.
    /// </summary>
    public static class VehicleTemplates
    {
        public const int DefaultHealth = 2000;

        /// <summary>
        /// Engine the Java server gave vehicles whose template names none (Engine.DEFAULT_ID).
        /// </summary>
        public const int DefaultEngine = 290012;

        private static readonly string[] Files =
        {
            "MSTEMPLATE.DAT", "TANKTEMPLATE.DAT", "MATEMPLATE.DAT", "FIGHTERTEMPLATE.DAT", "CARTEMPLATE.DAT",
            "BATTLESHIPTEMPLATE.DAT", "EVENTMSTEMPLATE.DAT", "EVENTMATEMPLATE.DAT", "EVENTTANKTEMPLATE.DAT",
            "EVENTFIGHTERTEMPLATE.DAT", "EVENTCARTEMPLATE.DAT",
        };

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
                foreach (var file in Files)
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
                if (t.EngineID < 290000 || t.EngineID >= 300000)
                {
                    t.EngineID = DefaultEngine;
                }
                p += 4;
                // Required skills: UC size, 7 bytes each (byte skill table, int16 skill index, int32 level x10).
                // Most vehicles have one; GUNTANK, GOUF, GIGAN, ZAKUTANK and a few others have two, cars none.
                int mods = ReadSize(d, ref p);
                for (int k = 0; k < mods; k++, p += 7)
                {
                    t.RequiredSkills.Add(new RequiredSkill
                    {
                        Table = d[p],
                        Index = (short)((d[p + 1] << 8) | d[p + 2]),
                        Level = (d[p + 3] << 24) | (d[p + 4] << 16) | (d[p + 5] << 8) | d[p + 6],
                    });
                }
                p += 1;
                // Two resistance lists of int32.
                int lists = ReadSize(d, ref p);
                for (int k = 0; k < lists; k++)
                {
                    int values = ReadSize(d, ref p);
                    p += values * 4;
                }
                p += 12;
                t.Health = ReadInt(d, ref p);
                if (t.Health <= 0)
                {
                    // Freighters, cargo boats, most battleships and some event units have 0 (the client never reads it).
                    t.Health = DefaultHealth;
                }
                t.CargoSlots = d[p + 17];
                int mi = p + 18;
                t.ModelIndex = ReadInt(d, ref mi);
                int g = p + 26;
                t.EquipGroup = ReadInt(d, ref g);
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
        public VehicleTemplate()
        {
            RequiredSkills = new List<RequiredSkill>();
        }

        public int ID { get; set; }

        /// <summary>
        /// The skills the client wants for driving it at full speed (it checks the first two): the GM Mobile Suit,
        /// the Ball Mobile Armor, tanks, the hover truck and planes Fighter, the Gundam Mobile Suit 30.0.
        /// </summary>
        public List<RequiredSkill> RequiredSkills { get; private set; }
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

        /// <summary>
        /// The VEHICLEEQUIPMENTTEMPLATE record saying which weapons and shields fit each armament slot
        /// (see <see cref="VehicleEquipment"/>); -1 for vehicles that carry none (cars, battleships).
        /// </summary>
        public int EquipGroup { get; set; }

        /// <summary>
        /// Item slots in the vehicle's cargo (MS 8, cars 16-20, 0 = no cargo), as the client shows them.
        /// </summary>
        public int CargoSlots { get; set; }

        /// <summary>
        /// The vehicle's upgrade table (<see cref="Improvements"/>); -1 when it has none.
        /// </summary>
        public int ModelIndex { get; set; }
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

    /// <summary>
    /// A skill a vehicle requires: the client's skill table (0 combat) and index (<see cref="SkillTables"/>) and
    /// the level x10.
    /// </summary>
    public class RequiredSkill
    {
        public int Table { get; set; }
        public int Index { get; set; }
        public int Level { get; set; }

        public Skill? Skill
        {
            get { return SkillTables.Get(Table, Index); }
        }
    }
}
