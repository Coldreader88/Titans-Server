using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Common.Database;
using Common.Network;
using Common.Network.Packets;
using Common.Server;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.GameServer.Manager;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.Network.Link;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer
{
    /// <summary>
    /// The game server (Java: mina_gameserver, launched as the Earth server). The Lobby sends clients here
    /// after they pick a character (LobbyServer.xml GameServerIP / GameServerPort).
    /// </summary>
    public class GameServer
    {
        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(ShuttingDown);
            WatchConsoleClose();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            // The logger starts in InitServer; anything logged before it (reading the configuration does)
            // crashes its thread, so only the command line is read first, for the server's name.
            Configuration.Instance.SetInstance(args);
            ServerInfo.InitServer("Game-Server (" + Configuration.Instance.InstanceName + ")");
            Configuration.Instance.Initialization("./Config/GameServer.xml");
            PlayerInventory.SetUniqueIDBase(Configuration.Instance.IsSpace);
            Logger.ShowInfo(string.Format("Running the {0} world (zone {1}) on port {2}; the other side is at {3}:{4}.",
                Configuration.Instance.InstanceName, Configuration.Instance.Zone, Configuration.Instance.ListenPort,
                Configuration.Instance.TransferHost, Configuration.Instance.TransferPort));
            Network<GSOpcode>.SuppressUnknownPackets = false;
            Network<GSOpcode>.SuppressPacketPrintOut = true;
            Network<GSOpcode>.SuppressPacketHeaderPrintOut = true;

            Logger.ShowInfo(string.Format("Connecting to MySQL database {0} at {1}:{2}",
                Configuration.Instance.DBName, Configuration.Instance.DBHost, Configuration.Instance.DBPort));
            DatabaseConnection.Init(Configuration.Instance.DBHost, Configuration.Instance.DBPort,
                Configuration.Instance.DBName, Configuration.Instance.DBUser, Configuration.Instance.DBPass);
            if (!DatabaseConnection.TestConnection())
            {
                Logger.ShowError("Cannot connect to the database, game logins will fail until it is reachable.");
            }
            if (!Configuration.Instance.CheckSessionKey)
            {
                Logger.ShowWarning("CheckSessionKey is off: anyone can log in to any character.");
            }

            try
            {
                WorldDatabase.EnsureTables();
                WorldDatabase.LoadGround(Configuration.Instance.Zone);
            }
            catch (Exception ex)
            {
                Logger.ShowError("Cannot read the saved ground items: " + ex.Message);
            }
            WorldDatabase.StartSaving(Configuration.Instance.Zone);

            GameClientManager.Instance.Port = Configuration.Instance.ListenPort;
            Encryption.KeyExchangeImplementation = new SmartEngine.Network.DefaultEncryptionKeyExchange();
            Encryption.Implementation = new Common.Network.Encryption.UCEncryption();
            // The official game server numbered its own packets 1, 2, 3, ...: once the client sends packets
            // that get no reply (0x02 moves), its numbers run ahead (UCGOZone-Login.pcap: the reply to the
            // client's packet 98 is the server's packet 97).
            UCNetwork<GSOpcode>.CountServerSequence = true;
            Network<GSOpcode>.Implementation = new UCNetwork<GSOpcode>();

            if (!GameClientManager.Instance.Start())
            {
                Logger.ShowError("Cannot Listen on port:" + Configuration.Instance.ListenPort);
                Logger.ShowError("Shutting down in 20sec.");
                GameClientManager.Instance.Stop();
                System.Threading.Thread.Sleep(20000);
                Environment.Exit(0);
                return;
            }

            World.NpcManager.Instance.Start();
            if (Configuration.Instance.Zone == (ushort)Common.Characters.Zone.EARTH)
            {
                World.Occupation.Start();
            }

            StartBackups();
            Logger.ShowInfo("Listening on port:" + GameClientManager.Instance.Port);
            Logger.ShowInfo("Accepting clients...");

            if (string.IsNullOrEmpty(Configuration.Instance.ChatPassword))
            {
                Logger.ShowWarning("ChatPassword is empty: not connecting to the CMS server, so GM commands from chat will not work.");
            }
            else
            {
                Network<CGOpcode>.SuppressPacketPrintOut = true;
                Network<CGOpcode>.SuppressPacketHeaderPrintOut = true;
                Network<CGOpcode>.Implementation = new UCNetwork<CGOpcode>();
                CmsLink.Start();
            }

            while (true)
            {
                try
                {
                    string cmd = Console.ReadLine();
                    if (cmd == null)
                    {
                        // No console (input closed): keep serving; the ground is saved as it changes.
                        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                    }
                    args = Common.Utils.SplitCommand(cmd).ToArray();
                    if (args.Length == 0)
                    {
                        continue;
                    }
                    switch (args[0].ToLower())
                    {
                        case "printthreads":
                            ClientManager.PrintAllThreads();
                            break;

                        case "printpackets":
                            Network<GSOpcode>.SuppressPacketPrintOut = !Network<GSOpcode>.SuppressPacketPrintOut;
                            Network<GSOpcode>.SuppressPacketHeaderPrintOut = Network<GSOpcode>.SuppressPacketPrintOut;
                            break;

                        case "spawn":
                            // spawn <character name or id> <#spawn arguments, e.g. id 540000 100 or npc>
                            if (args.Length < 3)
                            {
                                Logger.ShowInfo("Usage: spawn <character name or id> id itemID [amount] | name item name | ideng vehicleID engine | npc [friendly] [vehicleID]");
                                break;
                            }
                            uint spawnID;
                            var gm = uint.TryParse(args[1], out spawnID) ? GameWorld.Instance.Get(spawnID)
                                : GameWorld.Instance.Players.Find(s => string.Equals(s.Character.Name, args[1], StringComparison.OrdinalIgnoreCase));
                            if (gm == null)
                            {
                                Logger.ShowInfo(args[1] + " is not online.");
                                break;
                            }
                            Logger.ShowInfo(gm.GmSpawn(args.Skip(2).ToArray()));
                            break;

                        case "gm":
                            // gm <character name or id> <command>, e.g. gm Brian items weapon or gm Brian skill ambac 80
                            if (args.Length < 3)
                            {
                                Logger.ShowInfo("Usage: gm <character name or id> items [category [filter] [page]] | skill [name level] | town [town action]");
                                break;
                            }
                            uint gmID;
                            var who = uint.TryParse(args[1], out gmID) ? GameWorld.Instance.Get(gmID)
                                : GameWorld.Instance.Players.Find(s => string.Equals(s.Character.Name, args[1], StringComparison.OrdinalIgnoreCase));
                            if (who == null)
                            {
                                Logger.ShowInfo(args[1] + " is not online.");
                                break;
                            }
                            foreach (var line in GmCommands.Run(who, string.Join("::", args.Skip(2))))
                            {
                                Logger.ShowInfo(line);
                            }
                            break;

                        case "backup":
                            System.Threading.Tasks.Task.Run(() => MakeBackup());
                            break;

                        case "save":
                            foreach (UCGameSession player in GameWorld.Instance.Players)
                            {
                                player.Save();
                            }
                            WorldDatabase.SaveGround(Configuration.Instance.Zone);
                            Logger.ShowInfo("Saved the players and the ground.");
                            break;

                        case "online":
                        case "players":
                            foreach (UCGameSession player in GameWorld.Instance.Players)
                            {
                                var coord = player.Coord;
                                Logger.ShowInfo(string.Format("{0} (character {1}, account {2}, level {3}) from {4} at {5}, {6}, {7}",
                                    player.Character.Name, player.CharacterID, player.AccountID, player.AccountLevel, player.Address,
                                    coord.X, coord.Y, coord.Z));
                            }
                            Logger.ShowInfo(string.Format("{0} players online.", GameWorld.Instance.Count));
                            break;

                        case "kick":
                            // kick <character name or id>: saves them and logs them out
                            if (args.Length < 2)
                            {
                                Logger.ShowInfo("Usage: kick <character name or id>");
                                break;
                            }
                            var kicked = FindPlayer(args[1]);
                            if (kicked == null)
                            {
                                Logger.ShowInfo(args[1] + " is not online.");
                                break;
                            }
                            kicked.Kick();
                            Logger.ShowInfo(string.Format("Kicked {0}.", kicked.Character.Name));
                            break;

                        case "ban":
                        case "unban":
                            // ban <character name> [days] (none or 0: for good), unban <character name>: the whole account
                            if (args.Length < 2)
                            {
                                Logger.ShowInfo("Usage: ban <character name> [days] (no days: for good); unban <character name>");
                                break;
                            }
                            int banDays = 0;
                            if (args[0].ToLower() == "unban")
                            {
                                banDays = -1;
                            }
                            else if (args.Length > 2 && (!int.TryParse(args[2], out banDays) || banDays < 0))
                            {
                                Logger.ShowInfo("The days must be a number, 0 or more (0: for good).");
                                break;
                            }
                            uint bannedAccount;
                            Logger.ShowInfo(Common.Database.AccountBans.BanCharacter(args[1], banDays, out bannedAccount));
                            if (banDays >= 0 && bannedAccount != 0)
                            {
                                foreach (UCGameSession player in GameWorld.Instance.Players)
                                {
                                    if (player.AccountID == bannedAccount)
                                    {
                                        player.Kick();
                                        Logger.ShowInfo(string.Format("Kicked {0}.", player.Character.Name));
                                    }
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowError(ex);
                }
            }
        }

        private static Timer backupTimer;

        /// <summary>
        /// Starts the backups every BackupHours (the Earth server only, so two servers do not both make them).
        /// </summary>
        private static void StartBackups()
        {
            int hours = Configuration.Instance.BackupHours;
            if (hours <= 0 || Configuration.Instance.IsSpace)
            {
                return;
            }
            var every = TimeSpan.FromHours(hours);
            backupTimer = new Timer(_ => MakeBackup(), null, every, every);
            Logger.ShowInfo(string.Format("Backing up the database every {0} hours to {1}.", hours, Configuration.Instance.BackupFolder));
        }

        private static void MakeBackup()
        {
            try
            {
                var started = DateTime.Now;
                string file = Common.Database.Backup.Run(Configuration.Instance.BackupFolder, Configuration.Instance.BackupKeep);
                Logger.ShowInfo(string.Format("Backed up the database to {0} ({1:0.0} MB, {2:0.0} s).", file,
                    new System.IO.FileInfo(file).Length / 1048576.0, (DateTime.Now - started).TotalSeconds));
            }
            catch (Exception ex)
            {
                Logger.ShowWarning("The database backup failed: " + ex.Message);
            }
        }

        private static UCGameSession FindPlayer(string nameOrID)
        {
            uint id;
            return uint.TryParse(nameOrID, out id) ? GameWorld.Instance.Get(id)
                : GameWorld.Instance.Players.Find(s => string.Equals(s.Character.Name, nameOrID, StringComparison.OrdinalIgnoreCase));
        }

        private static void ShuttingDown(object sender, ConsoleCancelEventArgs args)
        {
            Logger.ShowInfo("Closing.....");
            SaveEverything();
            GameClientManager.Instance.Stop();
        }

        private static int savedAtExit;

        /// <summary>
        /// Writes every player and the ground, once, however the server is going down.
        /// </summary>
        private static void SaveEverything()
        {
            if (Interlocked.Exchange(ref savedAtExit, 1) == 1)
            {
                return;
            }
            foreach (UCGameSession player in GameWorld.Instance.Players)
            {
                try
                {
                    player.Save();
                }
                catch (Exception ex)
                {
                    Logger.ShowError(ex);
                }
            }
            try
            {
                WorldDatabase.SaveGround(Configuration.Instance.Zone);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        private delegate bool ConsoleCtrlHandler(int type);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, bool add);

        private static ConsoleCtrlHandler consoleHandler;

        /// <summary>
        /// Closing the console window, logging off or shutting Windows down does not raise CancelKeyPress; Windows
        /// tells the console handler instead (close 2, logoff 5, shutdown 6), and the server saves before it goes.
        /// </summary>
        private static void WatchConsoleClose()
        {
            try
            {
                consoleHandler = type =>
                {
                    if (type == 2 || type == 5 || type == 6)
                    {
                        SaveEverything();
                    }
                    return false;
                };
                SetConsoleCtrlHandler(consoleHandler, true);
            }
            catch (Exception)
            {
                // Not on Windows: Ctrl+C is all there is.
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            Logger.ShowError("Fatal: An unhandled exception is thrown, terminating...");
            Logger.ShowError("Error Message:" + ex.Message);
            Logger.ShowError("Call Stack:" + ex.StackTrace);

            SaveEverything();
            GameClientManager.Instance.Stop();
        }
    }
}
