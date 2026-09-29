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
        int port = 42010, loglevel = 31, viewDistance, saveInterval = 60, npcAggroRange = 1500;
        bool checkSessionKey = true;
        string dbHost = "127.0.0.1", dbName = "titans-server", dbUser = "root", dbPass = "";
        int dbPort = 3306;
        string chatHost = "127.0.0.1", chatPassword = "";
        int chatPort = 10241;
        string transferHost = "127.0.0.1";
        ushort zone = ZoneEarth;

        public const ushort ZoneEarth = 1;
        public const ushort ZoneSpace = 2;

        /// <summary>
        /// The Earth (ground) server's port; the Space server listens on the next one, as the Java server
        /// had them (24010 and 24011). The Lobby sends the client to one or the other by the character's zone
        /// (LobbyServer.xml GameServerPort).
        /// </summary>
        public int Port { get { return port; } }

        /// <summary>
        /// Which world this process runs: <see cref="ZoneEarth"/> (the default, "-instance=ground") or
        /// <see cref="ZoneSpace"/> ("-instance=space"), like the Java server's SPACE argument.
        /// </summary>
        public ushort Zone { get { return zone; } }

        public bool IsSpace { get { return zone == ZoneSpace; } }

        public string InstanceName { get { return IsSpace ? "Space" : "Earth"; } }

        /// <summary>
        /// The port this process listens on: <see cref="Port"/> on Earth, <see cref="Port"/> + 1 in Space.
        /// </summary>
        public int ListenPort { get { return IsSpace ? port + 1 : port; } }

        /// <summary>
        /// The port of the other side's server, where a shuttle flight sends the client (0x8040).
        /// </summary>
        public int TransferPort { get { return IsSpace ? port : port + 1; } }

        /// <summary>
        /// Reads "-instance=ground|earth|space" from the command line (ground when missing).
        /// </summary>
        public void SetInstance(string[] args)
        {
            foreach (var arg in args ?? new string[0])
            {
                var a = arg.Trim().TrimStart('-', '/').ToLowerInvariant();
                if (a.StartsWith("instance=") || a.StartsWith("instance:"))
                {
                    var value = a.Substring(9);
                    zone = value == "space" || value == "2" ? ZoneSpace : ZoneEarth;
                }
                else if (a == "space")
                {
                    zone = ZoneSpace;
                }
            }
        }

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
        /// Hostile NPCs attack pilots of the other faction who come this close (position units); 0 = they only
        /// fire back when attacked, as the official captures show.
        /// </summary>
        public int NpcAggroRange { get { return npcAggroRange; } }

        /// <summary>
        /// The CMS (chat) server's game link (CMSServer.xml GameLinkPort), for GM commands typed in chat.
        /// </summary>
        public string ChatHost { get { return chatHost; } }
        public int ChatPort { get { return chatPort; } }

        /// <summary>
        /// Must match CMSServer.xml GameLinkPassword. Empty turns the link off.
        /// </summary>
        public string ChatPassword { get { return chatPassword; } }

        /// <summary>
        /// The other side's server address as the client sees it, where a shuttle flight between Earth and
        /// Space reconnects the client (0x8040), on <see cref="TransferPort"/>.
        /// </summary>
        public string TransferHost { get { return transferHost; } }

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
                        case "wreckchance":
                            World.Combat.WreckChance = Math.Max(0, Math.Min(100, int.Parse(i.InnerText.Trim())));
                            break;
                        case "skilltotalcap":
                            World.SkillGrowth.TotalCap = Math.Max(0, int.Parse(i.InnerText.Trim()));
                            break;
                        case "statuscap":
                            World.SkillGrowth.StatusCap = Math.Max(0, int.Parse(i.InnerText.Trim()));
                            break;
                        case "skillgainrate":
                            World.SkillGrowth.GainRate = Math.Max(0, int.Parse(i.InnerText.Trim()));
                            break;
                        case "crimeexilecount":
                            World.Criminal.ExileCount = Math.Max(0, int.Parse(i.InnerText.Trim()));
                            break;
                        case "crimeexileearth":
                            World.Criminal.ExileEarth = World.Criminal.ParsePoint(i.InnerText);
                            break;
                        case "crimeexilespace":
                            World.Criminal.ExileSpace = World.Criminal.ParsePoint(i.InnerText);
                            break;
                        case "viewdistance":
                            viewDistance = int.Parse(i.InnerText.Trim());
                            break;
                        case "saveinterval":
                            saveInterval = int.Parse(i.InnerText.Trim());
                            break;
                        case "npcaggrorange":
                            npcAggroRange = int.Parse(i.InnerText.Trim());
                            break;
                        case "chathost":
                            chatHost = i.InnerText.Trim();
                            break;
                        case "chatport":
                            chatPort = int.Parse(i.InnerText.Trim());
                            break;
                        case "transferhost":
                            transferHost = i.InnerText.Trim();
                            break;
                        case "transferport":
                            // Old setting: the other server's port is now always Port or Port + 1.
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
