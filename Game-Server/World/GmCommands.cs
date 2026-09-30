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
    /// GM chat commands the game server answers itself (the CMS server forwards them): #items, #skill, #near, #tp and #crime.
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
                case "near":
                    return Near(gm, args);
                case "tp":
                    return TeleportTo(gm, args);
                case "crime":
                    return CrimeCommand(gm, args);
                case "town":
                    return Occupation.GmCommand(gm, args);
                case "npcs":
                    return Npcs(gm, args);
                default:
                    return new List<string> { "This game server does not know #" + parts[0] + "." };
            }
        }

        public const int NearLines = 25;

        private static int ViewDistance
        {
            get { return TitansUC.GameServer.Configuration.Instance.ViewDistance > 0 ? TitansUC.GameServer.Configuration.Instance.ViewDistance : 8000; }
        }

        private static double Distance(CoordData from, int x, int y, int z)
        {
            double dx = x - (double)from.X, dy = y - (double)from.Y, dz = z - (double)from.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static string FactionName(int faction)
        {
            return faction == 1 ? "EF" : faction == 2 ? "Zeon" : "-";
        }

        private static string VehicleName(int templateID)
        {
            var t = ItemTemplates.Get(templateID);
            return t != null ? t.Name : templateID.ToString();
        }

        /// <summary>
        /// #near [radius]: players, NPCs and vehicles on the ground within the view distance (or the radius),
        /// nearest first, with the ids #tp takes.
        /// </summary>
        public static List<string> Near(UCGameSession gm, IList<string> args)
        {
            var me = gm.Coord;
            int radius = ViewDistance, n;
            if (args.Count > 0 && int.TryParse(args[0], out n) && n > 0)
            {
                radius = n;
            }

            var found = new List<KeyValuePair<double, string>>();
            foreach (var p in GameWorld.Instance.Players)
            {
                var c = p.InGame ? p.Coord : null;
                if (p == gm || c == null || c.ClusterID != me.ClusterID || !me.IsNear(c, radius))
                {
                    continue;
                }
                var vehicle = p.Inventory.Piloting;
                found.Add(new KeyValuePair<double, string>(Distance(me, c.X, c.Y, c.Z), string.Format("{0} - player {1}, {2}, {3}",
                    p.Character.Name, p.CharacterID, FactionName((int)p.Character.Faction), vehicle != null ? "in " + vehicle.Name : "on foot")));
            }
            foreach (var npc in NpcManager.Instance.Visible(me.ClusterID, me.X, me.Y, radius))
            {
                var info = NpcManager.Instance.Get(npc.CharacterID);
                found.Add(new KeyValuePair<double, string>(Distance(me, npc.X, npc.Y, npc.Z), string.Format("{0} - NPC {1}, {2}, {3}{4}{5}",
                    info.Name, npc.CharacterID, FactionName(info.Faction), VehicleName(info.TemplateID),
                    info.IsVendor ? ", vendor" : "", npc.Damage > 0 ? ", " + npc.Damage + "% damaged" : "")));
            }
            foreach (var g in GameWorld.Instance.GroundNear(me.ClusterID, me.X, me.Y, radius, GroundItem.ListVehicles))
            {
                found.Add(new KeyValuePair<double, string>(Distance(me, g.X, g.Y, g.Z), string.Format("{0} - {1} {2} on the ground, owner {3}",
                    g.Node.Name ?? VehicleName(g.Node.StaticID), g.IsWreck ? "wreck" : "vehicle", g.UniqueID,
                    g.OwnerID == 0xFFFFFFFF ? "none" : g.OwnerID.ToString())));
            }

            found.RemoveAll(f => f.Key > radius);
            var lines = new List<string>();
            lines.Add(string.Format("{0} within {1} (#tp id goes there):", found.Count, radius));
            foreach (var f in found.OrderBy(f => f.Key).Take(NearLines))
            {
                lines.Add(string.Format("{0} ({1:0} away)", f.Value, f.Key));
            }
            if (found.Count > NearLines)
            {
                lines.Add(string.Format("... and {0} more farther away; #near radius narrows it.", found.Count - NearLines));
            }
            return lines;
        }

        /// <summary>
        /// #npcs [filter] [page]: the NPC spawns outside your view, nearest first (your zone first, then the
        /// other one). The filter matches a name, vehicle, faction (ef, zeon), zone (earth, space), "vendor",
        /// "hostile" or "dead"; "all" also lists the NPCs in view.
        /// </summary>
        public static List<string> Npcs(UCGameSession gm, IList<string> args)
        {
            var me = gm.Coord;
            int page = 1, n;
            bool all = false;
            var filters = new List<string>();
            foreach (var a in args)
            {
                if (int.TryParse(a, out n))
                {
                    page = n;
                }
                else if (a.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    all = true;
                }
                else
                {
                    filters.Add(a.ToLowerInvariant());
                }
            }

            int view = ViewDistance;
            var list = NpcManager.Instance.All
                .Select(npc => new { Npc = npc, Here = npc.Zone == me.ClusterID, Distance = npc.Zone == me.ClusterID ? Distance(me, npc.X, npc.Y, npc.Z) : double.MaxValue })
                .Where(x => all || !x.Here || x.Distance > view)
                .Where(x => filters.All(f => NpcMatches(x.Npc, f)))
                .OrderBy(x => x.Here ? 0 : 1).ThenBy(x => x.Distance).ThenBy(x => x.Npc.ID)
                .ToList();

            var lines = new List<string>();
            int pages = Math.Max(1, (list.Count + ItemsPerPage - 1) / ItemsPerPage);
            page = Math.Max(1, Math.Min(page, pages));
            lines.Add(string.Format("{0} NPCs{1}{2}, page {3}/{4} (#tp id goes there):", list.Count,
                all ? "" : " outside your view", filters.Count > 0 ? " matching \"" + string.Join(" ", filters) + "\"" : "", page, pages));
            foreach (var x in list.Skip((page - 1) * ItemsPerPage).Take(ItemsPerPage))
            {
                var npc = x.Npc;
                string where = x.Here ? string.Format("{0:0} away", x.Distance) : "in " + ZoneName(npc.Zone);
                int respawn = NpcManager.Instance.SecondsToRespawn(npc);
                string state = npc.Alive ? (npc.Damage > 0 ? ", " + npc.Damage + "% damaged" : "")
                    : respawn > 0 ? ", destroyed, back in " + respawn + " s" : ", destroyed";
                lines.Add(string.Format("{0} - NPC {1}, {2}, {3}{4}, {5}{6}", npc.Name, npc.ID, FactionName(npc.Faction),
                    VehicleName(npc.TemplateID), npc.IsVendor ? ", vendor" : "", where, state));
            }
            if (page < pages)
            {
                lines.Add(string.Format("#npcs {0}{1} for more.", filters.Count > 0 || all ? string.Join(" ", (all ? new[] { "all" } : new string[0]).Concat(filters)) + " " : "", page + 1));
            }
            return lines;
        }

        private static string ZoneName(int zone)
        {
            return zone == 2 ? "Space" : "Earth";
        }

        private static bool NpcMatches(Npc npc, string f)
        {
            switch (f)
            {
                case "ef": return npc.Faction == 1;
                case "zeon": return npc.Faction == 2;
                case "earth": return npc.Zone == 1;
                case "space": return npc.Zone == 2;
                case "vendor": return npc.IsVendor;
                case "hostile": return !npc.IsVendor;
                case "dead": return !npc.Alive;
            }
            return (npc.Name ?? "").ToLowerInvariant().Contains(f) || VehicleName(npc.TemplateID).ToLowerInvariant().Contains(f)
                || npc.ID.ToString() == f;
        }

        /// <summary>
        /// Where a #tp target is: a player (id or name), an NPC (id or name; the nearest of that name) or a
        /// vehicle on the ground (unique id). Null when there is none.
        /// </summary>
        private static CoordData Locate(UCGameSession gm, string target, out string what)
        {
            what = null;
            uint id;
            bool numeric = uint.TryParse(target, out id);
            var player = numeric ? GameWorld.Instance.Get(id)
                : GameWorld.Instance.Players.Find(p => p.InGame && string.Equals(p.Character.Name, target, StringComparison.OrdinalIgnoreCase));
            if (player != null && player.InGame && player.Coord != null)
            {
                what = player.Character.Name;
                return player.Coord;
            }

            Npc npc = null;
            if (numeric)
            {
                npc = NpcManager.Instance.Get(id);
            }
            else
            {
                var me = gm.Coord;
                npc = NpcManager.Instance.All.Where(x => x.Alive && string.Equals(x.Name, target, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Zone == me.ClusterID ? 0 : 1).ThenBy(x => Distance(me, x.X, x.Y, x.Z)).FirstOrDefault();
            }
            if (npc != null && npc.Alive)
            {
                what = "NPC " + npc.Name;
                return npc.ToCoord();
            }

            var ground = numeric ? GameWorld.Instance.GetGround(id) : null;
            if (ground != null)
            {
                what = ground.Node.Name ?? "the vehicle";
                return new CoordData { X = ground.X, Y = ground.Y, Z = ground.Z, ClusterID = ground.ClusterID };
            }
            return null;
        }

        /// <summary>
        /// #tp target [player]: teleports you (or the player) next to a player, NPC or ground vehicle.
        /// </summary>
        public static List<string> TeleportTo(UCGameSession gm, IList<string> args)
        {
            var lines = new List<string>();
            if (args.Count == 0)
            {
                lines.Add("Usage: #tp target [player]. Target: a player's id or name, an NPC's id or name, or a ground vehicle's id (#near lists them).");
                return lines;
            }

            string what;
            var to = Locate(gm, args[0], out what);
            if (to == null)
            {
                lines.Add("Nothing called \"" + args[0] + "\" here (not a player, NPC or ground vehicle on this server).");
                return lines;
            }

            var mover = gm;
            if (args.Count > 1)
            {
                string moverName;
                var who = Locate(gm, args[1], out moverName);
                uint id;
                mover = uint.TryParse(args[1], out id) ? GameWorld.Instance.Get(id)
                    : GameWorld.Instance.Players.Find(p => p.InGame && string.Equals(p.Character.Name, args[1], StringComparison.OrdinalIgnoreCase));
                if (mover == null || who == null)
                {
                    lines.Add("No player \"" + args[1] + "\" online.");
                    return lines;
                }
            }
            if (to.ClusterID != mover.Coord.ClusterID)
            {
                lines.Add(what + " is in " + (to.ClusterID == 2 ? "Space" : "Earth") + "; take a shuttle there first.");
                return lines;
            }

            // Next to it, not inside it.
            mover.Teleport(to.X + 300, to.Y, to.Z);
            lines.Add(mover == gm ? "Teleported to " + what + "." : "Teleported " + mover.Character.Name + " to " + what + ".");
            return lines;
        }

        /// <summary>
        /// #items: the categories; #items weapon [page]; #items weapon zaku [page] (names containing
        /// "zaku"); #items all zaku searches every category.
        /// </summary>
        public static List<string> Items(IList<string> args)
        {
            var lines = new List<string>();
            if (args.Count == 0)
            {
                lines.Add("Item categories (#items category [name filter] [page]):");
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
            var words = args.Skip(1).ToList();
            if (words.Count > 0 && int.TryParse(words[words.Count - 1], out n))
            {
                page = n;
                words.RemoveAt(words.Count - 1);
            }
            string filter = words.Count > 0 ? string.Join(" ", words) : null;
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
                lines.Add(string.Format("#items {0}{1} {2} for more. Spawn one with #spawn id <id>.",
                    category, filter != null ? " " + filter : "", page + 1));
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
        /// #crime: shows the GM's criminal count and previous offenses; #crime 10 sets the count (0 clears it).
        /// The client is told with 0x8008.
        /// </summary>
        public static List<string> CrimeCommand(UCGameSession gm, IList<string> args)
        {
            var c = gm.Character;
            int count;
            if (args.Count == 0 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 0)
            {
                return new List<string>
                {
                    string.Format("Criminal count {0}, previous offenses {1}. #crime n sets the count.", c.CrimeCount, c.PreviousOffense),
                };
            }
            gm.SetCrimeCount(count);
            Logger.ShowInfo(string.Format("{0} set their criminal count to {1} with #crime.", c.Name, count));
            return new List<string> { string.Format("Criminal count is now {0} (previous offenses {1}).", c.CrimeCount, c.PreviousOffense) };
        }

        /// <summary>
        /// #skill: shows every skill; #skill ambac 85.5 sets one (skills in points with one decimal,
        /// strength, spirit and luck as whole numbers); #skill all 100 sets every combat skill.
        /// Saved at once; the client is sent the change as a gain (0x8034), so its window updates too.
        /// </summary>
        public static List<string> SkillCommand(UCGameSession gm, IList<string> args)
        {
            var c = gm.Character;
            var lines = new List<string>();
            if (args.Count < 2)
            {
                lines.Add("Your skills (#skill name level, e.g. #skill ambac 85.5 or #skill all 100):");
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
                skills = SkillTables.Combat.Where(s => s.HasValue).Select(s => s.Value).ToList();
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

            var changes = new List<KeyValuePair<Skill, int>>();
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
                if (gain != 0)
                {
                    changes.Add(new KeyValuePair<Skill, int>(s, gain));
                }
            }
            if (changes.Count > 0)
            {
                gm.Network.SendPacket(new SM_SKILL_GAIN(gm.CharacterID, changes, true));
            }

            Logger.ShowInfo(string.Format("{0} set {1} to {2} with #skill.", c.Name, args[0], args[1]));
            lines.Add(skills.Count == 1
                ? string.Format("{0} is now {1}.", args[0], Show(skills[0], c.GetSkill(skills[0])))
                : string.Format("All {0} combat skills are now {1}.", skills.Count, Show(skills[0], c.GetSkill(skills[0]))));
            return lines;
        }
    }
}
