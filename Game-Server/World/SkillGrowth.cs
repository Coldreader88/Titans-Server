using System;
using System.Collections.Generic;
using System.Linq;
using Common.Characters;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// How skills and statuses grow. The official server sent every change as 0x8034 (a delta the client adds;
    /// the client clamps nothing), after hits (the attacker's engagement, tactics, weapon skills and weapon
    /// manipulation; the target's AMBAC, or defence on a shield hit), emergency repairs and 0x0D. When and how much
    /// was the server's own rule and is not in the captures, so the numbers here are ours: a near-untrained skill
    /// rises on most hits by 0.2 to 0.5, a trained one (50.0 and up) on a few percent of hits by 0.1.
    ///
    /// The Status Setting arrows (<see cref="SkillManagement"/>): only "raise" skills and statuses rise. Skills
    /// share a total cap (GameServer.xml SkillTotalCap, the Java server's 800.0 by default); a rise past it takes
    /// the same amount from the "lower" skills, the highest first, and is cut to what they can give.
    /// </summary>
    public static class SkillGrowth
    {
        /// <summary>
        /// The highest a skill goes, in tenths (130.0).
        /// </summary>
        public const int SkillCap = GmCommands.MaxSkill;

        /// <summary>
        /// The most all skills together may have, in tenths (GameServer.xml SkillTotalCap; 0 = no cap).
        /// </summary>
        public static int TotalCap = 8000;

        /// <summary>
        /// The highest strength, spirit and luck rise to by play (GameServer.xml StatusCap).
        /// </summary>
        public static int StatusCap = 170;

        /// <summary>
        /// Percent applied to every growth chance (GameServer.xml SkillGainRate).
        /// </summary>
        public static int GainRate = 100;

        private static readonly Random random = new Random();

        /// <summary>
        /// The chance one skill rises after a hit: 40% at 0, falling by 1% per 1.5 points to 3% from 55.5 up.
        /// </summary>
        public static double HitChance(int level)
        {
            return Math.Max(0.03, 0.4 - level / 1500.0) * GainRate / 100.0;
        }

        public static bool Roll(double chance)
        {
            lock (random)
            {
                return random.NextDouble() < chance;
            }
        }

        /// <summary>
        /// How much a skill at <paramref name="level"/> rises when it does: 0.1, or 0.1 to 0.5 below 10.0.
        /// </summary>
        public static int Amount(int level)
        {
            if (level >= 100)
            {
                return 1;
            }
            lock (random)
            {
                return 1 + random.Next(5);
            }
        }

        /// <summary>
        /// Rolls <see cref="HitChance"/> for each skill; the ones that rise with their amount.
        /// </summary>
        public static List<KeyValuePair<Skill, int>> RollHit(Character c, IEnumerable<Skill> skills)
        {
            var result = new List<KeyValuePair<Skill, int>>();
            foreach (var s in skills.Distinct())
            {
                int level = c.GetSkill(s);
                if (Roll(HitChance(level)))
                {
                    result.Add(new KeyValuePair<Skill, int>(s, Amount(level)));
                }
            }
            return result;
        }

        /// <summary>
        /// Raises the character's skills and statuses by the wanted amounts under the arrows and caps, and returns
        /// every change made (drops from "lower" skills as negative amounts), one entry per skill.
        /// <paramref name="ignoreManagement"/> (a GM's change) skips the arrows and the total cap.
        /// </summary>
        public static List<KeyValuePair<Skill, int>> Raise(Character c, IEnumerable<KeyValuePair<Skill, int>> wanted,
            bool ignoreManagement)
        {
            var changes = new Dictionary<Skill, int>();
            lock (c)
            {
                foreach (var w in wanted)
                {
                    var skill = w.Key;
                    int amount = w.Value;
                    int level = c.GetSkill(skill);
                    if (amount <= 0 || (!ignoreManagement && c.GetManagement(skill) != SkillManagement.Raise))
                    {
                        continue;
                    }

                    if (SkillTables.IsStatus(skill))
                    {
                        amount = Math.Min(amount, StatusCap - level);
                        if (amount > 0)
                        {
                            Change(c, changes, skill, amount);
                        }
                        continue;
                    }

                    amount = Math.Min(amount, SkillCap - level);
                    if (amount <= 0)
                    {
                        continue;
                    }
                    int over = !ignoreManagement && TotalCap > 0 ? Total(c) + amount - TotalCap : 0;
                    if (over > 0)
                    {
                        var donors = Enum.GetValues(typeof(Skill)).Cast<Skill>()
                            .Where(s => s != skill && !SkillTables.IsStatus(s) && c.GetManagement(s) == SkillManagement.Lower &&
                                c.GetSkill(s) > 0)
                            .OrderByDescending(s => c.GetSkill(s)).ToList();
                        int available = donors.Sum(s => c.GetSkill(s));
                        int taken = Math.Min(over, available);
                        amount -= over - taken;
                        if (amount <= 0)
                        {
                            continue;
                        }
                        foreach (var d in donors)
                        {
                            if (taken == 0)
                            {
                                break;
                            }
                            int drop = Math.Min(taken, c.GetSkill(d));
                            Change(c, changes, d, -drop);
                            taken -= drop;
                        }
                    }
                    Change(c, changes, skill, amount);
                }
            }
            return changes.Where(ch => ch.Value != 0).ToList();
        }

        /// <summary>
        /// All skills together, statuses not counted.
        /// </summary>
        public static int Total(Character c)
        {
            return Enum.GetValues(typeof(Skill)).Cast<Skill>().Where(s => !SkillTables.IsStatus(s)).Sum(s => c.GetSkill(s));
        }

        private static void Change(Character c, Dictionary<Skill, int> changes, Skill skill, int amount)
        {
            c.SetSkill(skill, c.GetSkill(skill) + amount);
            int before;
            changes.TryGetValue(skill, out before);
            changes[skill] = before + amount;
        }

        /// <summary>
        /// The engagement skill of where the player fights: space in Space, air in a fighter, else ground.
        /// </summary>
        public static Skill Engagement(ushort zone, ItemNode vehicle)
        {
            if (zone == (ushort)Zone.SPACE)
            {
                return Skill.SPACE_ENGAGEMENT;
            }
            var t = vehicle != null ? ItemTemplates.Get(vehicle.StaticID) : null;
            return t != null && (t.Category == "fighter" || t.Category == "eventfighter") ? Skill.AIR_ENGAGEMENT : Skill.GROUND_ENGAGEMENT;
        }

        /// <summary>
        /// The operation skill of a vehicle (what 0x0D may raise): mobile suit, mobile armour or fighter; null for
        /// the others.
        /// </summary>
        public static Skill? Operation(ItemNode vehicle)
        {
            var t = vehicle != null ? ItemTemplates.Get(vehicle.StaticID) : null;
            switch (t != null ? t.Category : null)
            {
                case "ms":
                case "eventms":
                    return Skill.MOBILE_SUIT;
                case "ma":
                case "eventma":
                    return Skill.MOBILE_ARMOR;
                case "fighter":
                case "eventfighter":
                    return Skill.FIGHTER;
                default:
                    return null;
            }
        }
    }
}
