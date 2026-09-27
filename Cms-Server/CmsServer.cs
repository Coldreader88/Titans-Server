using System;
using Common.Database;
using Common.Network;
using Common.Network.Packets;
using Common.Server;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.CmsServer.Database;
using TitansUC.CmsServer.Manager;
using TitansUC.CmsServer.Network.Client;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer
{
    /// <summary>
    /// The CMS server (Java: mina_cmsserver on 24016): chat, friend lists, teams, group chat and GM
    /// commands. Game clients connect to it next to the game server; game servers connect to its game
    /// link (CMSServer.xml GameLinkPort) for GM commands that act in the world.
    /// </summary>
    public class CmsServer
    {
        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(ShuttingDown);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            ServerInfo.InitServer("CMS-Server");

            Configuration.Instance.Initialization("./Config/CMSServer.xml");
            Network<CMSOpcode>.SuppressUnknownPackets = false;
            Network<CMSOpcode>.SuppressPacketPrintOut = true;
            Network<CMSOpcode>.SuppressPacketHeaderPrintOut = true;
            Network<CGOpcode>.SuppressPacketPrintOut = true;
            Network<CGOpcode>.SuppressPacketHeaderPrintOut = true;

            Logger.ShowInfo(string.Format("Connecting to MySQL database {0} at {1}:{2}",
                Configuration.Instance.DBName, Configuration.Instance.DBHost, Configuration.Instance.DBPort));
            DatabaseConnection.Init(Configuration.Instance.DBHost, Configuration.Instance.DBPort,
                Configuration.Instance.DBName, Configuration.Instance.DBUser, Configuration.Instance.DBPass);
            if (DatabaseConnection.TestConnection())
            {
                try
                {
                    CmsDatabase.Instance.EnsureTables();
                }
                catch (Exception ex)
                {
                    Logger.ShowError("Cannot create the team and friends tables: " + ex.Message);
                }
            }
            else
            {
                Logger.ShowError("Cannot connect to the database, CMS logins will fail until it is reachable.");
            }
            if (!Configuration.Instance.CheckLoginSession)
            {
                Logger.ShowWarning("CheckLoginSession is off: anyone can log in to the CMS server as any character.");
            }

            Encryption.KeyExchangeImplementation = new SmartEngine.Network.DefaultEncryptionKeyExchange();
            Encryption.Implementation = new Common.Network.Encryption.UCEncryption();
            // The official CMS server numbered its own packets instead of echoing the client's numbers.
            UCNetwork<CMSOpcode>.CountServerSequence = true;
            Network<CMSOpcode>.Implementation = new UCNetwork<CMSOpcode>();
            Network<CGOpcode>.Implementation = new UCNetwork<CGOpcode>();

            CmsClientManager.Instance.Port = Configuration.Instance.Port;
            if (!CmsClientManager.Instance.Start())
            {
                Logger.ShowError("Cannot Listen on port:" + Configuration.Instance.Port);
                Logger.ShowError("Shutting down in 20sec.");
                CmsClientManager.Instance.Stop();
                System.Threading.Thread.Sleep(20000);
                Environment.Exit(0);
                return;
            }
            Logger.ShowInfo("Listening on port:" + CmsClientManager.Instance.Port);

            if (string.IsNullOrEmpty(Configuration.Instance.GameLinkPassword))
            {
                Logger.ShowWarning("GameLinkPassword is empty, so the game link is off: GM commands that act in the game world will not work.");
            }
            else
            {
                GameLinkManager.Instance.Port = Configuration.Instance.GameLinkPort;
                if (GameLinkManager.Instance.Start())
                {
                    Logger.ShowInfo("Listening for game servers on port:" + GameLinkManager.Instance.Port);
                }
                else
                {
                    Logger.ShowError("Cannot listen for game servers on port:" + Configuration.Instance.GameLinkPort);
                }
            }

            Logger.ShowInfo("Accepting clients...");

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
                            Network<CMSOpcode>.SuppressPacketPrintOut = !Network<CMSOpcode>.SuppressPacketPrintOut;
                            Network<CMSOpcode>.SuppressPacketHeaderPrintOut = Network<CMSOpcode>.SuppressPacketPrintOut;
                            break;

                        case "online":
                            foreach (UCCmsSession player in CmsWorld.Instance.Players)
                            {
                                Logger.ShowInfo(string.Format("{0} (character {1})", player.Name, player.CharacterID));
                            }
                            Logger.ShowInfo(string.Format("{0} players online, game link {1}.", CmsWorld.Instance.Count,
                                GameLinkManager.Instance.Connected ? "connected" : "not connected"));
                            break;

                        case "sys":
                            if (cmd.Length > 4)
                            {
                                CmsWorld.Instance.SystemMessage(cmd.Substring(4));
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

        public static uint UnixTime()
        {
            return (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static void ShuttingDown(object sender, ConsoleCancelEventArgs args)
        {
            Logger.ShowInfo("Closing.....");
            CmsClientManager.Instance.Stop();
            GameLinkManager.Instance.Stop();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            Logger.ShowError("Fatal: An unhandled exception is thrown, terminating...");
            Logger.ShowError("Error Message:" + ex.Message);
            Logger.ShowError("Call Stack:" + ex.StackTrace);

            CmsClientManager.Instance.Stop();
        }
    }
}
