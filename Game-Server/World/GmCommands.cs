using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Common.Characters;
using Common.Database;
using SmartEngine.Core;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.Network.Packets.Client;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// GM chat commands the game server answers itself (the CMS server forwards them): #items and #skill.
    /// Each returns the lines to show the GM as system messages.
    /// </summary>
    public static class GmCommands
    {
        public const int ItemsPerPage = 15;

        /// <summary>
        /// Skill levels are stored in tenths (the starter kit's 5.0 mobile suit skill is 50); the Java server
        /// capped them at 130.0.
        /// </summary>
        public const int MaxSkill = 1300;

        public static List<string> Run(UCGameSession gm, string command)
        {
            var parts = command.Split(new[] { "::" }, StringSplitOptions.None).Select(a => a.Trim()).ToList();
            var args = parts.Skip(1).Where(a => a.Length > 0).ToList();
            switch (parts[0].ToLowerInvariant())
            {
                case "items":
                    return Items(args);
                case "skill":
                    return SkillCommand(gm, args);
                default:
                    return new List<string> { "This game server does not know #" + parts[0] + "." };
            }
        }

        /// <summary>
        /// #items: the categories; #items::weapon[::page]; #items::weapon::zaku[::page] (names containing
        /// "zaku"); #items::all::zaku searches every category.
        /// </summary>
        public static List<string> Items(IList<string> args)
        {
            var lines = new List<string>();
            if (args.Count == 0)
            {
                lines.Add("Item categories (#items::category[::name filter][::page]):");
                lines.Add(string.Join(", ", ItemTemplates.Categories().Select(c => c.Key + " (" + c.Value + ")")));
                return lines;
            }

            string category = args[0].ToLowerInvariant();
            var items = ItemTemplates.InCategory(category);
            if (items.Count == 0)
            {
                lines.Add("No category \"" + args[0] + "\". #items lists them.");
                return lines;
            }

            int page = 1, n;
            string filter = null;
            foreach (var a in args.Skip(1))
            {
                if (int.TryParse(a, out n))
                {
                    page = n;
                }
                else
                {
                    filter = a;
                }
            }
            if (filter != null)
            {
                items = items.Where(t => t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            int pages = Math.Max(1, (items.Count + ItemsPerPage - 1) / ItemsPerPage);
            page = Math.Max(1, Math.Min(page, pages));

            lines.Add(string.Format("{0}{1}: {2} items, page {3}/{4}", category, filter != null ? " \"" + filter + "\"" : "",
                items.Count, page, pages));
            foreach (var t in items.Skip((page - 1) * ItemsPerPage).Take(ItemsPerPage))
            {
                lines.Add(t.ID + "  " + t.Name + (t.ForSale ? "" : " (not sold)"));
            }
            if (page < pages)
            {
                lines.Add(string.Format("#items::{0}{1}::{2} for more. Spawn one with #spawn::id::<id>.",
                    category, filter != null ? "::" + filter : "", page + 1));
            }
            return lines;
        }

        /// <summary>
        /// Chat names of the skills, in the order #skill lists them.
        /// </summary>
        private static readonly KeyValuePair<string, Skill>[] SkillNames =
        {
            Named("strength", Skill.STRENGTH), Named("spirit", Skill.SPIRIT), Named("luck", Skill.LUCK),
            Named("ms", Skill.MOBILE_SUIT), Named("ma", Skill.MOBILE_ARMOR), Named("fighter", Skill.FIGHTER),
            Named("spaceengagement", Skill.SPACE_ENGAGEMENT), Named("groundengagement", Skill.GROUND_ENGAGEMENT),
            Named("airengagement", Skill.AIR_ENGAGEMENT), Named("beam", Skill.BEAMCARTRIDGE_WEAPON),
            Named("shell", Skill.SHELLFIRING_WEAPON), Named("weaponmanipulation", Skill.WEAPON_MANIPULATION),
            Named("shooting", Skill.SHOOTING), Named("sniping", Skill.SNIPING), Named("cqb", Skill.CQB),
            Named("handtohand", Skill.HANDTOHAND_COMBAT), Named("tactics", Skill.TACTICS), Named("ambac", Skill.AMBAC),
            Named("defence", Skill.DEFENCE), Named("evasion", Skill.EVASION), Named("emergencyrepair", Skill.EMERGENCY_REPAIR),
            Named("mining", Skill.MINING), Named("refinery", Skill.REFINERY), Named("msmaconstruction", Skill.MSMA_CONSTRUCTION),
            Named("battleshipconstruction", Skill.BATTLESHIP_CONSTRUCTION), Named("armsconstruction", Skill.ARMS_CONSTRUCTION),
            Named("clothing", Skill.CLOTHING_MANUFACTURING),
        };

        private static readonly Dictionary<string, Skill> Aliases = new Dictionary<string, Skill>
        {
            { "str", Skill.STRENGTH }, { "mobilesuit", Skill.MOBILE_SUIT }, { "mobilearmor", Skill.MOBILE_ARMOR },
            { "space", Skill.SPACE_ENGAGEMENT }, { "ground", Skill.GROUND_ENGAGEMENT }, { "air", Skill.AIR_ENGAGEMENT },
            { "beamcartridge", Skill.BEAMCARTRIDGE_WEAPON }, { "shellfiring", Skill.SHELLFIRING_WEAPON },
            { "manipulation", Skill.WEAPON_MANIPULATION }, { "hth", Skill.HANDTOHAND_COMBAT }, { "melee", Skill.HANDTOHAND_COMBAT },
            { "defense", Skill.DEFENCE }, { "er", Skill.EMERGENCY_REPAIR }, { "repair", Skill.EMERGENCY_REPAIR },
            { "msconstruction", Skill.MSMA_CONSTRUCTION }, { "shipconstruction", Skill.BATTLESHIP_CONSTRUCTION },
            { "weaponconstruction", Skill.ARMS_CONSTRUCTION }, { "clothingmanufacturing", Skill.CLOTHING_MANUFACTURING },
        };

        /// <summary>
        /// The skills of the player info's combat list, in its order: a skill's place is its id in 0x8034
        /// (see <see cref="PlayerInfoWriter"/>).
        /// </summary>
        private static readonly Skill?[] CombatList =
        {
            Skill.MOBILE_SUIT, Skill.MOBILE_ARMOR, null, Skill.FIGHTER, Skill.SPACE_ENGAGEMENT, Skill.GROUND_ENGAGEMENT, null,
            Skill.AIR_ENGAGEMENT, Skill.BEAMCARTRIDGE_WEAPON, Skill.SHELLFIRING_WEAPON, null, Skill.WEAPON_MANIPULATION,
            Skill.SHOOTING, Skill.SNIPING, Skill.CQB, Skill.HANDTOHAND_COMBAT, Skill.TACTICS, Skill.AMBAC, Skill.DEFENCE,
            Skill.EVASION, Skill.EMERGENCY_REPAIR,
        };

        private static KeyValuePair<string, Skill> Named(string name, Skill skill)
        {
            return new KeyValuePair<string, Skill>(name, skill);
        }

        private static bool IsStat(Skill s)
        {
            return s == Skill.STRENGTH || s == Skill.SPIRIT || s == Skill.LUCK;
        }

        private static string Show(Skill s, int level)
        {
            return IsStat(s) ? level.ToString(CultureInfo.InvariantCulture) : (level / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static Skill? FindSkill(string name)
        {
            string key = new string(name.ToLowerInvariant().Where(char.IsLetter).ToArray());
            Skill s;
            if (Aliases.TryGetValue(key, out s))
            {
                return s;
            }
            foreach (var n in SkillNames)
            {
                if (n.Key == key)
                {
                    return n.Value;
                }
            }
            var prefix = SkillNames.Where(n => n.Key.StartsWith(key)).ToList();
            return key.Length > 0 && prefix.Count == 1 ? prefix[0].Value : (Skill?)null;
        }

        /// <summary>
        /// #skill: shows every skill; #skill::ambac::85.5 sets one (skills in points with one decimal,
        /// strength, spirit and luck as whole numbers); #skill::all::100 sets every combat skill.
        /// Saved at once; the client is sent the change as a gain (0x8034), so its window updates too.
        /// </summary>
        public static List<string> SkillCommand(UCGameSession gm, IList<string> args)
        {
            var c = gm.Character;
            var lines = new List<string>();
            if (args.Count < 2)
            {
                lines.Add("Your skills (#skill::name::level, e.g. #skill::ambac::85.5 or #skill::all::100):");
                var row = new List<string>();
                foreach (var n in SkillNames)
                {
                    row.Add(n.Key + " " + Show(n.Value, c.GetSkill(n.Value)));
                    if (row.Count == 4)
                    {
                        lines.Add(string.Join(", ", row));
                        row.Clear();
                    }
                }
                if (row.Count > 0)
                {
                    lines.Add(string.Join(", ", row));
                }
                return lines;
            }

            double value;
            if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0)
            {
                lines.Add("\"" + args[1] + "\" is not a level. Use a number like 85.5.");
                return lines;
            }

            List<Skill> skills;
            if (args[0].ToLowerInvariant() == "all")
            {
                skills = CombatList.Where(s => s.HasValue).Select(s => s.Value).ToList();
            }
            else
            {
                var s = FindSkill(args[0]);
                if (s == null)
                {
                    lines.Add("No skill \"" + args[0] + "\". Skills: " + string.Join(", ", SkillNames.Select(n => n.Key)));
                    return lines;
                }
                skills = new List<Skill> { s.Value };
            }

            var stats = new List<KeyValuePair<byte, int>>();
            var gains = new List<KeyValuePair<ushort, int>>();
            foreach (var s in skills)
            {
                int level = IsStat(s) ? (int)Math.Round(value) : (int)Math.Round(value * 10);
                level = Math.Min(level, MaxSkill);
                int gain = level - c.GetSkill(s);
                c.SetSkill(s, level);
                try
                {
                    CharacterDatabase.Instance.SaveSkill(c, s);
                }
                catch (Exception ex)
                {
                    Logger.ShowError(ex);
                    lines.Add("Could not save " + s + ": " + ex.Message);
                }
                int combat = Array.IndexOf(CombatList, s);
                if (IsStat(s))
                {
                    stats.Add(new KeyValuePair<byte, int>((byte)s, gain));
                }
                else if (combat >= 0)
                {
                    gains.Add(new KeyValuePair<ushort, int>((ushort)combat, gain));
                }
            }
            if (stats.Count > 0 || gains.Count > 0)
            {
                gm.Network.SendPacket(new SM_SKILL_GAIN(gm.CharacterID, stats, gains));
            }

            Logger.ShowInfo(string.Format("{0} set {1} to {2} with #skill.", c.Name, args[0], args[1]));
            lines.Add(skills.Count == 1
                ? string.Format("{0} is now {1}.", args[0], Show(skills[0], c.GetSkill(skills[0])))
                : string.Format("All {0} combat skills are now {1}.", skills.Count, Show(skills[0], c.GetSkill(skills[0]))));
            if (skills.Any(s => !IsStat(s) && Array.IndexOf(CombatList, s) < 0))
            {
                lines.Add("Construction skills show in the skill window after your next login.");
            }
            return lines;
        }
    }
}
