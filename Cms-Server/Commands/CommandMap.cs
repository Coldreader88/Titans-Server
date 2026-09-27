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
    }

    /// <summary>
    /// The GM commands, typed in chat as #name::arg::arg (the Java syntax) or #name arg arg.
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

        private CommandMap()
        {
            Add(new Command
            {
                Name = "help", Usage = "#help", Level = AccessLevel.GM,
                Description = "Lists the commands you can use.",
                Run = (s, a) => Help(s),
            });
            Add(new Command
            {
                Name = "online", Usage = "#online", Level = AccessLevel.Player,
                Description = "Shows how many players are online (VIP and up also see who, with their ids).",
                Run = Online,
            });
            Add(new Command
            {
                Name = "sys", Usage = "#sys::message", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Sends a system message to all online players.",
                Run = (s, a) => CmsWorld.Instance.SystemMessage(string.Join(" ", a)),
            });
            Add(new Command
            {
                Name = "tele", Usage = "#tele::x::y::z", Level = AccessLevel.GM, MinArguments = 3,
                Description = "Teleports you to the given coordinates.",
                Run = Teleport,
            });
            Add(new Command
            {
                Name = "tp", Usage = "#tp::targetID[::playerID]", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Teleports you (or playerID) to the player targetID. #online shows the ids.",
                Run = TeleportTo,
            });
            Add(new Command
            {
                Name = "bookmark", Usage = "#bookmark::name", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Teleports you to a place from the tele_bookmark table.",
                Run = Bookmark,
            });
            Add(new Command
            {
                Name = "spawn", Usage = "#spawn::id::itemID | #spawn::name::item name | #spawn::ideng::vehicleID::engineName | #spawn::npc", Level = AccessLevel.GM, MinArguments = 1,
                Description = "Asks the game server to spawn an item or vehicle next to you.",
                Run = Spawn,
            });
            Add(new Command
            {
                Name = "poslog", Usage = "#poslog::message", Level = AccessLevel.VIP, MinArguments = 1,
                Description = "Writes your position and a message to the game server's position log.",
                Run = (s, a) => ToGame(s, GameLinkManager.Instance.PositionLog(s.CharacterID, string.Join(" ", a))),
            });
            Add(new Command
            {
                Name = "script", Usage = "#script::game|cms::file", Level = AccessLevel.GM, MinArguments = 2,
                Description = "Runs a script (Jython on the Java server; not available in the C# servers).",
                Run = (s, a) => CmsWorld.Instance.SystemMessage(s, "Scripts are not supported by the C# servers."),
            });
            Add(new Command
            {
                Name = "shutdown", Usage = "#shutdown::seconds (-1 ends maintenance)", Level = AccessLevel.Admin, MinArguments = 1,
                Description = "Counts down to maintenance, then closes the game servers. -1 ends the maintenance.",
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
        /// Splits "name::a::b" (Java) or "name a b".
        /// </summary>
        public static List<string> Split(string line)
        {
            line = (line ?? string.Empty).Trim();
            string[] parts = line.Contains("::")
                ? line.Split(new[] { "::" }, StringSplitOptions.None)
                : line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var list = parts.Select(p => p.Trim()).ToList();
            if (list.Count > 0 && list[0].Length == 0)
            {
                list.Clear();
            }
            return list;
        }

        private void Describe(UCCmsSession session, Command command)
        {
            CmsWorld.Instance.SystemMessage(session, command.Usage + ": " + command.Description);
        }

        private void Help(UCCmsSession session)
        {
            foreach (var command in commands.Values.Where(c => session.Level >= c.Level).OrderBy(c => c.Name))
            {
                Describe(session, command);
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
                CmsWorld.Instance.SystemMessage(session, "Usage: #tele::x::y::z");
                return;
            }
            ToGame(session, GameLinkManager.Instance.Teleport(session.CharacterID, x, y, z));
        }

        private static void TeleportTo(UCCmsSession session, List<string> args)
        {
            uint to, from = session.CharacterID;
            if (!uint.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out to) ||
                (args.Count > 1 && !uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out from)))
            {
                CmsWorld.Instance.SystemMessage(session, "Usage: #tp::targetID[::playerID]");
                return;
            }
            ToGame(session, GameLinkManager.Instance.TeleportTo(from, to));
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
                CmsWorld.Instance.SystemMessage(session, "Usage: #shutdown::seconds (-1 ends maintenance)");
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
