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
        CM_SERVER_TIME = 0x13,
        CM_ITEM_INFO = 0x16,
        CM_CLIENT_MSG = 0x37,
        CM_REGISTER_PLAYER = 0x38,
        CM_LOGIN_GAME = 0x41,
        CM_LOGOUT_GAME_FE = 0x42,
        CM_LOGOUT_GS = 0x55,
        CM_PAPER_DOLL_INFO = 0x6F,
        CM_OCCUPATION_CITY_INFO_LIST = 0x70,

        SM_REGIST_COORD_MGR = 0x8000,
        SM_PLAYER_COORD_DATA_LIST = 0x8003,
        SM_SPACE_CIRCUIT_ITEM = 0x8005,
        SM_SIMPLE_PLAYER_INFO = 0x8006,
        SM_PLAYER_LOOKS = 0x800A,
        SM_SERVER_TIME = 0x8013,
        SM_ITEM_INFO = 0x8016,
        SM_REGISTER_PLAYER = 0x8038,
        SM_LOGIN_GAME = 0x8041,
        SM_LOGOUT_GAME_FE = 0x8042,
        SM_LOGOUT_GS = 0x8055,
        SM_PAPER_DOLL_INFO = 0x806F,
        SM_OCCUPATION_CITY_INFO_LIST = 0x8070,
    }
}