using System;

namespace Common.Characters
{
    /// <summary>
    /// Where each skill sits in the client's four skill tables. The player info lists the tables in this order
    /// (see <see cref="PlayerInfoWriter"/>), and 0x8034, 0x0B and 0x0C name a skill by its table (the "type") and
    /// its place in that table (the "index"). Strength, spirit and luck are the three statuses, numbered 0 to 2.
    /// From the client (table sizes 21, 7, 10, 5 at 0xa79134) and the official 0x8034 captures.
    /// </summary>
    public static class SkillTables
    {
        public static readonly Skill?[] Combat =
        {
            Skill.MOBILE_SUIT, Skill.MOBILE_ARMOR, null, Skill.FIGHTER, Skill.SPACE_ENGAGEMENT, Skill.GROUND_ENGAGEMENT, null,
            Skill.AIR_ENGAGEMENT, Skill.BEAMCARTRIDGE_WEAPON, Skill.SHELLFIRING_WEAPON, null, Skill.WEAPON_MANIPULATION,
            Skill.SHOOTING, Skill.SNIPING, Skill.CQB, Skill.HANDTOHAND_COMBAT, Skill.TACTICS, Skill.AMBAC, Skill.DEFENCE,
            Skill.EVASION, Skill.EMERGENCY_REPAIR,
        };

        public static readonly Skill?[] Construction =
        {
            Skill.MINING, Skill.REFINERY, Skill.MSMA_CONSTRUCTION, Skill.BATTLESHIP_CONSTRUCTION, Skill.ARMS_CONSTRUCTION, null, null,
        };

        public static readonly Skill?[] Other =
        {
            null, null, null, null, null, Skill.CLOTHING_MANUFACTURING, null, null, null, null,
        };

        public static readonly Skill?[] Extra = { null, null, null, null, null };

        /// <summary>
        /// The tables by type: 0 combat, 1 construction, 2 other, 3 the five-entry table.
        /// </summary>
        public static readonly Skill?[][] Tables = { Combat, Construction, Other, Extra };

        /// <summary>
        /// The statuses by index.
        /// </summary>
        public static readonly Skill[] Statuses = { Skill.STRENGTH, Skill.SPIRIT, Skill.LUCK };

        public static bool IsStatus(Skill skill)
        {
            return skill == Skill.STRENGTH || skill == Skill.SPIRIT || skill == Skill.LUCK;
        }

        /// <summary>
        /// The skill at <paramref name="type"/>, <paramref name="index"/>; null for an unused or unknown place.
        /// </summary>
        public static Skill? Get(int type, int index)
        {
            if (type < 0 || type >= Tables.Length || index < 0 || index >= Tables[type].Length)
            {
                return null;
            }
            return Tables[type][index];
        }

        /// <summary>
        /// The table and place of a skill (false for the statuses).
        /// </summary>
        public static bool TryFind(Skill skill, out byte type, out byte index)
        {
            for (int t = 0; t < Tables.Length; t++)
            {
                int i = Array.IndexOf(Tables[t], (Skill?)skill);
                if (i >= 0)
                {
                    type = (byte)t;
                    index = (byte)i;
                    return true;
                }
            }
            type = 0;
            index = 0;
            return false;
        }
    }

    /// <summary>
    /// The arrow the player sets for each skill and status in the "Status Setting" window: the top four bits of
    /// every skill and status value in the player info, changed with 0x0B and 0x0C. The client shows 0 as its
    /// default and enforces nothing; what the arrows do is the server's rule (see the Game server's SkillGrowth):
    /// 0 may rise, 1 may fall to make room under the total cap and never rises, 2 stays as it is.
    /// </summary>
    public static class SkillManagement
    {
        public const byte Raise = 0;
        public const byte Lower = 1;
        public const byte Lock = 2;
    }

    /// <summary>
    /// The ten counters of the player info's score list (and, six of them, the profile window). The client
    /// counts kills itself from the attack results; the criminal count is also run down by the client (0x08).
    /// </summary>
    public static class ScoreSlot
    {
        public const int Count = 10;

        public const int EnemyNpcKills = 0;
        public const int DeathsByEnemyNpc = 1;
        public const int FriendlyNpcKills = 2;
        public const int DeathsByFriendlyNpc = 3;
        public const int CriminalCount = 4;
        public const int PreviousOffense = 5;
        public const int EnemyPlayerKills = 6;
        public const int DeathsByEnemyPlayer = 7;
        public const int FriendlyPlayerKills = 8;
        public const int DeathsByFriendlyPlayer = 9;
    }
}
