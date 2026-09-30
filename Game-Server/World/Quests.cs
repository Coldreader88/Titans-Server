using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The NPC item quests from the client's QUESTLIST.DAT (DB/Templates): hand an NPC the items it asks for and
    /// get money, sometimes an item, and for the town combat quests promotion points. The client decides alone
    /// which NPC offers which quest (LOGINSCHEDULELIST) and whether the player may take it, and it sends the
    /// hand-in (0x3E) without checking the items, so the server checks everything here.
    ///
    /// <code>
    /// uint32 BE 0, UC size n, then per quest:
    /// uint32 BE id, UC string name,
    /// UC size + (uint32 BE template, uint32 BE amount)[]  items to hand in
    /// UC size + (uint32 BE template, uint32 BE amount)[]  rewards (500000 = money)
    /// byte bonus kind, uint32 BE bonus amount, uint32 BE bonus param  (never read by the client)
    /// UC size + (uint32 BE total skill min, int32 BE total skill max (-1 none), byte rank min, byte rank max (255 none))[]
    /// </code>
    /// Layout and the offer rule from the client (reader 0x8214c0, limits 0x82a888, check 0x82474c).
    /// </summary>
    public static class Quests
    {
        private static Dictionary<int, Quest> quests;
        private static readonly object loadLock = new object();

        public static Quest Get(int id)
        {
            Quest q;
            return All.TryGetValue(id, out q) ? q : null;
        }

        public static Dictionary<int, Quest> All
        {
            get
            {
                if (quests == null)
                {
                    lock (loadLock)
                    {
                        if (quests == null)
                        {
                            quests = Load();
                        }
                    }
                }
                return quests;
            }
        }

        private static Dictionary<int, Quest> Load()
        {
            var result = new Dictionary<int, Quest>();
            try
            {
                var r = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "QUESTLIST.DAT")), 4);
                int count = r.Size();
                for (int i = 0; i < count; i++)
                {
                    var q = new Quest { ID = r.Int(), Name = r.String() };
                    for (int n = r.Size(); n > 0; n--)
                    {
                        q.Required.Add(new KeyValuePair<int, int>(r.Int(), r.Int()));
                    }
                    for (int n = r.Size(); n > 0; n--)
                    {
                        q.Rewards.Add(new KeyValuePair<int, int>(r.Int(), r.Int()));
                    }
                    r.Byte();
                    q.Bonus = r.Int();
                    r.Int();
                    q.SkillMin = 0;
                    q.SkillMax = -1;
                    q.RankMin = 0;
                    q.RankMax = 255;
                    for (int n = r.Size(); n > 0; n--)
                    {
                        q.SkillMin = r.Int();
                        q.SkillMax = r.Int();
                        q.RankMin = r.Byte();
                        q.RankMax = r.Byte();
                    }
                    if (q.Required.Count > 0)
                    {
                        result[q.ID] = q;
                    }
                }
                Logger.ShowInfo(string.Format("Loaded {0} quests.", result.Count));
            }
            catch (Exception ex)
            {
                Logger.ShowWarning("Cannot read QUESTLIST.DAT, no quests: " + ex.Message);
            }
            return result;
        }
    }

    public class Quest
    {
        public Quest()
        {
            Required = new List<KeyValuePair<int, int>>();
            Rewards = new List<KeyValuePair<int, int>>();
        }

        public int ID { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Template and amount of each item to hand in (a transport quest lists the same template once per piece).
        /// </summary>
        public List<KeyValuePair<int, int>> Required { get; private set; }

        /// <summary>
        /// Template and amount of each reward; 500000 is money.
        /// </summary>
        public List<KeyValuePair<int, int>> Rewards { get; private set; }

        /// <summary>
        /// The table's bonus amount: 5 to 100 on the town combat quests, 0 elsewhere. The client never reads it;
        /// the server gives it as promotion points (our reading).
        /// </summary>
        public int Bonus { get; set; }

        /// <summary>
        /// Bounds on the sum of all skills in tenths (-1 = no max) and on the rank (255 = no max).
        /// </summary>
        public int SkillMin { get; set; }
        public int SkillMax { get; set; }
        public int RankMin { get; set; }
        public int RankMax { get; set; }

        public int Money
        {
            get { return Rewards.Where(r => r.Key == PlayerContainers.Money).Sum(r => r.Value); }
        }

        public KeyValuePair<int, int>? Item
        {
            get
            {
                foreach (var r in Rewards)
                {
                    if (r.Key != PlayerContainers.Money)
                    {
                        return r;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Why the character may not take the quest (the client's offer rule), or null.
        /// </summary>
        public string Refusal(Character c)
        {
            int total = SkillGrowth.Total(c);
            if (SkillMax >= 0 && total > SkillMax)
            {
                return string.Format("your skills total {0:0.0}, more than its {1:0.0}", total / 10.0, SkillMax / 10.0);
            }
            if (SkillMin > 0 && total < SkillMin)
            {
                return string.Format("your skills total {0:0.0}, less than its {1:0.0}", total / 10.0, SkillMin / 10.0);
            }
            if ((sbyte)RankMax >= 0 && c.Rank > RankMax)
            {
                return "your rank is above what it allows";
            }
            if (RankMin > 0 && c.Rank < RankMin)
            {
                return "your rank is too low for it";
            }
            return null;
        }
    }

    /// <summary>
    /// An item a quest hand-in used: how much was taken and whether it is gone.
    /// </summary>
    public class QuestUse
    {
        public QuestUse(ItemNode item, int taken, bool gone)
        {
            Item = item;
            Taken = taken;
            Gone = gone;
        }

        public ItemNode Item { get; private set; }
        public int Taken { get; private set; }
        public bool Gone { get; private set; }
    }
}
