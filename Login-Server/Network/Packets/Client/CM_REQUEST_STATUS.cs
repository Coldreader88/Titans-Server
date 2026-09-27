using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Network.Packets;
using Login_Server.Network.Client;
using SmartEngine.Network;

namespace Login_Server.Network.Packets.Client
{
    public class CM_REQUEST_STATUS : UCPacket<ISOpcode>
    {

        public CM_REQUEST_STATUS()
        {
            this.ID = ISOpcode.CM_REQUEST_STATUS;
        }

        public override Packet<ISOpcode> New()
        {
            return new CM_REQUEST_STATUS();
        }

        public override void OnProcess(Session<ISOpcode> client)
        {
            ((UCLoginSession)client).OnRequestServerStatus(this);
        }

    }
}
