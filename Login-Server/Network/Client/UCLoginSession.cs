using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Account;
using Login_Server.Network.Packets;
using Login_Server.Network.Packets.Client;
using SmartEngine.Core;
using SmartEngine.Network;
using Common.Network.Packets;

namespace Login_Server.Network.Client
{
    public partial class UCLoginSession : Session<ISOpcode>
    {

        public override void OnConnect()
        {
            base.OnConnect();
        }

        public override void OnDisconnect()
        {
            Logger.ShowInfo("User[{0}] connected.", 0);
        }

        public void OnRequestServerStatus(CM_REQUEST_STATUS p)
        {
            try
            {

                var packet = new SM_REQUEST_STATUS();

                //Logger.ShowWarning(packet.DumpDetailedData());

                this.Network.SendPacket(packet);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

    }
}

