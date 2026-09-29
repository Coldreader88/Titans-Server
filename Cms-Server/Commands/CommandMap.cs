using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmartEngine.Core;
using TitansUC.CmsServer.Database;
using TitansUC.CmsServer.Manager;
using TitansUC.CmsServer.Network.Client;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Commands
{
    /// <summary>
    /// A GM command typed in chat.
    /// </summary>
    public class Command
    {
        public string Name { get; set; }

        public string Usage { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Lowest <see cref="AccessLevel"/> rank that may use it.
        /// </summary>
        public int Level { get; set; }

        public int MinArguments { get; set; }

        public Action<UCCmsSession, List<string>> Run { get; set; }

        /// <summary>
        /// Example lines #help shows.
        /// </summary>
        public string[] Examples { get; set; }
    }

    /// <summary>
    /// The GM commands, typed in chat as #name arg arg, split at white space; "double quotes" keep an argument with spaces together. (The Java server used #name::arg::arg.)
    /// Java reference: mina_cmsserver model/command (CommandMap.java and one class per command).
    /// Commands that act in the game world are sent to the game servers over the game link
    /// (<see cref="GameLinkManager"/>).
    /// </summary>
    public class CommandMap
    {
        public const string Prefix = "#";

        static readonly CommandMap instance = new CommandMap();

        public static CommandMap Instance { get { return instance; } }

        private readonly Dictionary<string, Command> commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The #items categories (one per template file in DB/Templates), for the help text.
        /// </summary>
        public const string ItemCategories =
            "weapon, shield, ammo, ms, ma, tank, car, fighter, battleship, tool, material, fuel, camp, target, " +
            "eventms, eventtank, eventcar, eventfighter, eventitem, all";

        private CommandMap()
        {
            Add(new Command
            {
                Name = "help", Usage = "#help [command]", Level = AccessLevel.GM,
                Description = "Lists the commands you can use, or one command with examples.",
                Examples = new[] { "#help", "#help items" },
                Run = Help,
            });
            Add(new Command
            {
                Name = "online", Usage = "#online", Level = AccessLevel.Player,
                Description = "Shows how many players are online (VIP and up also see who, with their ids).",
                Examples = new[] { "#online" },
                Run = Online,
            });
            Add(new Command
            {
                Name = "near", Usage = "#near [radius]", Level = AccessLevel.GM,
                Description = "Lists the players, NPCs and ground vehicles around you (view distance, 8000 by default), nearest first, with their ids.",
                Examples = new[] { "#near", "#near 2000" },
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.GmCommand(s.CharacterID, "near", a)),
            });
            Add(new Command
            {
                Name = "npcs", Usage = "#npcs [filter] [page]", Level = AccessLevel.GM,
                Description = "Lists the NPC spawns outside your view, nearest first, 15 per page, with id, vehicle, distance and whether they are destroyed. Filters: a name or vehicle, ef, zeon, earth, space, hostile, vendor, dead; all also lists the ones in view.",
                Examples = new[] { "#npcs", "#npcs 2 (page 2)", "#npcs zeon", "#npcs zaku", "#npcs space hostile", "#npcs dead", "#npcs all" },
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.GmCommand(s.CharacterID, "npcs", a)),
            });
            Add(new Command
            {
                Name = "sys", Usage = "#sys message", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Sends a system message to all online players.",
                Examples = new[] { "#sys Server restart in 10 minutes" },
                Run = (s, a) => CmsWorld.Instance.SystemMessage(string.Join(" ", a)),
            });
            Add(new Command
            {
                Name = "tele", Usage = "#tele x y z", Level = AccessLevel.GM, MinArguments = 3,
                Description = "Teleports you to the given coordinates.",
                Examples = new[] { "#tele 1000 2000 30" },
                Run = Teleport,
            });
            Add(new Command
            {
                Name = "tp", Usage = "#tp target [player]", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Teleports you (or player) next to a player, NPC or ground vehicle, by id or name. #near and #online show the ids.",
                Examples = new[] { "#tp Char", "#tp 13", "#tp 1000000043", "#tp Burchard", "#tp Amuro Char (moves Char to Amuro)" },
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.GmCommand(s.CharacterID, "tp", a)),
            });
            Add(new Command
            {
                Name = "bookmark", Usage = "#bookmark name", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Teleports you to a place from the tele_bookmark table.",
                Examples = new[] { "#bookmark Sydney" },
                Run = Bookmark,
            });
            Add(new Command
            {
                Name = "spawn", Usage = "#spawn id itemID [amount] | #spawn name item name | #spawn ideng vehicleID engine | #spawn npc [vehicleID]", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Spawns an item or vehicle next to you, or a hostile NPC 1000 away. An MS/MA spawned without an engine gets random weapons, a shield, ammo and a lv.3 engine. #items finds the ids.",
                Examples = new[] { "#spawn id 280048 (75mm machine gun)", "#spawn id 540000 100 (100 cartridges)",
                    "#spawn name elecar aaron", "#spawn id 410000 (a GM with random weapons, ammo and a lv.3 engine)",
                    "#spawn ideng 410000 290033 (a bare GM with that engine)", "#spawn npc", "#spawn npc 410007 (a ZAKU II)" },
                Run = Spawn,
            });
            Add(new Command
            {
                Name = "items", Usage = "#items [category [name filter] [page]]", Level = AccessLevel.GM,
                Description = "Lists the item templates you can #spawn, with their ids, 15 per page. Categories: " + ItemCategories + ".",
                Examples = new[] { "#items (categories and counts)", "#items weapon", "#items weapon 2 (page 2)",
                    "#items weapon zaku", "#items ms gundam", "#items all shield" },
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.GmCommand(s.CharacterID, "items", a)),
            });
            Add(new Command
            {
                Name = "skill", Usage = "#skill [name level]", Level = AccessLevel.GM,
                Description = "Shows your skills, or sets one (skills with one decimal; strength, spirit and luck whole numbers; all = every combat skill).",
                Examples = new[] { "#skill", "#skill ambac 85.5", "#skill ms 100", "#skill all 100", "#skill spirit 80" },
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.GmCommand(s.CharacterID, "skill", a)),
            });
            Add(new Command
            {
                Name = "crime", Usage = "#crime [count]", Level = AccessLevel.GM,
                Description = "Shows your criminal count and previous offenses, or sets the count (0 clears it).",
                Examples = new[] { "#crime", "#crime 5", "#crime 0" },
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.GmCommand(s.CharacterID, "crime", a)),
            });
            Add(new Command
            {
                Name = "poslog", Usage = "#poslog message", Level = AccessLevel.VIP, MinArguments = 1,
                Description = "Writes your position and a message to the game server's position log.",
                Examples = new[] { "#poslog stuck in wall" },
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.PositionLog(s.CharacterID, string.Join(" ", a))),
            });
            Add(new Command
            {
                Name = "script", Usage = "#script game|cms file", Level = AccessLevel.GM, MinArguments = 2,
                Description = "Runs a script (Jython on the Java server; not available in the C# servers).",
                Run = (s, a) => CmsWorld.Instance.SystemMessage(s, "Scripts are not supported by the C# servers."),
            });
            Add(new Command
            {
                Name = "shutdown", Usage = "#shutdown seconds (-1 ends maintenance)", Level = AccessLevel.Admin, MinArguments = 1,
                Description = "Counts down to maintenance, then closes the game servers. -1 ends the maintenance.",
                Examples = new[] { "#shutdown 300", "#shutdown -1" },
                Run = Shutdown,
            });
        }

        public IEnumerable<Command> Commands { get { return commands.Values; } }

        private void Add(Command command)
        {
            commands[command.Name] = command;
        }

        /// <summary>
        /// Runs a command line (without the # prefix) for a player.
        /// </summary>
        public void Execute(UCCmsSession session, string line)
        {
            List<string> args = Split(line);
            if (args.Count == 0)
            {
                return;
            }

            string name = args[0];
            args.RemoveAt(0);

            Command command;
            if (!commands.TryGetValue(name, out command))
            {
                CmsWorld.Instance.SystemMessage(session, string.Format("Command \"{0}\" does not exist.", name));
                return;
            }

            if (session.Level < command.Level)
            {
                Logger.ShowWarning(string.Format("{0} ({1}) tried to use #{2}, which needs {3}.",
                    session.Name, AccessLevel.Name(session.Level), command.Name, AccessLevel.Name(command.Level)));
                CmsWorld.Instance.SystemMessage(session, string.Format("Command \"{0}\" does not exist.", name));
                return;
            }

            if (args.Count < command.MinArguments)
            {
                Describe(session, command);
                return;
            }

            Logger.ShowInfo(string.Format("{0} used #{1} {2}", session.Name, command.Name, string.Join(" ", args)));
            try
            {
                command.Run(session, args);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                CmsWorld.Instance.SystemMessage(session, "The command failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Splits "name a b" at white space ("double quotes" keep spaces in one argument).
        /// </summary>
        public static List<string> Split(string line)
        {
            return Common.Utils.SplitCommand(line);
        }

        private void Describe(UCCmsSession session, Command command)
        {
            CmsWorld.Instance.SystemMessage(session, command.Usage + ": " + command.Description);
        }

        /// <summary>
        /// #help lists every command with its examples; #help name shows one.
        /// </summary>
        private void Help(UCCmsSession session, List<string> args)
        {
            IEnumerable<Command> list = commands.Values.Where(c => session.Level >= c.Level).OrderBy(c => c.Name);
            if (args.Count > 0)
            {
                list = list.Where(c => string.Equals(c.Name, args[0].TrimStart('#'), StringComparison.OrdinalIgnoreCase)).ToList();
                if (!list.Any())
                {
                    CmsWorld.Instance.SystemMessage(session, "No command #" + args[0].TrimStart('#') + ". #help lists them.");
                    return;
                }
            }
            foreach (var command in list)
            {
                Describe(session, command);
                foreach (var example in command.Examples ?? new string[0])
                {
                    CmsWorld.Instance.SystemMessage(session, "   e.g. " + example);
                }
            }
        }

        private static void Online(UCCmsSession session, List<string> args)
        {
            var players = CmsWorld.Instance.Players;
            CmsWorld.Instance.SystemMessage(session, "Online: " + players.Count);
            if (session.Level >= AccessLevel.VIP)
            {
                foreach (var p in players.OrderBy(p => p.Name))
                {
                    CmsWorld.Instance.SystemMessage(session, p.Name + "-" + p.CharacterID);
                }
            }
        }

        private static void Teleport(UCCmsSession session, List<string> args)
        {
            int x, y, z;
            if (!TryInt(args[0], out x) || !TryInt(args[1], out y) || !TryInt(args[2], out z))
            {
                CmsWorld.Instance.SystemMessage(session, "Usage: #tele x y z");
                return;
            }
            ToGame(session, GameLinkManager.Instance.Teleport(session.CharacterID, x, y, z));
        }

        private static void Bookmark(UCCmsSession session, List<string> args)
        {
            string name = string.Join(" ", args);
            var position = CmsDatabase.Instance.LoadBookmark(name);
            if (position == null)
            {
                CmsWorld.Instance.SystemMessage(session, string.Format("There is no bookmark called {0}.", name));
                return;
            }
            ToGame(session, GameLinkManager.Instance.Teleport(session.CharacterID, position[0], position[1], position[2]));
        }

        private static void Spawn(UCCmsSession session, List<string> args)
        {
            ToGame(session, GameLinkManager.Instance.Spawn(session.CharacterID, args));
        }

        private static void Shutdown(UCCmsSession session, List<string> args)
        {
            int seconds;
            if (!TryInt(args[0], out seconds) || seconds < -1)
            {
                CmsWorld.Instance.SystemMessage(session, "Usage: #shutdown seconds (-1 ends maintenance)");
                return;
            }

            if (seconds == -1)
            {
                Maintenance.Instance.Cancel();
                CmsWorld.Instance.SystemMessage("Ending the server maintenance.");
                GameLinkManager.Instance.EndMaintenance();
                return;
            }
            Maintenance.Instance.Start(seconds);
        }

        private static void ToGame(UCCmsSession session, bool sent)
        {
            if (!sent)
            {
                CmsWorld.Instance.SystemMessage(session, "No game server is connected to the CMS server.");
            }
        }

        private static bool TryInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }
    }
}
