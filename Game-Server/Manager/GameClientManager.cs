using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;
using TitansUC.GameServer.Network.Packets.Client;

namespace TitansUC.GameServer.Manager
{
    public class GameClientManager : ClientManager<GSOpcode>
    {
        static readonly GameClientManager instance = new GameClientManager();

        public static GameClientManager Instance { get { return instance; } }

        public GameClientManager()
        {
            RegisterPacketHandler(GSOpcode.CM_LOGIN_GAME, new CM_LOGIN_GAME());
            RegisterPacketHandler(GSOpcode.CM_REGISTER_PLAYER, new CM_REGISTER_PLAYER());
            RegisterPacketHandler(GSOpcode.CM_SERVER_TIME, new CM_SERVER_TIME());
            RegisterPacketHandler(GSOpcode.CM_ITEM_INFO, new CM_ITEM_INFO());
            RegisterPacketHandler(GSOpcode.CM_MOVE_ITEM, new CM_MOVE_ITEM());
            RegisterPacketHandler(GSOpcode.CM_OCCUPATION_CITY_INFO_LIST, new CM_OCCUPATION_CITY_INFO_LIST());
            RegisterPacketHandler(GSOpcode.CM_REGIST_COORD_MGR, new CM_REGIST_COORD_MGR());
            RegisterPacketHandler(GSOpcode.CM_PLAYER_COORD_DATA_LIST, new CM_PLAYER_COORD_DATA_LIST());
            RegisterPacketHandler(GSOpcode.CM_PLAYER_COORD_UPDATE, new CM_PLAYER_COORD_UPDATE());
            RegisterPacketHandler(GSOpcode.CM_SPACE_CIRCUIT_ITEM, new CM_SPACE_CIRCUIT_ITEM());
            RegisterPacketHandler(GSOpcode.CM_SIMPLE_PLAYER_INFO, new CM_SIMPLE_PLAYER_INFO());
            RegisterPacketHandler(GSOpcode.CM_PLAYER_LOOKS, new CM_PLAYER_LOOKS());
            RegisterPacketHandler(GSOpcode.CM_PAPER_DOLL_INFO, new CM_PAPER_DOLL_INFO());
            RegisterPacketHandler(GSOpcode.CM_LOGOUT_GS, new CM_LOGOUT_GS());
            RegisterPacketHandler(GSOpcode.CM_LOGOUT_GAME_FE, new CM_LOGOUT_GAME_FE());
            RegisterPacketHandler(GSOpcode.CM_CLIENT_MSG, new CM_CLIENT_MSG());
        }

        protected override Session<GSOpcode> NewSession()
        {
            return new UCGameSession();
        }
    }
}
