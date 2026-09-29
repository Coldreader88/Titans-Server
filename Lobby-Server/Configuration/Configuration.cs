using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using SmartEngine.Core;
using SmartEngine.Network.Utils;
using SmartEngine.Network;

namespace TitansUC.LobbyServer
{
    public struct WorldInfo
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public int MaxUserCount { get; set; }
    }

    public class Configuration : Singleton<Configuration>
    {
        int client_version, port, gameport, loglevel;

        string dbHost = "127.0.0.1", dbName = "titans-server", dbUser = "root", dbPass = "";
        int dbPort = 3306;
        bool autoCreateAccounts;
        string gameServerIP = "127.0.0.1";
        int gameServerPort = 42010;
        int defaultMoney = 50000;
        int staffDeleteWaitMinutes = 5;
        int playerDeleteWaitMinutes = 240;

        private List<WorldInfo> worlds = new List<WorldInfo>();

        /// <summary>
        /// returns required client version for logging in.
        /// </summary>
        public int RequiredVersion { get { return client_version; } }

        /// <summary>
        /// 登陆服务器的监听端口
        /// </summary>
        public int Port { get { return port; } }

        /// <summary>
        /// GameServer port.
        /// </summary>
        public int GamePort { get { return gameport; } }

        /// <summary>
        /// Game server address sent to the client when it enters the game (0x38005).
        /// </summary>
        public string GameServerIP { get { return gameServerIP; } }

        /// <summary>
        /// Game server port sent to the client when it enters the game (0x38005).
        /// </summary>
        public int GameServerPort { get { return gameServerPort; } }

        /// <summary>
        /// How long after creation a character of a GM or admin account (levels 4 and 9) can be deleted.
        /// </summary>
        public int StaffDeleteWaitMinutes { get { return staffDeleteWaitMinutes; } }

        /// <summary>
        /// How long after creation any other account's character can be deleted.
        /// </summary>
        public int PlayerDeleteWaitMinutes { get { return playerDeleteWaitMinutes; } }

        /// <summary>
        /// 日志等级
        /// </summary>
        public int LogLevel { get { return loglevel; } }

        public List<WorldInfo> Worlds { get { return worlds; } }

        /// <summary>
        /// MySQL server holding the accounts table.
        /// </summary>
        public string DBHost { get { return dbHost; } }
        public int DBPort { get { return dbPort; } }
        public string DBName { get { return dbName; } }
        public string DBUser { get { return dbUser; } }
        public string DBPass { get { return dbPass; } }

        /// <summary>
        /// Create an account on first login when the user name does not exist yet.
        /// </summary>
        public bool AutoCreateAccounts { get { return autoCreateAccounts; } }

        /// <summary>
        /// Money a new character starts with (Java reference: default_money in config.cfg).
        /// </summary>
        public int DefaultMoney { get { return defaultMoney; } }

        public void Initialization(string path)
        {
            XmlDocument xml = new XmlDocument();
            try
            {
                XmlElement root;
                XmlNodeList list;
                xml.Load(path);
                root = xml["LobbyServer"];
                list = root.ChildNodes;
                foreach (object j in list)
                {
                    XmlElement i;
                    if (j.GetType() != typeof(XmlElement)) continue;
                    i = (XmlElement)j;
                    switch (i.Name.ToLower())
                    {
                        case "required-client-version":
                            this.client_version = int.Parse(i.InnerText);
                            break;
                        case "port":
                            this.port = int.Parse(i.InnerText);
                            break;
                        case "loglevel":
                            this.loglevel = int.Parse(i.InnerText);
                            break;
                        case "autocreateaccounts":
                            this.autoCreateAccounts = bool.Parse(i.InnerText.Trim());
                            break;
                        case "gameserverip":
                            this.gameServerIP = i.InnerText.Trim();
                            break;
                        case "gameserverport":
                            this.gameServerPort = int.Parse(i.InnerText.Trim());
                            break;
                        case "defaultmoney":
                            this.defaultMoney = int.Parse(i.InnerText.Trim());
                            break;
                        case "staffdeletewaitminutes":
                            this.staffDeleteWaitMinutes = int.Parse(i.InnerText.Trim());
                            break;
                        case "playerdeletewaitminutes":
                            this.playerDeleteWaitMinutes = int.Parse(i.InnerText.Trim());
                            break;
                        case "database":
                            foreach (object l in i.ChildNodes)
                            {
                                XmlElement k = l as XmlElement;
                                if (k == null) continue;
                                switch (k.Name.ToLower())
                                {
                                    case "host":
                                        dbHost = k.InnerText.Trim();
                                        break;
                                    case "port":
                                        dbPort = int.Parse(k.InnerText);
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
                        case "world":
                            {
                                WorldInfo info = new WorldInfo
                                    {
                                        Address = "0.0.0.0", Name = "Unknown", MaxUserCount = 1
                                    };

                                XmlNodeList children = i.ChildNodes;
                                foreach (object l in children)
                                {
                                    XmlElement k = l as XmlElement;
                                    if (k == null) continue;
                                    switch (k.Name.ToLower())
                                    {
                                        case "name":
                                            info.Name = k.InnerText;
                                            break;
                                        case "publicip":
                                            info.Address = k.InnerText;
                                            break;
                                        case "MaxPlayerCount":
                                            info.MaxUserCount = int.Parse(k.InnerText);
                                            break;

                                        case "port":
                                            gameport = int.Parse(k.InnerText);
                                            break;
                                    }
                                }
                                worlds.Add(info);
                            }
                            break;
                    }
                }
                Logger.ShowInfo("Done reading configuration...");
                xml = null;
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }
    }
}

