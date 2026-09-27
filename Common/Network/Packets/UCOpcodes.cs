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

        SM_REQUEST_LOGIN = 0x38000,
        SM_CHARACTER_LIST = 0x38001,
    }
}