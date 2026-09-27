using System;
using System.Linq;
using System.Xml;
using SmartEngine.Core;
using SmartEngine.Network;

namespace TitansUC.CmsServer
{
    /// <summary>
    /// Settings from Config/CMSServer.xml.
    /// </summary>
    public class Configuration : Singleton<Configuration>
    {
        int port = 42016, loglevel = 31, gameLinkPort = 10241;
        bool checkLoginSession = true;
        string welcome = string.Empty, gameLinkPassword = string.Empty;
        string dbHost = "127.0.0.1", dbName = "titans-server", dbUser = "root", dbPass = "";
        int dbPort = 3306;

        /// <summary>
        /// Port the game client connects to (Java: 24016).
        /// </summary>
        public int Port { get { return port; } }

        public int LogLevel { get { return loglevel; } }

        /// <summary>
        /// Only accept characters the Lobby handed to the game server in their account's current session.
        /// The CMS login carries no session key, so this is the only check there is. Turn off only for testing.
        /// </summary>
        public bool CheckLoginSession { get { return checkLoginSession; } }

        /// <summary>
        /// System message sent to each player after logging in (empty for none). One message per line.
        /// </summary>
        public string Welcome { get { return welcome; } }

        /// <summary>
        /// Port the game servers connect to (GameServer.xml ChatPort).
        /// </summary>
        public int GameLinkPort { get { return gameLinkPort; } }

        /// <summary>
        /// Password the game servers must send first (GameServer.xml ChatPassword).
        /// </summary>
        public string GameLinkPassword { get { return gameLinkPassword; } }

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
                foreach (object j in xml["ChatServer"].ChildNodes)
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
                        case "checkloginsession":
                            checkLoginSession = bool.Parse(i.InnerText.Trim());
                            break;
                        case "welcome":
                            welcome = string.Join("\n", i.InnerText.Replace("\r", string.Empty).Split('\n')
                                .Select(l => l.Trim()).Where(l => l.Length > 0));
                            break;
                        case "gamelinkport":
                            gameLinkPort = int.Parse(i.InnerText.Trim());
                            break;
                        case "gamelinkpassword":
                            gameLinkPassword = i.InnerText;
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
