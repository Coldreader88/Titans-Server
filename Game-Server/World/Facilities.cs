using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The towns' facilities (banks, hangars, shops, factories), read from the client's TOWNFACILITYINFOTEMPLATE.DAT,
    /// and what each factory makes (FACTORYINFOTEMPLATE.DAT, FACTORYPRODUCTLIST.DAT). The client checks these
    /// before it lets a player use a facility (0x627708, 0x7e2d04) and builds each factory's menu from them
    /// (0x7e2d40); the server checks them again (specs/re-combat-chat-targets.md section 4).
    ///
    /// TOWNFACILITYINFOTEMPLATE.DAT, after the 4-byte header and a UC size count: int32 town, int32 facility index,
    /// int32 category (0 bank, 1 hangar, 2 shop, 3 factory, 5 repair, ...), int32 subcategory (the shop id or the
    /// factory id), UC string name, UC size 3 + one byte per faction (neutral, EF, Zeon: 1 = may use it), byte
    /// battle town facility (then the town's owner decides).
    /// FACTORYINFOTEMPLATE.DAT: int32 0, UC size count, then int32 3, int32 factory id, int32 type, UC size n (3),
    /// n + 1 lists of int32 product list ids: neutral, EF, Zeon, and the vehicles it takes apart or upgrades.
    /// FACTORYPRODUCTLIST.DAT: int32 0, UC size count, then UC string name, int32 category, list of int32
    /// templates; a list's id is its index.
    /// </summary>
    public static class Facilities
    {
        public const int CategoryBank = 0;
        public const int CategoryHangar = 1;
        public const int CategoryShop = 2;
        public const int CategoryFactory = 3;

        /// <summary>
        /// The factory's list slot for taking vehicles apart and upgrading them.
        /// </summary>
        public const int DismantleSlot = 3;

        private static readonly object loadLock = new object();
        private static Dictionary<long, Facility> facilities;
        private static Dictionary<int, List<HashSet<int>>> factories;

        public static Facility Get(int town, int index)
        {
            EnsureLoaded();
            Facility f;
            return facilities.TryGetValue(Key(town, index), out f) ? f : null;
        }

        /// <summary>
        /// A shop (by shop id) in a town; null when the town has none.
        /// </summary>
        public static Facility FindShop(int town, int shopID)
        {
            EnsureLoaded();
            return facilities.Values.FirstOrDefault(f => f.Town == town && f.Category == CategoryShop && f.SubCategory == shopID);
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return facilities.Count;
            }
        }

        /// <summary>
        /// Why a player of that faction may not use the facility (as the client's messages 346 and 347), or null.
        /// </summary>
        public static string Refusal(Facility f, Faction faction, bool criminal)
        {
            if (f == null)
            {
                return "there is no such facility";
            }
            if (criminal)
            {
                return "criminals cannot use the town's facilities";
            }
            if (f.BattleTown)
            {
                var city = Occupation.Get(f.Town);
                if (city != null && city.Owner != (ushort)faction)
                {
                    return "this facility belongs to the other side";
                }
                if (city != null)
                {
                    return null;
                }
            }
            if (!f.Factions.Any(b => b))
            {
                return "this facility is closed";
            }
            int slot = (int)faction;
            return slot < f.Factions.Length && f.Factions[slot] ? null : "this facility is not for your side";
        }

        /// <summary>
        /// Whether factory <paramref name="factoryID"/> offers the product in that list slot (1 EF, 2 Zeon,
        /// <see cref="DismantleSlot"/> for taking apart). False when the factory is unknown to the tables.
        /// </summary>
        public static bool Makes(int factoryID, int slot, int productID)
        {
            EnsureLoaded();
            List<HashSet<int>> lists;
            if (!factories.TryGetValue(factoryID, out lists))
            {
                return false;
            }
            return slot >= 0 && slot < lists.Count && lists[slot].Contains(productID);
        }

        private static long Key(int town, int index)
        {
            return ((long)town << 32) | (uint)index;
        }

        internal static void EnsureLoaded()
        {
            if (facilities != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (facilities != null)
                {
                    return;
                }
                var result = new Dictionary<long, Facility>();
                var made = new Dictionary<int, List<HashSet<int>>>();
                try
                {
                    var r = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "TOWNFACILITYINFOTEMPLATE.DAT")), 4);
                    for (int n = r.Size(); n > 0; n--)
                    {
                        var f = new Facility { Town = r.Int(), Index = r.Int(), Category = r.Int(), SubCategory = r.Int(), Name = r.String() };
                        int count = r.Size();
                        f.Factions = new bool[count];
                        for (int i = 0; i < count; i++)
                        {
                            f.Factions[i] = r.Byte() != 0;
                        }
                        f.BattleTown = r.Byte() != 0;
                        result[Key(f.Town, f.Index)] = f;
                    }

                    var lists = new List<List<int>>();
                    var g = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "FACTORYPRODUCTLIST.DAT")), 4);
                    for (int n = g.Size(); n > 0; n--)
                    {
                        g.String();
                        g.Int();
                        var ids = new List<int>();
                        for (int k = g.Size(); k > 0; k--)
                        {
                            ids.Add(g.Int());
                        }
                        lists.Add(ids);
                    }

                    var fr = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "FACTORYINFOTEMPLATE.DAT")), 4);
                    for (int n = fr.Size(); n > 0; n--)
                    {
                        fr.Int();
                        int id = fr.Int();
                        fr.Int();
                        int slots = fr.Size() + 1;
                        var sets = new List<HashSet<int>>();
                        for (int s = 0; s < slots; s++)
                        {
                            var set = new HashSet<int>();
                            for (int k = fr.Size(); k > 0; k--)
                            {
                                int list = fr.Int();
                                if (list >= 0 && list < lists.Count)
                                {
                                    set.UnionWith(lists[list]);
                                }
                            }
                            sets.Add(set);
                        }
                        made[id] = sets;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowError("Cannot read the town facilities and factories: " + ex.Message);
                }
                factories = made;
                facilities = result;
            }
        }
    }

    public class Facility
    {
        public int Town { get; set; }
        public int Index { get; set; }
        public int Category { get; set; }
        public int SubCategory { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Who may use it: neutral, EF, Zeon.
        /// </summary>
        public bool[] Factions { get; set; }

        /// <summary>
        /// A battle town's facility: the town's owner may use it.
        /// </summary>
        public bool BattleTown { get; set; }
    }
}
