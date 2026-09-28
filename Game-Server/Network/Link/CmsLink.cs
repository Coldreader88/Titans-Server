using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.World;

namespace TitansUC.GameServer.Network.Link
{
    /// <summary>
    /// The connection to the CMS (chat) server, which sends the GM commands that act in the game world
    /// (GameServer.xml ChatHost / ChatPort / ChatPassword, CMSServer.xml GameLinkPort / GameLinkPassword).
    /// Java reference: mina_gameserver cluster/cms (GCClient.java and incoming/).
    /// </summary>
    public class CmsLink : DefaultClient<CGOpcode>
    {
        const int RetrySeconds = 10;

        static CmsLink current;
        static Timer closureTimer;
        static readonly object closureSync = new object();

        public CmsLink()
        {
            this.Host = Configuration.Instance.ChatHost;
            this.Port = Configuration.Instance.ChatPort;
            RegisterPacketHandler(CGOpcode.CMS_TELEPORT, new CMS_TELEPORT());
            RegisterPacketHandler(CGOpcode.CMS_SPAWN, new CMS_SPAWN());
            RegisterPacketHandler(CGOpcode.CMS_CLOSURE, new CMS_CLOSURE());
            RegisterPacketHandler(CGOpcode.CMS_END_MAINTENANCE, new CMS_END_MAINTENANCE());
            RegisterPacketHandler(CGOpcode.CMS_POSITION_LOG, new CMS_POSITION_LOG());
            RegisterPacketHandler(CGOpcode.CMS_TELEPORT_TO_PLAYER, new CMS_TELEPORT_TO_PLAYER());
            RegisterPacketHandler(CGOpcode.CMS_GM_COMMAND, new CMS_GM_COMMAND());
        }

        /// <summary>
        /// Keeps a connection to the CMS server open from a background thread, reconnecting when it drops.
        /// </summary>
        public static void Start()
        {
            var thread = new Thread(KeepConnected) { IsBackground = true, Name = "CmsLink" };
            thread.Start();
        }

        private static void KeepConnected()
        {
            bool warned = false;
            while (true)
            {
                try
                {
                    var link = current;
                    if (link == null || link.Network == null || link.Network.Disconnected)
                    {
                        if (Reachable())
                        {
                            link = new CmsLink();
                            if (link.Connect(0))
                            {
                                current = link;
                                warned = false;
                                Logger.ShowInfo(string.Format("Connected to the CMS server at {0}:{1}.", link.Host, link.Port));
                            }
                        }
                        else if (!warned)
                        {
                            Logger.ShowWarning(string.Format("Cannot reach the CMS server at {0}:{1}; GM commands from chat will not work until it is up. Retrying every {2} seconds.",
                                Configuration.Instance.ChatHost, Configuration.Instance.ChatPort, RetrySeconds));
                            warned = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowError(ex);
                }
                Thread.Sleep(RetrySeconds * 1000);
            }
        }

        /// <summary>
        /// Tries the port first so a CMS server that is down does not fill the log with socket errors
        /// (<see cref="DefaultClient{T}.Connect"/> logs every failure).
        /// </summary>
        private static bool Reachable()
        {
            try
            {
                using (var probe = new TcpClient())
                {
                    var result = probe.BeginConnect(Configuration.Instance.ChatHost, Configuration.Instance.ChatPort, null, null);
                    bool ok = result.AsyncWaitHandle.WaitOne(3000) && probe.Connected;
                    if (ok)
                    {
                        probe.EndConnect(result);
                    }
                    return ok;
                }
            }
            catch (SocketException)
            {
                return false;
            }
        }

        protected override void SendInitialPacket()
        {
            this.Network.SendPacket(GameLink.Hello(Configuration.Instance.ChatPassword));
        }

        public override void OnDisconnect()
        {
            base.OnDisconnect();
            Logger.ShowWarning("Lost the connection to the CMS server; reconnecting.");
        }

        public void Send(Packet<CGOpcode> p)
        {
            var network = this.Network;
            if (network != null && !network.Disconnected)
            {
                network.SendPacket(p);
            }
        }

        /// <summary>
        /// A system message (chat) to one player through the CMS server. Does nothing when it is not connected.
        /// </summary>
        public static void SystemMessage(uint characterID, string message)
        {
            var link = current;
            if (link != null)
            {
                link.Send(GameLink.SystemMessage(characterID, message));
            }
        }

        public void OnTeleport(uint characterID, int x, int y, int z)
        {
            var player = GameWorld.Instance.Get(characterID);
            if (player != null)
            {
                player.Teleport(x, y, z);
            }
        }

        public void OnTeleportTo(uint fromID, uint toID)
        {
            var from = GameWorld.Instance.Get(fromID);
            var to = GameWorld.Instance.Get(toID);
            if (from == null)
            {
                return;
            }
            if (to == null || to.Coord == null)
            {
                SystemMessage(fromID, string.Format("Player {0} is not on this game server.", toID));
                return;
            }
            var target = to.Coord;
            from.Teleport(target.X, target.Y, target.Z);
        }

        public void OnSpawn(uint characterID, string args)
        {
            var player = GameWorld.Instance.Get(characterID);
            if (player == null)
            {
                return;
            }
            SystemMessage(characterID, player.GmSpawn(args.Split(new[] { "::" }, StringSplitOptions.None)));
        }

        public void OnGmCommand(uint characterID, string command)
        {
            var player = GameWorld.Instance.Get(characterID);
            if (player == null)
            {
                return;
            }
            foreach (var line in GmCommands.Run(player, command))
            {
                SystemMessage(characterID, line);
            }
        }

        public void OnPositionLog(uint characterID, string message)
        {
            var player = GameWorld.Instance.Get(characterID);
            if (player == null || player.Coord == null)
            {
                return;
            }
            var coord = player.Coord;
            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} {1} [X:{2}, Y:{3}, Z:{4}] {5}",
                DateTime.Now, player.Character.Name, coord.X, coord.Y, coord.Z, message);
            Logger.ShowInfo("Position log: " + line);
            try
            {
                File.AppendAllText("PositionLog.txt", line + Environment.NewLine);
                SystemMessage(characterID, string.Format("Logged [X:{0}, Y:{1}, Z:{2}].", coord.X, coord.Y, coord.Z));
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        /// <summary>
        /// Maintenance: after <paramref name="seconds"/>, close logins and log everyone out.
        /// Java reference: cluster/cms/incoming/RequestGSClosure.java and ShutdownEvent.java.
        /// </summary>
        public void OnClosure(int seconds)
        {
            Logger.ShowWarning(string.Format("The CMS server closes this game server for maintenance in {0} seconds.", seconds));
            lock (closureSync)
            {
                if (closureTimer != null)
                {
                    closureTimer.Dispose();
                }
                closureTimer = new Timer(_ => Close(), null, Math.Max(0, seconds) * 1000L, Timeout.Infinite);
            }
        }

        private static void Close()
        {
            GameWorld.Instance.Closed = true;
            var players = GameWorld.Instance.Players;
            Logger.ShowWarning(string.Format("Closed for maintenance; logging out {0} players.", players.Count));
            foreach (UCGameSession player in players)
            {
                try
                {
                    player.ForceLogout();
                }
                catch (Exception ex)
                {
                    Logger.ShowError(ex);
                }
            }
        }

        public void OnEndMaintenance()
        {
            lock (closureSync)
            {
                if (closureTimer != null)
                {
                    closureTimer.Dispose();
                    closureTimer = null;
                }
            }
            GameWorld.Instance.Closed = false;
            Logger.ShowInfo("Maintenance ended; logins are open again.");
        }
    }
}
