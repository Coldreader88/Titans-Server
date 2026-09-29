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
            RegisterPacketHandler(GSOpcode.CM_UNKNOWN_0D, new CM_UNKNOWN_0D());
            RegisterPacketHandler(GSOpcode.CM_ITEM_INFO, new CM_ITEM_INFO());
            RegisterPacketHandler(GSOpcode.CM_MOVE_ITEM, new CM_MOVE_ITEM());
            RegisterPacketHandler(GSOpcode.CM_RESERVE_ANOTHER_GAME_FE, new CM_RESERVE_ANOTHER_GAME_FE());
            RegisterPacketHandler(GSOpcode.CM_GC_PLAYER_INFO, new CM_GC_PLAYER_INFO());
            RegisterPacketHandler(GSOpcode.CM_DELETE_ITEM, new CM_DELETE_ITEM());
            RegisterPacketHandler(GSOpcode.CM_PAY_REPAIR, new CM_PAY_REPAIR());
            RegisterPacketHandler(GSOpcode.CM_CHAIN_EXPLOSION, new CM_CHAIN_EXPLOSION());
            RegisterPacketHandler(GSOpcode.CM_CHANGE_MACHINE_OWNER, new CM_CHANGE_MACHINE_OWNER());
            RegisterPacketHandler(GSOpcode.CM_REPAIR_PLAYER, new CM_REPAIR_PLAYER());
            RegisterPacketHandler(GSOpcode.CM_UPDATE_DEPOSIT, new CM_UPDATE_DEPOSIT());
            RegisterPacketHandler(GSOpcode.CM_BUY_ITEM, new CM_BUY_ITEM());
            RegisterPacketHandler(GSOpcode.CM_ATTACK_RESULT, new CM_ATTACK_RESULT());
            RegisterPacketHandler(GSOpcode.CM_ATTACK_ITEM, new CM_ATTACK_ITEM());
            RegisterPacketHandler(GSOpcode.CM_SPACE_ITEM_LOCK, new CM_SPACE_ITEM_LOCK(GSOpcode.CM_SPACE_ITEM_LOCK));
            RegisterPacketHandler(GSOpcode.CM_SPACE_ITEM_LIST, new CM_SPACE_ITEM_LOCK(GSOpcode.CM_SPACE_ITEM_LIST));
            RegisterPacketHandler(GSOpcode.CM_PRODUCT_ITEM, new CM_PRODUCT_ITEM(GSOpcode.CM_PRODUCT_ITEM));
            RegisterPacketHandler(GSOpcode.CM_PRODUCT_DONE, new CM_PRODUCT_ITEM(GSOpcode.CM_PRODUCT_DONE));
            RegisterPacketHandler(GSOpcode.CM_TARGET_DESTROYED, new CM_TARGET_DESTROYED());
            RegisterPacketHandler(GSOpcode.CM_MULTI_ATTACK_RESULT, new CM_MULTI_ATTACK_RESULT());
            RegisterPacketHandler(GSOpcode.CM_RELAY, new CM_RELAY());
            RegisterPacketHandler(GSOpcode.CM_BROADCAST, new CM_BROADCAST());
            RegisterPacketHandler(GSOpcode.CM_EQUIP_ITEM, new CM_EQUIP_ITEM());
            RegisterPacketHandler(GSOpcode.CM_USE_ITEM_WITH_TARGET, new CM_USE_ITEM_WITH_TARGET());
            RegisterPacketHandler(GSOpcode.CM_USE_ITEM_BUFF, new CM_USE_ITEM_BUFF());
            RegisterPacketHandler(GSOpcode.CM_SELL_ITEM, new CM_SELL_ITEM());
            RegisterPacketHandler(GSOpcode.CM_SPACE_PLACED_ITEM, new CM_SPACE_PLACED_ITEM());
            RegisterPacketHandler(GSOpcode.CM_SPACE_PICKUP_ITEM, new CM_SPACE_PICKUP_ITEM());
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
