using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Names and shop prices of the items, read from the client's template files (DB/Templates/*TEMPLATE.DAT).
    ///
    /// Every binary template file lists its records after a 4-byte header and a UC size count, and every record
    /// starts the same way: int32 BE id, int32 BE sub-type (<see cref="ItemTemplate.Kind"/>), three
    /// UC-size-prefixed UTF-16 strings (comment, code, name), 4 bytes, int32 BE price. What follows differs per
    /// file (see <see cref="VehicleTemplates"/> for the MS layout), so the next record is found by looking for
    /// the next id of the same range followed by valid strings. That finds exactly the header's count in every
    /// file; the prices match what the official server charged (75mm machine gun 7500 in 75mm_MG.pcap, alumina
    /// 250 in Alumina_1(ZSSAEO3).pcap).
    ///
    /// A price of 100000000 marks items the shops do not sell. CLOTHESTEMPLATE.DAT is a text file without
    /// prices; clothes and yarn (24xxxx) are made by players, not bought.
    /// </summary>
    public static class ItemTemplates
    {
        public const int NotForSale = 100000000;

        /// <summary>
        /// Template files whose items are vehicles (format 0x14, bought into the hangar or ridden away).
        /// </summary>
        private static readonly HashSet<string> VehicleFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MSTEMPLATE.DAT", "TANKTEMPLATE.DAT", "CARTEMPLATE.DAT", "FIGHTERTEMPLATE.DAT", "MATEMPLATE.DAT",
            "BATTLESHIPTEMPLATE.DAT", "EVENTMSTEMPLATE.DAT", "EVENTTANKTEMPLATE.DAT", "EVENTCARTEMPLATE.DAT",
            "EVENTFIGHTERTEMPLATE.DAT", "EVENTMATEMPLATE.DAT",
        };

        private static readonly string[] Files =
        {
            "AMMUNITIONTEMPLATE.DAT", "BATTLESHIPTEMPLATE.DAT", "CAMPTEMPLATE.DAT", "CARTEMPLATE.DAT",
            "DEVELOPMENTTOOLTEMPLATE.DAT", "EVENTCARTEMPLATE.DAT", "EVENTFIGHTERTEMPLATE.DAT", "EVENTITEMTEMPLATE.DAT",
            "EVENTMSTEMPLATE.DAT", "EVENTTANKTEMPLATE.DAT", "FIGHTERTEMPLATE.DAT", "FUELTEMPLATE.DAT", "MATEMPLATE.DAT",
            "MSTEMPLATE.DAT", "RAWMATERIALTEMPLATE.DAT", "SHIELDTEMPLATE.DAT", "TANKTEMPLATE.DAT", "TARGETTEMPLATE.DAT",
            "WEAPONTEMPLATE.DAT", "ENGINETEMPLATE.DAT", "ORETEMPLATE.DAT", "FARMPRODUCTTEMPLATE.DAT",
            "STOCKFARMPRODUCTTEMPLATE.DAT", "MARINEPRODUCTTEMPLATE.DAT", "DYETEMPLATE.DAT", "WRECKAGETEMPLATE.DAT",
            "EVENTMATEMPLATE.DAT",
        };

        private static readonly object loadLock = new object();
        private static Dictionary<int, ItemTemplate> templates;

        public static ItemTemplate Get(int id)
        {
            EnsureLoaded();
            ItemTemplate t;
            return templates.TryGetValue(id, out t) ? t : null;
        }

        /// <summary>
        /// The template named <paramref name="name"/> (any case), else the shortest name containing it; null
        /// when none does (for #spawn name).
        /// </summary>
        /// <summary>
        /// The engine a new vehicle of this template gets (<see cref="VehicleTemplates.DefaultEngine"/> when
        /// its template names none).
        /// </summary>
        public static int EngineOf(int templateID)
        {
            var t = Get(templateID);
            return t != null && t.EngineID > 0 ? t.EngineID : VehicleTemplates.DefaultEngine;
        }

        public static ItemTemplate Find(string name)
        {
            EnsureLoaded();
            name = name.Trim();
            var exact = templates.Values.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            return exact ?? templates.Values.Where(t => t.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(t => t.Name.Length).ThenBy(t => t.ID).FirstOrDefault();
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return templates.Count;
            }
        }

        internal static void EnsureLoaded()
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

                var result = new Dictionary<int, ItemTemplate>();
                foreach (var file in Files)
                {
                    try
                    {
                        Load(File.ReadAllBytes(CharacterData.FindFile("Templates", file)), VehicleFiles.Contains(file),
                            CategoryOf(file), result);
                    }
                    catch (Exception ex)
                    {
                        Logger.ShowWarning(string.Format("Could not load {0}: {1}", file, ex.Message));
                    }
                }

                try
                {
                    // Town and mine props (56xxxx): one table split over three files, read as one.
                    var parts = new List<byte>();
                    for (int i = 1; i <= 3; i++)
                    {
                        parts.AddRange(File.ReadAllBytes(CharacterData.FindFile("Templates", "BACKGROUNDITEMTEMPLATE_" + i + ".DAT")));
                    }
                    Load(parts.ToArray(), false, "background", result);
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load BACKGROUNDITEMTEMPLATE_1-3.DAT: " + ex.Message);
                }
                try
                {
                    LoadClothes(File.ReadAllLines(CharacterData.FindFile("Templates", "CLOTHESTEMPLATE.DAT")), result);
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load CLOTHESTEMPLATE.DAT: " + ex.Message);
                }
                foreach (var link in IdLinks.All)
                {
                    // Models with no template of their own: spawnable by id, named after their model file.
                    if (!result.ContainsKey(link.ID))
                    {
                        result[link.ID] = new ItemTemplate
                        {
                            ID = link.ID, Name = link.ShortName, Price = NotForSale, IsVehicle = link.IsVehicle, Category = "idlink",
                        };
                    }
                }

                Logger.ShowInfo(string.Format("Loaded {0} item templates.", result.Count));
                templates = result;
            }
        }

        /// <summary>
        /// The category #items lists a template file under: "WEAPONTEMPLATE.DAT" is "weapon",
        /// "EVENTMSTEMPLATE.DAT" is "eventms".
        /// </summary>
        private static string CategoryOf(string file)
        {
            var name = file.Substring(0, file.IndexOf("TEMPLATE", StringComparison.OrdinalIgnoreCase)).ToLowerInvariant();
            switch (name)
            {
                case "ammunition": return "ammo";
                case "developmenttool": return "tool";
                case "rawmaterial": return "material";
                case "farmproduct": return "farm";
                case "stockfarmproduct": return "stockfarm";
                case "marineproduct": return "marine";
                default: return name;
            }
        }

        /// <summary>
        /// All templates of a category (see <see cref="CategoryOf"/>), by id.
        /// </summary>
        public static List<ItemTemplate> InCategory(string category)
        {
            EnsureLoaded();
            return templates.Values.Where(t => category == "all" || t.Category == category).OrderBy(t => t.ID).ToList();
        }

        /// <summary>
        /// The categories and how many templates each has.
        /// </summary>
        public static List<KeyValuePair<string, int>> Categories()
        {
            EnsureLoaded();
            return templates.Values.GroupBy(t => t.Category).OrderBy(g => g.Key)
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count())).ToList();
        }

        /// <summary>
        /// CLOTHESTEMPLATE.DAT is text: blocks of "ID: 240000", "Comment: ...", "Code: ...", "Name: cotton yarn".
        /// Clothes are not sold, so they have no price.
        /// </summary>
        private static void LoadClothes(string[] lines, Dictionary<int, ItemTemplate> result)
        {
            int id = 0;
            string code = null;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                int n;
                if (line.StartsWith("ID:") && int.TryParse(line.Substring(3).Trim(), out n))
                {
                    id = n;
                    code = null;
                }
                else if (line.StartsWith("Code:"))
                {
                    code = line.Substring(5).Trim();
                }
                else if (line.StartsWith("Name:") && id > 0 && !result.ContainsKey(id))
                {
                    var name = line.Substring(5).Trim();
                    result[id] = new ItemTemplate { ID = id, Name = name.Length > 0 ? name : code ?? id.ToString(), Price = NotForSale, Category = "clothes" };
                    id = 0;
                }
            }
        }

        private static void Load(byte[] d, bool vehicles, string category, Dictionary<int, ItemTemplate> result)
        {
            int p = 4;
            int count = ReadSize(d, ref p);
            int range = -1;
            for (int i = 0; i < count && p < d.Length; i++)
            {
                ItemTemplate t = null;
                while (p < d.Length - 8 && (t = TryRead(d, ref p, range)) == null)
                {
                    p++;
                }
                if (t == null)
                {
                    break;
                }
                range = t.ID / 10000;
                t.IsVehicle = vehicles;
                t.Category = category;
                ReadEquipmentStats(d, p, t);
                if (vehicles && p + 120 <= d.Length)
                {
                    int q = p + 116;
                    int engine = ReadInt(d, ref q);
                    t.EngineID = engine >= 290000 && engine < 300000 ? engine : 0;
                }
                result[t.ID] = t;
            }
        }

        /// <summary>
        /// Weapons (28xxxx) and shields (36xxxx): int32 BE values at fixed offsets after the price, found by
        /// matching the 0x8016 descriptions in the captures (75mm machine gun: durability 400, power 35,
        /// 22, range 500, 100 rounds; beam gun 280019: 400, 110, 18, 700, 160 rounds; shields: durability).
        /// </summary>
        private static void ReadEquipmentStats(byte[] d, int afterPrice, ItemTemplate t)
        {
            int range = t.ID / 10000;
            if (range == 33 && afterPrice + 45 <= d.Length)
            {
                // TARGETTEMPLATE (breakable rocks and temporary plants): int32 max health, byte faction (0 none,
                // 1 EF, 2 Zeon; attacking your own side's plant is a crime).
                int q = afterPrice + 40;
                t.TargetHealth = ReadInt(d, ref q);
                t.TargetFaction = d[afterPrice + 44];
                return;
            }
            if (range == 29 && afterPrice + 44 <= d.Length)
            {
                // Engines: avoid_rate, the engine's evasion bonus ("EngineTempAvoidRate" in the client's battle
                // info dump), is the first value after the common part.
                int e = afterPrice + 40;
                t.AvoidRate = ReadInt(d, ref e);
                return;
            }
            if ((range != 28 && range != 36) || afterPrice + 89 > d.Length)
            {
                return;
            }
            int p;
            p = afterPrice + 45; t.Durability = ReadInt(d, ref p);
            if (afterPrice + 95 <= d.Length)
            {
                // Two skill references after the magazine: (byte group, int16 BE index), the operation skill
                // (shooting, sniping, CQB, hand to hand; defence on shields) and the weapon type skill (beam or
                // shell). Group 0 is the combat table; 255 / -1 is none.
                t.OperationSkill = CombatSkill(d, afterPrice + 89);
                t.TypeSkill = CombatSkill(d, afterPrice + 92);
            }
            if (afterPrice + 157 <= d.Length && d[afterPrice + 95] == 0x87 && d[afterPrice + 124] == 0x87)
            {
                // Seven damage and seven hit-rate multipliers by distance band ("WeaponAttackRatioOnDistance"
                // and "WeaponHitRatioOnDistance" in the client's battle info dump), then the hit rate in 1/10000.
                t.AttackRatios = ReadFloats(d, afterPrice + 96, 7);
                t.HitRatios = ReadFloats(d, afterPrice + 125, 7);
                p = afterPrice + 153; t.HitRate = ReadInt(d, ref p);
                // Four aim limits, then the special attacks (UC size 3 + int16 BE SPECIALATTACKTEMPLATE ids, -1 none).
                if (afterPrice + 168 <= d.Length && d[afterPrice + 161] == 0x83)
                {
                    t.SpecialAttackIDs = new short[3];
                    for (int i = 0; i < 3; i++)
                    {
                        t.SpecialAttackIDs[i] = (short)((d[afterPrice + 162 + 2 * i] << 8) | d[afterPrice + 163 + 2 * i]);
                    }
                }
            }
            if (range == 36)
            {
                // Shields: the chance in 1/10000 that a hit lands on the shield.
                if (afterPrice + 177 <= d.Length)
                {
                    p = afterPrice + 173; t.GuardProbability = ReadInt(d, ref p);
                }
                return;
            }
            p = afterPrice + 41; t.Range = ReadInt(d, ref p);
            p = afterPrice + 49; t.Power = ReadInt(d, ref p);
            p = afterPrice + 57; t.Rate = ReadInt(d, ref p);
            p = afterPrice + 73; t.AmmoID = ReadInt(d, ref p);
            p = afterPrice + 77; t.AmmoPerShot = ReadInt(d, ref p);
            p = afterPrice + 85; t.Magazine = ReadInt(d, ref p);
        }

        private static Skill? CombatSkill(byte[] d, int p)
        {
            if (d[p] != 0)
            {
                return null;
            }
            return SkillTables.Get(0, (short)((d[p + 1] << 8) | d[p + 2]));
        }

        /// <summary>
        /// Reads a record's common start at <paramref name="p"/>, moving past its price; null (and
        /// <paramref name="p"/> unchanged) when there is none there.
        /// </summary>
        private static ItemTemplate TryRead(byte[] d, ref int p, int range)
        {
            int q = p;
            try
            {
                int id = ReadInt(d, ref q);
                if (id < 100000 || id >= 3000000 || (range >= 0 && id / 10000 != range))
                {
                    return null;
                }
                int kind = ReadInt(d, ref q);
                string comment, code, name;
                if (!TryReadString(d, ref q, out comment) || !TryReadString(d, ref q, out code) ||
                    !TryReadString(d, ref q, out name) || name.Length == 0)
                {
                    return null;
                }
                q += 4;
                int price = ReadInt(d, ref q);
                p = q;
                // The price is an int64 BE; the sell-back price (another int64) follows it.
                int sellPrice = q + 8 <= d.Length ? (d[q + 4] << 24) | (d[q + 5] << 16) | (d[q + 6] << 8) | d[q + 7] : 0;
                return new ItemTemplate { ID = id, Kind = kind, Name = name.Trim(), Price = price, SellPrice = sellPrice };
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool TryReadString(byte[] d, ref int p, out string s)
        {
            s = null;
            int n = ReadSize(d, ref p);
            if (n < 0 || n > 200 || p + n * 2 > d.Length)
            {
                return false;
            }
            s = Encoding.Unicode.GetString(d, p, n * 2);
            p += n * 2;
            foreach (var c in s)
            {
                if (c < ' ')
                {
                    return false;
                }
            }
            return true;
        }

        private static int ReadSize(byte[] d, ref int p)
        {
            int value = 0, shift = 0;
            while (shift <= 28)
            {
                byte b = d[p++];
                value |= (b & 0x7F) << shift;
                shift += 7;
                if ((b & 0x80) != 0)
                {
                    return value;
                }
            }
            return -1;
        }

        private static float[] ReadFloats(byte[] d, int p, int count)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++)
            {
                int bits = ReadInt(d, ref p);
                values[i] = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }
            return values;
        }

        private static int ReadInt(byte[] d, ref int p)
        {
            int v = (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
            p += 4;
            return v;
        }
    }

    public class ItemTemplate
    {
        public int ID { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// The int32 BE after the id: a sub-type the production tables match ingredients on. Engines: 1 MS/MA
        /// rocket, 2 tank, 5 hydro jet, 7 general vehicle, 8 MS/MA jet (a Zaku II F2 takes a 1 or an 8).
        /// </summary>
        public int Kind { get; set; }

        /// <summary>
        /// The shop price of one; <see cref="ItemTemplates.NotForSale"/> for items the shops do not sell.
        /// </summary>
        public int Price { get; set; }

        /// <summary>
        /// What a shop pays for one (the int64 after the price). The official shops paid exactly this (RGM-79
        /// 145800 in Sold_RGM-79.pcap, alumina 215 in Alumina_(121).pcap).
        /// </summary>
        public int SellPrice { get; set; }

        public bool IsVehicle { get; set; }

        /// <summary>
        /// The template file it comes from, for #items: weapon, shield, ammo, ms, tank, car, ...
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Weapons and shields (see ItemTemplates.ReadEquipmentStats): durability; weapons also range, power
        /// (the third value of the weapon's stats list), a rate-like value and rounds per magazine (0 for melee).
        /// </summary>
        /// <summary>
        /// A vehicle's engine, from the int32 BE 116 bytes after the price in every vehicle template file
        /// (CARTEMPLATE.DAT: elecars 290008, MIDIA and FAT-UNCLE 290231 "Thermonuclear jet engine",
        /// FREIGHTER and the space cargoboats 290232 "Thermonuclear rocket engine"); 0 when it names none.
        /// </summary>
        public int EngineID { get; set; }

        public int Durability { get; set; }
        public int Range { get; set; }
        public int Power { get; set; }
        /// <summary>
        /// WEAPONTEMPLATE eq_unk4 (10 to 38; Java called it damage_variation). The client does not read it;
        /// <see cref="Combat"/> uses it for how far damage varies.
        /// </summary>
        public int Rate { get; set; }
        public int Magazine { get; set; }

        /// <summary>
        /// Rounds one attack uses (77 bytes after the price; the official 100mm machine gun fired 6, a beam
        /// rifle 8); 0 for melee weapons.
        /// </summary>
        public int AmmoPerShot { get; set; }

        /// <summary>
        /// Damage and hit-rate multipliers for seven distance bands, nearest first; null when the template has
        /// none.
        /// </summary>
        public float[] AttackRatios { get; set; }
        public float[] HitRatios { get; set; }

        /// <summary>
        /// A weapon's hit rate in 1/10000 (4000 to 8000).
        /// </summary>
        public int HitRate { get; set; }

        /// <summary>
        /// The melee special attacks the weapon can do (SPECIALATTACKTEMPLATE ids by combo tier, -1 none); null
        /// for weapons without the list.
        /// </summary>
        public short[] SpecialAttackIDs { get; set; }

        /// <summary>
        /// Breakable targets (33xxxx, TARGETTEMPLATE): full health, and the faction whose plant it is (0 none).
        /// </summary>
        public int TargetHealth { get; set; }
        public int TargetFaction { get; set; }
        public bool IsTarget { get { return ID / 10000 == 33; } }

        /// <summary>
        /// A shield's chance in 1/10000 to take a hit (2000 or 3000).
        /// </summary>
        public int GuardProbability { get; set; }

        /// <summary>
        /// An engine's evasion bonus (0 to 40, rising with the engine level).
        /// </summary>
        public int AvoidRate { get; set; }

        /// <summary>
        /// The ammunition (54xxxx) a weapon fires, from WEAPONTEMPLATE.DAT (73 bytes after the price); 0 or -1
        /// for none (melee weapons).
        /// </summary>
        public int AmmoID { get; set; }

        /// <summary>
        /// The combat skill that operates this weapon (shooting, sniping, CQB, hand to hand; defence for a shield),
        /// and the weapon type skill (beam or shell firing); null when it names none.
        /// </summary>
        public Skill? OperationSkill { get; set; }
        public Skill? TypeSkill { get; set; }

        public bool IsWeapon { get { return ID / 10000 == 28; } }
        public bool IsShield { get { return ID / 10000 == 36; } }

        public bool ForSale { get { return Price > 0 && Price < ItemTemplates.NotForSale; } }

        /// <summary>
        /// Whether bought items join a stack of the same item. Weapons (28xxxx), engines and parts (29xxxx) and
        /// shields (36xxxx) are single items, as the Java server treated them (BuyItem.read); so are vehicles.
        /// </summary>
        public bool Stacks
        {
            get
            {
                int range = ID / 10000;
                return !IsVehicle && range != 28 && range != 29 && range != 36 && !PlayerInventory.IsClothes(ID);
            }
        }
    }
}
