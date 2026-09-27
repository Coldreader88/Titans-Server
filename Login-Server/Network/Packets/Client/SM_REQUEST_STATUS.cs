using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Network.Packets;
using Login_Server.Manager;
using Login_Server.Network.Client;
using SmartEngine.Network;

namespace Login_Server.Network.Packets.Client
{
    public class SM_REQUEST_STATUS : UCPacket<ISOpcode>
    {

        public SM_REQUEST_STATUS()
        {
            this.ID = ISOpcode.SM_REQUEST_STATUS;
            this.Status = LoginClientManager.Instance.Status;
        }

        public Status Status
        {
            set
            {
                this.PutInt(0);
                this.PutInt((int)value, true);
            }
        }

    }
}
