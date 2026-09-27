using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Network;
using Common.Network.Packets;
using Common.Server;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.LobbyServer.Manager;

namespace TitansUC.LobbyServer
{
    public class LoginServer
    {
        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(ShuttingDown);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            ServerInfo.InitServer("Lobby-Server");

            Configuration.Instance.Initialization("./Config/LobbyServer.xml");
            Network<LSOpcode>.SuppressUnknownPackets = false;// !Debugger.IsAttached;
            Network<LSOpcode>.SuppressPacketPrintOut = false;
            Network<LSOpcode>.SuppressPacketHeaderPrintOut = false;
            Network<LSOpcode>.ForcePacketPrintOut = true;

            /*
            ClientManager<SagaBNS.Common.Packets.AccountPacketOpcode>.InitialSendCompletionPort = 10;
            ClientManager<SagaBNS.Common.Packets.AccountPacketOpcode>.NewSendCompletionPortEveryBatch = 2;
            ClientManager<SagaBNS.Common.Packets.CharacterPacketOpcode>.InitialSendCompletionPort = 10;
            ClientManager<SagaBNS.Common.Packets.CharacterPacketOpcode>.NewSendCompletionPortEveryBatch = 2;

            
            Logger.ShowInfo(string.Format("Connecting account server at {0}:{1}", Configuration.Instance.AccountHost, Configuration.Instance.AccountPort));
            if (!AccountSession.Instance.Connect(5))
            {
                Logger.ShowError("Cannot connect to account server");
                Logger.ShowError("Shutting down in 20sec.");
                System.Threading.Thread.Sleep(20000);
                return;
            }
            while (AccountSession.Instance.State == SESSION_STATE.NOT_IDENTIFIED ||
                AccountSession.Instance.State == SESSION_STATE.CONNECTED ||
                AccountSession.Instance.State == SESSION_STATE.DISCONNECTED)
            {
                System.Threading.Thread.Sleep(100);
            }
            if (AccountSession.Instance.State == SESSION_STATE.REJECTED ||
                AccountSession.Instance.State == SESSION_STATE.FAILED)
            {
                if (AccountSession.Instance.State == SESSION_STATE.REJECTED)
                    Logger.ShowError("Account server refused login request, please check the password");
                Logger.ShowInfo("Shutting down in 20sec.");
                AccountSession.Instance.Network.Disconnect();
                System.Threading.Thread.Sleep(20000);
                Environment.Exit(0);
                return;
            }
            Logger.ShowInfo("Login to account server successful");

            Logger.ShowInfo(string.Format("Connecting character server at {0}:{1}", Configuration.Instance.CharacterHost, Configuration.Instance.CharacterPort));
            if (!CharacterSession.Instance.Connect(5))
            {
                Logger.ShowError("Cannot connect to character server");
                Logger.ShowError("Shutting down in 20sec.");
                System.Threading.Thread.Sleep(20000);
                return;
            }
            while (CharacterSession.Instance.State == SESSION_STATE.NOT_IDENTIFIED ||
                CharacterSession.Instance.State == SESSION_STATE.CONNECTED ||
                CharacterSession.Instance.State == SESSION_STATE.DISCONNECTED)
            {
                System.Threading.Thread.Sleep(100);
            }
            if (CharacterSession.Instance.State == SESSION_STATE.REJECTED ||
                CharacterSession.Instance.State == SESSION_STATE.FAILED)
            {
                if (CharacterSession.Instance.State == SESSION_STATE.REJECTED)
                    Logger.ShowError("Character server refused login request, please check the password");
                Logger.ShowInfo("Shutting down in 20sec.");
                AccountSession.Instance.Network.Disconnect();
                System.Threading.Thread.Sleep(20000);
                Environment.Exit(0);
                return;
            }
            Logger.ShowInfo("Login to character server successful");
            */

            LobbyClientManager.Instance.Port = Configuration.Instance.Port;
            Encryption.KeyExchangeImplementation = new SmartEngine.Network.DefaultEncryptionKeyExchange();
            Encryption.Implementation = new Common.Network.Encryption.UCEncryption();
            Network<LSOpcode>.Implementation = new UCNetwork<LSOpcode>();

            if (!LobbyClientManager.Instance.Start())
            {
                Logger.ShowError("Cannot Listen on port:" + Configuration.Instance.Port);
                Logger.ShowError("Shutting down in 20sec.");
                LobbyClientManager.Instance.Stop();
                System.Threading.Thread.Sleep(20000);
                Environment.Exit(0);
                return;
            }

