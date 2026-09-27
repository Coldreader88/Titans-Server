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

    public enum LSOpcode
    {
        CM_REQUEST_LOGIN = 0x30000,
        SM_REQUEST_LOGIN = 0x38000,
    }
}