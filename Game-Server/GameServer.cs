using System;
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

            ServerInfo.InitServer("Game-Server");

            Configuration.Instance.Initialization("./Config/GameServer.xml");
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

            GameClientManager.Instance.Port = Configuration.Instance.Port;
            Encryption.KeyExchangeImplementation = new SmartEngine.Network.DefaultEncryptionKeyExchange();
            Encryption.Implementation = new Common.Network.Encryption.UCEncryption();
            // The official game server numbered its own packets 1, 2, 3, ...: once the client sends packets
            // that get no reply (0x02 moves), its numbers run ahead (UCGOZone-Login.pcap: the reply to the
            // client's packet 98 is the server's packet 97).
            UCNetwork<GSOpcode>.CountServerSequence = true;
            Network<GSOpcode>.Implementation = new UCNetwork<GSOpcode>();

            if (!GameClientManager.Instance.Start())
            {
                Logger.ShowError("Cannot Listen on port:" + Configuration.Instance.Port);
                Logger.ShowError("Shutting down in 20sec.");
                GameClientManager.Instance.Stop();
                System.Threading.Thread.Sleep(20000);
                Environment.Exit(0);
                return;
            }

            World.NpcManager.Instance.Start();

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
                        break;
                    args = cmd.Split(' ');
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
                            // spawn <character name or id> <#spawn arguments, e.g. id::540000::100 or npc>
                            if (args.Length < 3)
                            {
                                Logger.ShowInfo("Usage: spawn <character name or id> id::itemID[::amount] | name::item name | ideng::vehicleID::engine | npc[::vehicleID]");
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
                            Logger.ShowInfo(gm.GmSpawn(string.Join(" ", args, 2, args.Length - 2).Split(new[] { "::" }, StringSplitOptions.None)));
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
