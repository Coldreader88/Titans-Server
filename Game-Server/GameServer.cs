using System;
using System.Linq;
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

                        case "save":
                            foreach (UCGameSession player in GameWorld.Instance.Players)
                            {
                                player.Save();
                            }
                            WorldDatabase.SaveGround(Configuration.Instance.Zone);
                            Logger.ShowInfo("Saved the players and the ground.");
                            break;

                        case "online":
                            foreach (UCGameSession player in GameWorld.Instance.Players)
                            {
                                var coord = player.Coord;
                                Logger.ShowInfo(string.Format("{0} (character {1}) at {2}, {3}, {4}",
                                    player.Character.Name, player.CharacterID, coord.X, coord.Y, coord.Z));
                            }
                            Logger.ShowInfo(string.Format("{0} players online.", GameWorld.Instance.Count));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowError(ex);
                }
            }
        }

        private static void ShuttingDown(object sender, ConsoleCancelEventArgs args)
        {
            Logger.ShowInfo("Closing.....");
            foreach (UCGameSession player in GameWorld.Instance.Players)
            {
                player.Save();
            }
            WorldDatabase.SaveGround(Configuration.Instance.Zone);
            GameClientManager.Instance.Stop();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            Logger.ShowError("Fatal: An unhandled exception is thrown, terminating...");
            Logger.ShowError("Error Message:" + ex.Message);
            Logger.ShowError("Call Stack:" + ex.StackTrace);

            GameClientManager.Instance.Stop();
        }
    }
}
