using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Where the town NPCs who give each quest stand (DB/Npcs/quest_npcs.csv, made from the client's
    /// LOGINSCHEDULELIST.DAT, *_NPC.DAT and *_NPC.LST). The client picks the NPC and sends the hand-in (0x3E)
    /// without its id, so the server checks that the player is close to one of the quest's NPCs.
    /// </summary>
    public static class QuestGivers
    {
        private static readonly object loadLock = new object();
        private static Dictionary<int, List<Giver>> givers;

        public class Giver
        {
            public string Place { get; set; }
            public string Name { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }

        /// <summary>
        /// The NPCs who give the quest; empty when none is known (then the hand-in is not checked).
        /// </summary>
        public static List<Giver> Of(int questID)
        {
            EnsureLoaded();
            List<Giver> list;
            return givers.TryGetValue(questID, out list) ? list : new List<Giver>();
        }

        /// <summary>
        /// Whether one of the quest's NPCs is within <paramref name="distance"/> of (x, y); <paramref name="nearest"/>
        /// is the closest one. Also true, with <paramref name="known"/> false, when the quest has no known NPC.
        /// </summary>
        public static bool IsNear(int questID, int x, int y, int distance, out Giver nearest, out bool known)
        {
            var list = Of(questID);
            known = list.Count > 0;
            nearest = null;
            double best = double.MaxValue;
            foreach (var g in list)
            {
                double dx = (double)g.X - x, dy = (double)g.Y - y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < best)
                {
                    best = d;
                    nearest = g;
                }
            }
            return !known || best <= distance;
        }

        internal static void EnsureLoaded()
        {
            if (givers != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (givers != null)
                {
                    return;
                }
                var result = new Dictionary<int, List<Giver>>();
                try
                {
                    foreach (var raw in File.ReadAllLines(CharacterData.FindFile("Npcs", "quest_npcs.csv")))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line[0] == '#' || line.StartsWith("quest_id", StringComparison.Ordinal))
                        {
                            continue;
                        }
                        var f = line.Split(',');
                        int quest;
                        if (f.Length < 5 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out quest))
                        {
                            continue;
                        }
                        List<Giver> list;
                        if (!result.TryGetValue(quest, out list))
                        {
                            result[quest] = list = new List<Giver>();
                        }
                        list.Add(new Giver
                        {
                            Place = f[1],
                            Name = f[2],
                            X = int.Parse(f[3], CultureInfo.InvariantCulture),
                            Y = int.Parse(f[4], CultureInfo.InvariantCulture),
                        });
                    }
                    Logger.ShowInfo(string.Format("Loaded the NPCs of {0} quests.", result.Count));
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load quest_npcs.csv, quest hand-ins are not checked for an NPC nearby: " + ex.Message);
                    result.Clear();
                }
                givers = result;
            }
        }
    }
}
