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
    /// starts the same way: int32 BE id, 4 bytes, three UC-size-prefixed UTF-16 strings (comment, code, name),
    /// 4 bytes, int32 BE price. What follows differs per file (see <see cref="VehicleTemplates"/> for the MS
    /// layout), so the next record is found by looking for the next id of the same range followed by valid
    /// strings. That finds exactly the header's count in every file; the prices match what the official server
    /// charged (75mm machine gun 7500 in 75mm_MG.pcap, alumina 250 in Alumina_1(ZSSAEO3).pcap).
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
            "EVENTFIGHTERTEMPLATE.DAT",
        };

        private static readonly string[] Files =
        {
            "AMMUNITIONTEMPLATE.DAT", "BATTLESHIPTEMPLATE.DAT", "CAMPTEMPLATE.DAT", "CARTEMPLATE.DAT",
            "DEVELOPMENTTOOLTEMPLATE.DAT", "EVENTCARTEMPLATE.DAT", "EVENTFIGHTERTEMPLATE.DAT", "EVENTITEMTEMPLATE.DAT",
            "EVENTMSTEMPLATE.DAT", "EVENTTANKTEMPLATE.DAT", "FIGHTERTEMPLATE.DAT", "FUELTEMPLATE.DAT", "MATEMPLATE.DAT",
            "MSTEMPLATE.DAT", "RAWMATERIALTEMPLATE.DAT", "SHIELDTEMPLATE.DAT", "TANKTEMPLATE.DAT", "TARGETTEMPLATE.DAT",
            "WEAPONTEMPLATE.DAT",
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
        /// when none does (for #spawn::name).
        /// </summary>
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

                var result = new Dictionary<int, ItemTemplate>();
                foreach (var file in Files)
                {
                    try
                    {
                        Load(File.ReadAllBytes(CharacterData.FindFile("Templates", file)), VehicleFiles.Contains(file), result);
                    }
                    catch (Exception ex)
                    {
                        Logger.ShowWarning(string.Format("Could not load {0}: {1}", file, ex.Message));
                    }
                }

                Logger.ShowInfo(string.Format("Loaded {0} item templates.", result.Count));
                templates = result;
            }
        }

        private static void Load(byte[] d, bool vehicles, Dictionary<int, ItemTemplate> result)
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
                ReadEquipmentStats(d, p, t);
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
            if ((range != 28 && range != 36) || afterPrice + 89 > d.Length)
            {
                return;
            }
            int p;
            p = afterPrice + 45; t.Durability = ReadInt(d, ref p);
            if (range == 36)
            {
                return;
            }
            p = afterPrice + 41; t.Range = ReadInt(d, ref p);
            p = afterPrice + 49; t.Power = ReadInt(d, ref p);
            p = afterPrice + 57; t.Rate = ReadInt(d, ref p);
            p = afterPrice + 85; t.Magazine = ReadInt(d, ref p);
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
                q += 4;
                string comment, code, name;
                if (!TryReadString(d, ref q, out comment) || !TryReadString(d, ref q, out code) ||
                    !TryReadString(d, ref q, out name) || name.Length == 0)
                {
                    return null;
                }
                q += 4;
                int price = ReadInt(d, ref q);
                p = q;
                return new ItemTemplate { ID = id, Name = name.Trim(), Price = price };
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
        /// The shop price of one; <see cref="ItemTemplates.NotForSale"/> for items the shops do not sell.
        /// </summary>
        public int Price { get; set; }

        public bool IsVehicle { get; set; }

        /// <summary>
        /// Weapons and shields (see ItemTemplates.ReadEquipmentStats): durability; weapons also range, power
        /// (the third value of the weapon's stats list), a rate-like value and rounds per magazine (0 for melee).
        /// </summary>
        public int Durability { get; set; }
        public int Range { get; set; }
        public int Power { get; set; }
        public int Rate { get; set; }
        public int Magazine { get; set; }

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
                return !IsVehicle && range != 28 && range != 29 && range != 36;
            }
        }
    }
}
