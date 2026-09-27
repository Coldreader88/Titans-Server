using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;
using TitansUC.LobbyServer.Network.Packets.Client;

namespace TitansUC.LobbyServer.Manager
{
    public class LobbyClientManager : ClientManager<LSOpcode>
    {
        static LobbyClientManager instance = new LobbyClientManager();

        public static LobbyClientManager Instance { get { return instance; } }

        public LobbyClientManager()
        {
            RegisterPacketHandler(LSOpcode.CM_REQUEST_LOGIN, new CM_REQUEST_LOGIN());
            RegisterPacketHandler(LSOpcode.CM_REQUEST_CHARACTER_LIST, new CM_REQUEST_CHARACTER_LIST());
            RegisterPacketHandler(LSOpcode.CM_REQUEST_PLAYER_INFO, new CM_REQUEST_PLAYER_INFO());
            RegisterPacketHandler(LSOpcode.CM_REQUEST_CREATE_CHARACTER, new CM_REQUEST_CREATE_CHARACTER());

        }

        protected override Session<LSOpcode> NewSession()
        {
            return new UCLobbySession();
        }
    }
}
