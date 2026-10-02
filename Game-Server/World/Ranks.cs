using System;
using System.Linq;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Military rank, 0 (Seaman Recruit) to 15 (Admiral), the client's RANKNAMEINFO. The client only keeps the
    /// rank: it has no points and no promotion table, and it learns of a promotion from 0x8034's rank change
    /// (message "promoted to %s"). How the official server promoted players is not known, so this is our rule:
    /// players earn promotion points (quest bonuses, destroying enemies) and reach each rank at a fixed total
    /// (GameServer.xml RankPoints). Nobody is ever demoted.
    /// </summary>
    public static class Ranks
    {
        public const int MaxRank = 15;

        /// <summary>
        /// RANKNAMEINFO.DAT.
        /// </summary>
        public static readonly string[] Names =
        {
            "Seaman Recruit", "Seaman Apprentice", "Seaman", "Petty Officer", "Chief Petty Officer", "Senior Chief Petty Officer",
            "Ensign", "Lieutenant Junior Grade", "Lieutenant", "Lieutenant Commander", "Commander", "Captain",
            "Rear Admiral Lower Half", "Rear Admiral Upper Half", "Vice Admiral", "Admiral",
        };

        public static string Name(int rank)
        {
            return rank >= 0 && rank < Names.Length ? Names[rank] : "rank " + rank;
        }

        /// <summary>
        /// Points needed in total for ranks 1 to 15. The town combat quests give 5 (ranks 0 to 4), 20 (4 to 6), 50
        /// (6 to 8) and 100 (8 and up), so each band takes a few of its quests.
        /// </summary>
        public static int[] Thresholds = { 10, 25, 50, 100, 175, 275, 400, 550, 750, 1000, 1300, 1650, 2050, 2500, 3000 };

        /// <summary>
        /// Points for destroying an enemy player's machine and an enemy NPC (GameServer.xml RankPointsPlayerKill,
        /// RankPointsNpcKill; 0 = none).
        /// </summary>
        public static int PlayerKillPoints = 2;
        public static int NpcKillPoints = 1;

        /// <summary>
        /// The rank these points reach.
        /// </summary>
        public static int ForPoints(int points)
        {
            int rank = 0;
            while (rank < MaxRank && rank < Thresholds.Length && points >= Thresholds[rank])
            {
                rank++;
            }
            return rank;
        }

        /// <summary>
        /// The points a rank starts at.
        /// </summary>
        public static int PointsFor(int rank)
        {
            return rank <= 0 ? 0 : Thresholds[Math.Min(rank, Thresholds.Length) - 1];
        }

        public static bool TryParseThresholds(string text)
        {
            var parts = (text ?? string.Empty).Split(',');
            var values = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), out values[i]) || values[i] < 0 || (i > 0 && values[i] < values[i - 1]))
                {
                    return false;
                }
            }
            if (values.Length != MaxRank)
            {
                return false;
            }
            Thresholds = values;
            return true;
        }
    }
}
