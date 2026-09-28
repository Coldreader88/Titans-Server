using System.Collections.Generic;
using System.Linq;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Client;
using TitansUC.CmsServer.Network.Packets.Link;

namespace TitansUC.CmsServer.Manager
{
    /// <summary>
    /// Accepts the game servers (Java: CGServer on 24021; here CMSServer.xml GameLinkPort, which the
    /// game servers find as GameServer.xml ChatHost / ChatPort). GM commands that act in the game world
    /// go to every connected game server; a game server ignores players it does not have.
    /// </summary>
    public class GameLinkManager : ClientManager<CGOpcode>
    {
        static readonly GameLinkManager instance = new GameLinkManager();

        public static GameLinkManager Instance { get { return instance; } }

        public GameLinkManager()
        {
            RegisterPacketHandler(CGOpcode.GS_LINK_HELLO, new GS_LINK_HELLO());
            RegisterPacketHandler(CGOpcode.GS_NPC_CHAT, new GS_NPC_CHAT());
            RegisterPacketHandler(CGOpcode.GS_SYSTEM_MESSAGE, new GS_SYSTEM_MESSAGE());
            RegisterPacketHandler(CGOpcode.GS_PLAYER_SYSTEM_MESSAGE, new GS_PLAYER_SYSTEM_MESSAGE());
        }

        protected override Session<CGOpcode> NewSession()
        {
            return new GameLinkSession();
        }

        private List<GameLinkSession> GameServers
        {
            get
            {
                lock (Clients)
                {
                    return Clients.OfType<GameLinkSession>().Where(s => s.Authenticated).ToList();
                }
            }
        }

        public bool Connected { get { return GameServers.Count > 0; } }

        public bool Teleport(uint characterID, int x, int y, int z)
        {
            return SendToAll(() => GameLink.Teleport(characterID, x, y, z));
        }

        public bool TeleportTo(uint fromID, uint toID)
        {
            return SendToAll(() => GameLink.TeleportTo(fromID, toID));
        }

        public bool Spawn(uint characterID, IEnumerable<string> args)
        {
            var list = args.ToList();
            return SendToAll(() => GameLink.Spawn(characterID, list));
        }

        public bool GmCommand(uint characterID, string name, IEnumerable<string> args)
        {
            var list = args.ToList();
            return SendToAll(() => GameLink.GmCommand(characterID, name, list));
        }

        public bool PositionLog(uint characterID, string message)
        {
            return SendToAll(() => GameLink.PositionLog(characterID, message));
        }

        public bool Closure(int seconds)
        {
            return SendToAll(() => GameLink.Closure(seconds));
        }

        public bool EndMaintenance()
        {
            return SendToAll(GameLink.EndMaintenance);
        }

        private bool SendToAll(System.Func<Packet<CGOpcode>> make)
        {
            var servers = GameServers;
            foreach (var server in servers)
            {
                server.Send(make());
            }
            return servers.Count > 0;
        }
    }
}
