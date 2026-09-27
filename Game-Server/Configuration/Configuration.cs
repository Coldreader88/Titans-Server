using System;
using System.Xml;
using SmartEngine.Core;
using SmartEngine.Network;

namespace TitansUC.GameServer
{
    /// <summary>
    /// Settings from Config/GameServer.xml.
    /// </summary>
    public class Configuration : Singleton<Configuration>
    {
        int port = 42010, loglevel = 31, viewDistance, saveInterval = 60;
        bool checkSessionKey = true;
        string dbHost = "127.0.0.1", dbName = "titans-server", dbUser = "root", dbPass = "";
        int dbPort = 3306;
        string chatHost = "127.0.0.1", chatPassword = "";
        int chatPort = 10241;

        /// <summary>
        /// Port the game server listens on (the Lobby sends it to the client, see LobbyServer.xml GameServerPort).
        /// </summary>
        public int Port { get { return port; } }

        public int LogLevel { get { return loglevel; } }

        /// <summary>
        /// Only log in characters the Lobby handed over with a matching session key. Turn off only to test
        /// with a client that skips the Lobby.
        /// </summary>
        public bool CheckSessionKey { get { return checkSessionKey; } }

        /// <summary>
        /// How far away other players are visible, in position units. 0 uses the radius the client asks
        /// for in 0x03 (8000 on foot).
        /// </summary>
        public int ViewDistance { get { return viewDistance; } }

        /// <summary>
        /// Seconds between saves of a player's position while they play (they are also saved on logout).
        /// </summary>
        public int SaveInterval { get { return saveInterval; } }

        /// <summary>
        /// The CMS (chat) server's game link (CMSServer.xml GameLinkPort), for GM commands typed in chat.
        /// </summary>
        public string ChatHost { get { return chatHost; } }
        public int ChatPort { get { return chatPort; } }

        /// <summary>
        /// Must match CMSServer.xml GameLinkPassword. Empty turns the link off.
        /// </summary>
        public string ChatPassword { get { return chatPassword; } }

        public string DBHost { get { return dbHost; } }
        public int DBPort { get { return dbPort; } }
        public string DBName { get { return dbName; } }
        public string DBUser { get { return dbUser; } }
        public string DBPass { get { return dbPass; } }

        public void Initialization(string path)
        {
            try
            {
                var xml = new XmlDocument();
                xml.Load(path);
                foreach (object j in xml["GameServer"].ChildNodes)
                {
                    var i = j as XmlElement;
                    if (i == null) continue;
                    switch (i.Name.ToLower())
                    {
                        case "port":
                            port = int.Parse(i.InnerText.Trim());
                            break;
                        case "loglevel":
                            loglevel = int.Parse(i.InnerText.Trim());
                            break;
                        case "checksessionkey":
                            checkSessionKey = bool.Parse(i.InnerText.Trim());
                            break;
                        case "viewdistance":
                            viewDistance = int.Parse(i.InnerText.Trim());
                            break;
                        case "saveinterval":
                            saveInterval = int.Parse(i.InnerText.Trim());
                            break;
                        case "chathost":
                            chatHost = i.InnerText.Trim();
                            break;
                        case "chatport":
                            chatPort = int.Parse(i.InnerText.Trim());
                            break;
                        case "chatpassword":
                            chatPassword = i.InnerText;
                            break;
                        case "database":
                            foreach (object l in i.ChildNodes)
                            {
                                var k = l as XmlElement;
                                if (k == null) continue;
                                switch (k.Name.ToLower())
                                {
                                    case "host":
                                        dbHost = k.InnerText.Trim();
                                        break;
                                    case "port":
                                        dbPort = int.Parse(k.InnerText.Trim());
                                        break;
                                    case "name":
                                        dbName = k.InnerText.Trim();
                                        break;
                                    case "user":
                                        dbUser = k.InnerText.Trim();
                                        break;
                                    case "password":
                                        dbPass = k.InnerText;
                                        break;
                                }
                            }
                            break;
                    }
                }
                Logger.ShowInfo("Done reading configuration...");
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }
    }
}
