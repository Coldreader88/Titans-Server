using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using SmartEngine.Core;
using SmartEngine.Network.Utils;
using SmartEngine.Network;

namespace LoginServer
{
    public struct WorldInfo
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }

    public class Configuration : Singleton<Configuration>
    {
        int client_version, port, loglevel;

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
        /// 日志等级
        /// </summary>
        public int LogLevel { get { return loglevel; } }

        public List<WorldInfo> Worlds { get { return worlds; } }

        /// <summary>
        /// Reads an integer setting, ignoring surrounding whitespace and quotes.
        /// Keeps the current value and logs a warning when the setting is not a number.
        /// </summary>
        static int ParseInt(XmlElement element, int fallback)
        {
            string text = element.InnerText.Trim().Trim('"', '\'').Trim();
            int value;
            if (int.TryParse(text, out value))
                return value;
            Logger.ShowWarning("Config setting <{0}> has invalid number \"{1}\", using {2}", element.Name, element.InnerText, fallback);
            return fallback;
        }

        public void Initialization(string path)
        {
            XmlDocument xml = new XmlDocument();
            try
            {
                XmlElement root;
                XmlNodeList list;
                xml.Load(path);
                root = xml["LoginServer"];
                list = root.ChildNodes;
                foreach (object j in list)
                {
                    XmlElement i;
                    if (j.GetType() != typeof(XmlElement)) continue;
                    i = (XmlElement)j;
                    switch (i.Name.ToLower())
                    {
                        case "required-client-version":
                            this.client_version = ParseInt(i, this.client_version);
                            break;
                        case "port":
                            this.port = ParseInt(i, this.port);
                            break;
                        case "loglevel":
                            this.loglevel = ParseInt(i, this.loglevel);
                            break;
                        case "world":
                            {
                                WorldInfo info = new WorldInfo
                                    {
                                        Address = "0.0.0.0", Name = "Unknown"
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
                                        case "publicaddress":
                                            info.Address = k.InnerText;
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

