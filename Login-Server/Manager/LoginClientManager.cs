using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Network.Packets;
using Login_Server.Network.Client;
using Login_Server.Network.Packets.Client;
using SmartEngine.Network;

namespace Login_Server.Manager
{
    public class LoginClientManager : ClientManager<ISOpcode>
    {
        static LoginClientManager instance = new LoginClientManager();

        public static LoginClientManager Instance { get { return instance; } }

        public Status Status { get; set; }

        public LoginClientManager()
        {
            RegisterPacketHandler(ISOpcode.CM_REQUEST_STATUS, new CM_REQUEST_STATUS());


            this.Status = Status.ONLINE;
        }

        protected override Session<ISOpcode> NewSession()
        {
            return new UCLoginSession();
        }
    }
}
