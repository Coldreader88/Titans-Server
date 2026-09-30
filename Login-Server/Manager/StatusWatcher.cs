using System;
using System.Threading;
using Common.Database;
using LoginServer;
using SmartEngine.Core;

namespace Login_Server.Manager
{
    /// <summary>
    /// Works out the status the launcher shows from the other servers: they write a heartbeat and the
    /// maintenance flag to the shared database (Common.Database.ServerState). Maintenance (#shutdown) shows
    /// MAINTENANCE; a Lobby or Earth game server that stopped beating shows OFFLINE; otherwise ONLINE.
    /// The console command "mode online|offline|maintenance" fixes the status until "mode auto".
    /// </summary>
    public static class StatusWatcher
    {
        private static Timer timer;

        /// <summary>
        /// Whether the status follows the servers (true) or was fixed on the console (false).
        /// </summary>
        public static bool Automatic { get; private set; }

        public static void Start()
        {
            var config = Configuration.Instance;
            if (string.IsNullOrEmpty(config.DBHost) || config.WatchSeconds <= 0)
            {
                Logger.ShowWarning("No <Database> or <Watch><Seconds> is 0: the status stays ONLINE unless changed with the console command mode.");
                return;
            }
            DatabaseConnection.Init(config.DBHost, config.DBPort, config.DBName, config.DBUser, config.DBPass);
            Automatic = true;
            timer = new Timer(_ => Check(), null, TimeSpan.Zero, TimeSpan.FromSeconds(config.WatchSeconds));
            Logger.ShowInfo(string.Format("The status follows the Lobby and game servers (checked every {0} s).", config.WatchSeconds));
        }

        /// <summary>
        /// Fixes the status (console "mode ..."), or with null lets it follow the servers again.
        /// </summary>
        public static void Set(Status? status)
        {
            if (status == null)
            {
                if (timer == null)
                {
                    Logger.ShowWarning("Cannot follow the servers without <Database> in LoginServer.xml.");
                    return;
                }
                Automatic = true;
                Check();
                return;
            }
            Automatic = false;
            Change(status.Value, "set on the console");
        }

        /// <summary>
        /// The status the servers' state asks for, and why.
        /// </summary>
        public static Status Decide(out string why)
        {
            var config = Configuration.Instance;
            // A server beats every 10 s; three missed beats (or the watch interval, if longer) = stopped.
            long stale = Math.Max(30, config.WatchSeconds * 3);
            try
            {
                if (ServerState.IsMaintenance())
                {
                    why = "the game is closed for maintenance";
                    return Status.MAINTENANCE;
                }
                if (config.WatchLobby && !Running("lobby", stale))
                {
                    why = "the Lobby-Server is not running";
                    return Status.OFFLINE;
                }
                if (config.WatchGame && !Running("game_earth", stale))
                {
                    why = "the Earth Game-Server is not running";
                    return Status.OFFLINE;
                }
                why = "the Lobby and game servers are running";
                return Status.ONLINE;
            }
            catch (Exception ex)
            {
                why = "the database cannot be reached (" + ex.Message + ")";
                return Status.OFFLINE;
            }
        }

        private static bool Running(string server, long stale)
        {
            long seconds = ServerState.SecondsSinceBeat(server);
            return seconds >= 0 && seconds <= stale;
        }

        private static void Check()
        {
            if (!Automatic)
            {
                return;
            }
            string why;
            Status status = Decide(out why);
            if (Automatic)
            {
                Change(status, why);
            }
        }

        private static void Change(Status status, string why)
        {
            var manager = LoginClientManager.Instance;
            if (manager.Status == status && !why.StartsWith("set"))
            {
                return;
            }
            manager.Status = status;
            Logger.ShowInfo(string.Format("Status {0}: {1}.", status, why));
        }
    }
}
