using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartEngine.Core;
using SmartEngine.Network;
using SmartEngine.Network.VirtualFileSystem;

namespace Common.Server
{
    public class ServerInfo
    {
        public static string Server;

        public static void InitServer(string server_name = "Default Server", bool initialize_vfs = true, bool init_logger = true)
        {
            Server = server_name;

            if (init_logger)
            {
                string logName = server_name;

                if (logName.Contains("-"))
                {
                    logName = logName.Replace("-", "");
                }

                Logger.InitDefaultLogger(logName);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("===========================================================================");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("                 Titans-Server [OPERATION : METEOR] " + Server + "         ");
            Console.WriteLine("         (C)2007-2013 Titans-Server UCGO Emulator Project Development Team ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("===========================================================================");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            //Logger.ShowInfo("Version Informations:");
            Console.WriteLine("[Version Information]");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(Server);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(":SVN Rev." + VersionInformation.Version + "(" + VersionInformation.ModifyDate + ")");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Common Library");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(":SVN Rev." + Common.VersionInformation.Version + "(" + Common.VersionInformation.ModifyDate + ")");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("SmartEngine Network");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(":SVN Rev." + SmartEngine.Network.VersionInformation.Version + "(" + SmartEngine.Network.VersionInformation.ModifyDate + ")\n");

            Logger.ShowInfo(string.Format("Initializing VirtualFileSystem..."));

            VirtualFileSystemManager.Instance.Init(FileSystems.Real, ".");

            Logger.ShowInfo("Loading Configuration File...");
        }
    }
}
