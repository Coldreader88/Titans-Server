using System;
using System.Collections.Generic;
using System.IO;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The NPC shops, read from the client's SHOPINFOTEMPLATE.DAT and SHOPGOODSLIST.DAT: what each shop sells to
    /// each faction, what it buys back, and its price rates. The client sends the town and shop with every buy
    /// (0x21) and sell (0x22), and works out the prices it shows the same way (0x7f0ae4):
    /// <code>
    /// buy  = template price      x (town buy %  x shop b % x shop rank table[rank] % / 10000) / 100
    /// sell = template sell price x (town sell % x shop a % / 100) / 100
    /// </code>
    /// Every town's rates are 100 (TOWNINFOTEMPLATE.DAT), so they are left out. The rank tables are all 100 too;
    /// the only shops off 100% are 400-406, which serve both factions at 90%. The official shops charged and
    /// paid exactly the template prices at 100% (every buy and sell in the captures).
    ///
    /// SHOPGOODSLIST.DAT, after the 4-byte header and a UC size count: UC string tab name, int32 category, list of
    /// (int32 template, int32 colour variant start); a list's id is its index.
    /// SHOPINFOTEMPLATE.DAT: int32 2, int32 shop id, int32 shop type, then a list of three sections (neutral,
    /// EF, Zeon), each a list of int32 goods list ids, a list of int16 template families (id / 10000) it buys
    /// back, and a list of three rate entries (int16 sell %, int16 buy %, list of 16 int16 rank %).
    /// </summary>
    public static class Shops
    {
        private static readonly object loadLock = new object();
        private static Dictionary<int, Shop> shops;

        public static Shop Get(int shopID)
        {
            EnsureLoaded();
            Shop s;
            return shops.TryGetValue(shopID, out s) ? s : null;
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return shops.Count;
            }
        }

        internal static void EnsureLoaded()
        {
            if (shops != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (shops != null)
                {
                    return;
                }
                var result = new Dictionary<int, Shop>();
                try
                {
                    var goods = new List<List<int>>();
                    var g = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "SHOPGOODSLIST.DAT")), 4);
                    for (int n = g.Size(); n > 0; n--)
                    {
                        g.String();
                        g.Int();
                        var ids = new List<int>();
                        for (int k = g.Size(); k > 0; k--)
                        {
                            int id = g.Int();
                            g.Int();
                            if (id > 0)
                            {
                                ids.Add(id);
                            }
                        }
                        goods.Add(ids);
                    }

                    var r = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "SHOPINFOTEMPLATE.DAT")), 4);
                    for (int n = r.Size(); n > 0; n--)
                    {
                        r.Int();
                        var shop = new Shop { ID = r.Int(), Type = r.Int() };
                        int section = 0;
                        for (int k = r.Size(); k > 0; k--, section++)
                        {
                            var s = new ShopSection();
                            for (int i = r.Size(); i > 0; i--)
                            {
                                int list = r.Int();
                                if (list >= 0 && list < goods.Count)
                                {
                                    s.Goods.UnionWith(goods[list]);
                                }
                            }
                            for (int i = r.Size(); i > 0; i--)
                            {
                                s.BuysBack.Add(r.Short());
                            }
                            for (int i = r.Size(); i > 0; i--)
                            {
                                var rate = new ShopRate { SellPercent = r.Short(), BuyPercent = r.Short() };
                                for (int j = r.Size(); j > 0; j--)
                                {
                                    rate.RankPercent.Add(r.Short());
                                }
                                s.Rates.Add(rate);
                            }
                            if (section < shop.Sections.Length)
                            {
                                shop.Sections[section] = s;
                            }
                        }
                        result[shop.ID] = shop;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load the shops: " + ex.Message);
                }
                Logger.ShowInfo(string.Format("Loaded {0} shops.", result.Count));
                shops = result;
            }
        }
    }

    public class Shop
    {
        public int ID { get; set; }

        /// <summary>
        /// 1 vehicles, 2 weapons, 3 engines, 4 materials, 6 clothes, 7 toys, 8 camp, 9 stuff and dye.
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// By faction: 0 neutral (always empty), 1 EF, 2 Zeon.
        /// </summary>
        public ShopSection[] Sections { get; private set; }

        public Shop()
        {
            Sections = new[] { new ShopSection(), new ShopSection(), new ShopSection() };
        }

        private ShopSection SectionOf(Faction faction)
        {
            int i = (int)faction;
            return i >= 0 && i < Sections.Length ? Sections[i] : null;
        }

        private ShopRate RateOf(Faction faction)
        {
            var s = SectionOf(faction);
            int i = (int)faction;
            return s != null && i < s.Rates.Count ? s.Rates[i] : null;
        }

        public bool Sells(Faction faction, int templateID)
        {
            var s = SectionOf(faction);
            return s != null && s.Goods.Contains(templateID);
        }

        public bool BuysBack(Faction faction, int templateID)
        {
            var s = SectionOf(faction);
            return s != null && s.BuysBack.Contains(templateID / 10000);
        }

        /// <summary>
        /// What one costs a player of this faction and rank here.
        /// </summary>
        public long BuyPrice(Faction faction, int rank, int price)
        {
            var rate = RateOf(faction);
            if (rate == null)
            {
                return price;
            }
            int rankPercent = rank >= 0 && rank < rate.RankPercent.Count ? rate.RankPercent[rank] : 100;
            long percent = 100L * rate.BuyPercent * rankPercent / 10000;
            return price * percent / 100;
        }

        /// <summary>
        /// What the shop pays a player of this faction for one.
        /// </summary>
        public long SellPrice(Faction faction, int sellPrice)
        {
            var rate = RateOf(faction);
            if (rate == null)
            {
                return sellPrice;
            }
            long percent = 100L * rate.SellPercent / 100;
            return sellPrice * percent / 100;
        }
    }

    public class ShopSection
    {
        public HashSet<int> Goods { get; private set; }
        public HashSet<int> BuysBack { get; private set; }
        public List<ShopRate> Rates { get; private set; }

        public ShopSection()
        {
            Goods = new HashSet<int>();
            BuysBack = new HashSet<int>();
            Rates = new List<ShopRate>();
        }
    }

    public class ShopRate
    {
        public int SellPercent { get; set; }
        public int BuyPercent { get; set; }
        public List<int> RankPercent { get; private set; }

        public ShopRate()
        {
            RankPercent = new List<int>();
        }
    }
}