            Logger.ShowInfo("Listening on port:" + LobbyClientManager.Instance.Port);
            Logger.ShowInfo("Accepting clients...");

            //处理Console命令
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

                            if (!Network<ISOpcode>.SuppressPacketPrintOut)
                            {
                                Network<ISOpcode>.SuppressPacketPrintOut = true;
                            }
                            else { Network<ISOpcode>.SuppressPacketPrintOut = false; }

                            break;

                        case "printband":
                            int sendTotal = 0;
                            int receiveTotal = 0;
                            Logger.ShowWarning("Bandwidth usage information:");
                            try
                            {
                                foreach (Session<LSOpcode> i in LobbyClientManager.Instance.Clients.ToArray())
                                {
                                    sendTotal += i.Network.UpStreamBand;
                                    receiveTotal += i.Network.DownStreamBand;
                                    Logger.ShowWarning(string.Format("Client:{0} Receive:{1:0.##}KB/s Send:{2:0.##}KB/s",
                                        i.ToString(),
                                        (float)i.Network.DownStreamBand / 1024,
                                        (float)i.Network.UpStreamBand / 1024));
                                }
                            }
                            catch { }
                            Logger.ShowWarning(string.Format("Total: Receive:{0:0.##}KB/s Send:{1:0.##}KB/s",
                                        (float)receiveTotal / 1024,
                                        (float)sendTotal / 1024));
                            break;
                        case "status":
                            {
                                Logger.ShowWarning(string.Format("BufferManager:\r\n       TotalAllocatedMemory:{0:0.00}MB FreeMemory:{1:0.##}MB", (float)SmartEngine.Network.Memory.BufferManager.Instance.TotalAllocatedMemory / 1024 / 1024, (float)SmartEngine.Network.Memory.BufferManager.Instance.FreeMemory / 1024 / 1024));
                                Logger.ShowWarning("Network Status:");
                                Logger.ShowWarning("LoginServer:");
                                Logger.ShowWarning(string.Format("IOCP: \r\n       CurrentIOCPs:{0} Free IOCPs:{1}", LobbyClientManager.CurrentCompletionPort, LobbyClientManager.FreeCompletionPort));
                                //Logger.ShowWarning("AccountSession:");
                                //Logger.ShowWarning(string.Format("       Receive:{0:0.##}KB/s Send:{1:0.##}KB/s",
                                //         (float)Network.AccountServer.AccountSession.Instance.Network.DownStreamBand / 1024,
                                //        (float)Network.AccountServer.AccountSession.Instance.Network.UpStreamBand / 1024));
                                //Logger.ShowWarning(string.Format("IOCP: \r\n       CurrentIOCPs:{0} Free IOCPs:{1}", ClientManager<SagaBNS.Common.Packets.AccountPacketOpcode>.CurrentCompletionPort, ClientManager<SagaBNS.Common.Packets.AccountPacketOpcode>.FreeCompletionPort));
                                //Logger.ShowWarning("CharacterSession:");
                                //Logger.ShowWarning(string.Format("       Receive:{0:0.##}KB/s Send:{1:0.##}KB/s",
                                //         (float)Network.CharacterServer.CharacterSession.Instance.Network.DownStreamBand / 1024,
                                //         (float)Network.CharacterServer.CharacterSession.Instance.Network.UpStreamBand / 1024));
                                //Logger.ShowWarning(string.Format("IOCP: \r\n       CurrentIOCPs:{0} Free IOCPs:{1}", ClientManager<SagaBNS.Common.Packets.CharacterPacketOpcode>.CurrentCompletionPort, ClientManager<SagaBNS.Common.Packets.CharacterPacketOpcode>.FreeCompletionPort));
                                //Logger.ShowWarning(string.Format("Total OnlinePlayer:{0}", LoginClientManager.Instance.Clients.Count));
                                //Logger.ShowWarning(string.Format("Total Managed Ram:{0:0.00} MB, Total Process Ram:{1:0.00}MB", (float)GC.GetTotalMemory(false) / 1024 / 1024, (float)Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024));
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

        private static void ShuttingDown(object sender, ConsoleCancelEventArgs args)
        {
            Logger.ShowInfo("Closing.....");
            LobbyClientManager.Instance.Stop();
        }
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;

            Logger.ShowError("Fatal: An unhandled exception is thrown, terminating...");
            Logger.ShowError("Error Message:" + ex.Message);
            Logger.ShowError("Call Stack:" + ex.StackTrace);
            Logger.ShowError("Trying to save all player's data");

            LobbyClientManager.Instance.Stop();
        }
    }
}
