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
        /// 日志等级
        /// </summary>
        public int LogLevel { get { return loglevel; } }

        public List<WorldInfo> Worlds { get { return worlds; } }

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

