using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Network.Packets
{
    public enum ISOpcode
    {
        CM_REQUEST_STATUS,
        SM_REQUEST_STATUS = 0x00008000,
    }

    /// <summary>
    /// Opcodes of the account login server (C# Lobby-Server, Java "Login" server on 24018).
    /// Replies are the request opcode + 0x8000.
    /// </summary>
    public enum LSOpcode
    {
        CM_REQUEST_LOGIN = 0x30000,
        CM_REQUEST_CHARACTER_LIST = 0x30001,
        CM_REQUEST_PLAYER_INFO = 0x30002,
        CM_REQUEST_CREATE_CHARACTER = 0x30003,
        CM_REQUEST_DELETE_CHARACTER = 0x30004,
        CM_REQUEST_GAME_SERVER = 0x30005,

        SM_REQUEST_LOGIN = 0x38000,
        SM_CHARACTER_LIST = 0x38001,
        SM_PLAYER_INFO = 0x38002,
        SM_CREATE_CHARACTER = 0x38003,
        SM_DELETE_CHARACTER = 0x38004,
        SM_GAME_SERVER = 0x38005,
    }

    /// <summary>
    /// Opcodes of the game server (C# Game-Server, Java Earth/Space server on 24010/24011).
    /// Replies are the request opcode + 0x8000. Names follow the Java handlers (mina_gameserver GameOpcodeMap).
    /// </summary>
    public enum GSOpcode
    {
        CM_REGIST_COORD_MGR = 0x00,
        CM_PLAYER_COORD_UPDATE = 0x02,
        CM_PLAYER_COORD_DATA_LIST = 0x03,
        CM_SPACE_CIRCUIT_ITEM = 0x05,
        CM_SIMPLE_PLAYER_INFO = 0x06,
        CM_PLAYER_LOOKS = 0x0A,
        CM_DECREMENT_CRIMINAL_COUNT = 0x08,
        CM_CHANGE_SKILL_MANAGEMENT = 0x0B,
        CM_CHANGE_STATUS_MANAGEMENT = 0x0C,
        CM_GROW_BATTLE_SKILL = 0x0D,
        CM_ATTACK_RESULT = 0x0F,
        CM_ATTACK_ITEM = 0x11,
        CM_CHAIN_EXPLOSION = 0x12,
        CM_SERVER_TIME = 0x13,
        CM_DELETE_ITEM = 0x15,
        CM_ITEM_INFO = 0x16,
        CM_MOVE_ITEM = 0x17,
        CM_PAY_REPAIR = 0x18,
        CM_UPDATE_DEPOSIT = 0x19,
        CM_EQUIP_ITEM = 0x1B,
        CM_USE_ITEM_BUFF = 0x1C,
        CM_USE_ITEM_WITH_TARGET = 0x1D,
        CM_BUY_ITEM = 0x21,
        CM_SELL_ITEM = 0x22,
        CM_SPACE_PLACED_ITEM = 0x23,
        CM_SPACE_PICKUP_ITEM = 0x24,
        CM_CHANGE_MACHINE_OWNER = 0x25,
        CM_SPACE_ITEM_LOCK = 0x26,
        CM_SPACE_ITEM_LIST = 0x27,
        CM_PRODUCT_ITEM = 0x28,
        CM_PRODUCT_DONE = 0x29,
        CM_EXCAVATION = 0x32,
        CM_COMPLETE_QUEST = 0x3E,
        CM_CLIENT_MSG = 0x37,
        CM_REGISTER_PLAYER = 0x38,
        CM_RELAY = 0x39,
        CM_BROADCAST = 0x3A,
        CM_LOGIN_GAME = 0x41,
        CM_LOGOUT_GAME_FE = 0x42,
        CM_RESERVE_ANOTHER_GAME_FE = 0x40,
        CM_GC_PLAYER_INFO = 0x5F,
        CM_LOGOUT_GS = 0x55,
        CM_MULTI_ATTACK_RESULT = 0x67,
        CM_REPAIR_PLAYER = 0x69,
        CM_TARGET_DESTROYED = 0x6D,
        CM_PAPER_DOLL_INFO = 0x6F,
        CM_OCCUPATION_CITY_INFO_LIST = 0x70,
        CM_START_OCCUPATION = 0x71,
        CM_CAPTURE_FLAG = 0x73,
        CM_REGISTER_OCCUPATION = 0x74,
        CM_UNREGISTER_OCCUPATION = 0x75,

        SM_REGIST_COORD_MGR = 0x8000,
        SM_PLAYER_COORD_DATA_LIST = 0x8003,
        SM_SPACE_CIRCUIT_ITEM = 0x8005,
        SM_SIMPLE_PLAYER_INFO = 0x8006,
        SM_PLAYER_LOOKS = 0x800A,
        SM_ATTACK_RESULT = 0x800F,
        SM_LOCK_ON = 0x8010,
        SM_ATTACK_ITEM = 0x8011,
        SM_CHAIN_EXPLOSION = 0x8012,
        SM_UPDATE_CRIMINAL_COUNT = 0x8008,
        SM_CHANGE_SKILL_MANAGEMENT = 0x800B,
        SM_CHANGE_STATUS_MANAGEMENT = 0x800C,
        SM_GROW_BATTLE_SKILL = 0x800D,
        SM_SERVER_TIME = 0x8013,
        SM_DELETE_ITEM = 0x8015,
        SM_ITEM_INFO = 0x8016,
        SM_MOVE_ITEM = 0x8017,
        SM_PAY_REPAIR = 0x8018,
        SM_UPDATE_DEPOSIT = 0x8019,
        SM_EQUIP_ITEM = 0x801B,
        SM_USE_ITEM_BUFF = 0x801C,
        SM_USE_ITEM_WITH_TARGET = 0x801D,
        SM_BUY_ITEM = 0x8021,
        SM_SELL_ITEM = 0x8022,
        SM_SPACE_PLACED_ITEM = 0x8023,
        SM_SPACE_PICKUP_ITEM = 0x8024,
        SM_CHANGE_MACHINE_OWNER = 0x8025,
        SM_SPACE_ITEM_LOCK = 0x8026,
        SM_SPACE_ITEM_LIST = 0x8027,
        SM_PRODUCT_ITEM = 0x8028,
        SM_PRODUCT_DONE = 0x8029,
        SM_EXCAVATION = 0x8032,
        SM_COMPLETE_QUEST = 0x803E,
        SM_SKILL_GAIN = 0x8034,
        SM_UPDATE_ITEM_INFO = 0x8035,
        SM_ATTACK_RESULT_NEAR = 0x8036,
        SM_REGISTER_PLAYER = 0x8038,
        SM_BROADCAST = 0x803B,
        SM_COMPULSION_MOVE = 0x803C,
        SM_EXILE_PLAYER = 0x803D,
        SM_LOGIN_GAME = 0x8041,
        SM_LOGOUT_GAME_FE = 0x8042,
        SM_RESERVE_ANOTHER_GAME_FE = 0x8040,
        SM_GC_PLAYER_INFO = 0x805F,
        SM_FORCE_LOGOUT = 0x8052,
        SM_LOGOUT_GS = 0x8055,
        SM_MULTI_ATTACK_RESULT = 0x8067,
        SM_MULTI_ATTACK_RESULT_NEAR = 0x8068,
        SM_REPAIR_PLAYER = 0x8069,
        SM_REPAIR_PLAYER_NEAR = 0x806A,
        SM_PAPER_DOLL_INFO = 0x806F,
        SM_OCCUPATION_CITY_INFO_LIST = 0x8070,
        SM_START_OCCUPATION = 0x8071,
        SM_CAPTURE_FLAG = 0x8073,
        SM_REGISTER_OCCUPATION = 0x8074,
        SM_UNREGISTER_OCCUPATION = 0x8075,
        SM_OCCUPATION_EVENT = 0x8076,
    }

    /// <summary>
    /// Opcodes of the CMS server (chat, friends, teams and group chat; C# Cms-Server, Java CMS server on
    /// 24016). Replies are the request opcode + 0x8000. Layouts come from the official captures in
    /// UCGO Packet Logs.zip (see the packet classes in Cms-Server) and the Java mina_cmsserver handlers.
    ///
    /// Packets one client sends another (invitations and their answers) travel inside CM_RELAY and reach
    /// the other client under their own opcode: 0x15, 0x16, 0x1E, 0x1F, 0x25, 0x26, 0x8014, 0x801C, 0x8022.
    /// </summary>
    public enum CMSOpcode
    {
        CM_LOGIN_CMS = 0x01,
        CM_LOGOUT_CMS = 0x02,
        CM_CHAT_MSG = 0x03,
        CM_HEARTBEAT = 0x04,
        CM_RELAY = 0x07,
        CM_TEAM_NAME = 0x08,
        CM_TEAM_MEMBER_STATUS = 0x0A,
        CM_FRIEND_STATUS = 0x0B,
        CM_CREATE_TEAM = 0x0D,
        CM_TEAM_INFO = 0x0E,
        CM_TEAM_ADD_MEMBER = 0x0F,
        CM_TEAM_LEAVE = 0x10,
        CM_TEAM_KICK = 0x12,
        CM_CHAT_INFO = 0x13,
        CM_TEAM_REGISTER_ONLINE = 0x14,
        CM_GROUP_CHAT_CREATE = 0x17,
        CM_GROUP_CHAT_MEMBERS = 0x18,
        CM_GROUP_CHAT_ADD_MEMBER = 0x19,
        CM_GROUP_CHAT_LEAVE = 0x1A,
        CM_GROUP_CHAT_RELEASE = 0x1B,
        CM_COMMUNITY_LIST = 0x20,
        CM_ADD_FRIEND = 0x21,
        CM_DELETE_FRIEND = 0x22,
        CM_FRIENDS_REGISTER_ONLINE = 0x24,

        // Relayed between clients (see CM_RELAY).
        RELAY_TEAM_INVITE = 0x15,
        RELAY_TEAM_INVITE_CANCEL = 0x16,
        RELAY_GROUP_CHAT_INVITE = 0x1E,
        RELAY_GROUP_CHAT_INVITE_CANCEL = 0x1F,
        RELAY_FRIEND_REQUEST = 0x25,
        RELAY_FRIEND_REQUEST_CANCEL = 0x26,
        RELAY_TEAM_INVITE_ANSWER = 0x8014,
        RELAY_GROUP_CHAT_INVITE_ANSWER = 0x801C,
        RELAY_FRIEND_REQUEST_ANSWER = 0x8022,

        SM_PING = 0x00,
        SM_LOGIN_CMS = 0x8001,
        SM_LOGOUT_CMS = 0x8002,
        SM_CHAT_MSG = 0x8004,
        SM_HEARTBEAT = 0x8005,
        SM_TEAM_NAME = 0x8007,
        SM_TEAM_MEMBER_STATUS = 0x8009,
        SM_FRIEND_STATUS = 0x800A,
        SM_CREATE_TEAM = 0x800C,
        SM_TEAM_INFO = 0x800D,
        SM_TEAM_MEMBER_JOINED = 0x800E,
        SM_TEAM_MEMBER_LEFT = 0x800F,
        SM_TEAM_MEMBER_KICKED = 0x8011,
        SM_CHAT_INFO = 0x8012,
        SM_TEAM_ONLINE = 0x8013,
        SM_GROUP_CHAT_CREATE = 0x8015,
        SM_GROUP_CHAT_MEMBERS = 0x8016,
        SM_GROUP_CHAT_MEMBER_JOINED = 0x8017,
        SM_GROUP_CHAT_MEMBER_LEFT = 0x8018,
        SM_COMMUNITY_LIST = 0x801D,
        SM_FRIEND_ADDED = 0x801E,
        SM_FRIEND_DELETED = 0x801F,
        SM_FRIEND_ONLINE = 0x8021,
    }

    /// <summary>
    /// Opcodes of the internal link between the CMS server and the game servers (Java: CGServer on 24021
    /// in mina_cmsserver, GCClient in mina_gameserver). The game server connects to the CMS server
    /// (GameServer.xml ChatHost / ChatPort) and must send LINK_HELLO with the shared password first.
    /// The same numbers are used in both directions, so the names say who sends them.
    /// </summary>
    public enum CGOpcode
    {
        // CMS server -> game server (Java mina_gameserver cluster/cms/GCOpcodeMap).
        CMS_TELEPORT = 0x00,
        CMS_SPAWN = 0x01,
        CMS_CLOSURE = 0x02,
        CMS_END_MAINTENANCE = 0x03,
        CMS_POSITION_LOG = 0x06,
        CMS_TELEPORT_TO_PLAYER = 0x08,
        CMS_RUN_SCRIPT = 0x09,
        CMS_GM_COMMAND = 0x0A,

        // Game server -> CMS server (Java mina_cmsserver cluster/CGOpcodeMap).
        GS_NPC_CHAT = 0x01,
        GS_SYSTEM_MESSAGE = 0x5A,
        GS_PLAYER_SYSTEM_MESSAGE = 0x5B,
        GS_LINK_HELLO = 0x7F,
    }
}
