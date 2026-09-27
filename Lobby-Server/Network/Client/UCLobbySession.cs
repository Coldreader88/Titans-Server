using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Account;
using Common.Network;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Packets.Client;

namespace TitansUC.LobbyServer.Network.Client
{
    public partial class UCLobbySession : Session<LSOpcode>
    {

        public Account account { get; set; }

        public override void OnConnect()
        {
            base.OnConnect();

            this.account = new Account();
        }

        public override void OnDisconnect()
        {
            Logger.ShowInfo("User[{0}] disconnected.", 0);
        }

        public void OnRequestLogin(CM_REQUEST_LOGIN p)
        {
            try
            {
                this.account = new Account();

                Logger.ShowInfo("{0}, {1}, {2}", p.User.Name, p.User.Password, p.User.Version);
                this.account.UserName = p.User.Name;
                this.account.Password = p.User.Password;
                this.account.AccountID = 0x00034C18;
                this.account.GMLevel = 0x09;
                this.account.Version = p.User.Version;

                if (p.User.Name.ToLower().StartsWith("t"))
                {
                    this.account.Status = Account.AuthenticationStatus.TEST;
                }
                else
                {
                    this.account.Status = Account.AuthenticationStatus.SUCCESS;
                }


                var packet = new SM_REQUEST_LOGIN(this);

                //Logger.ShowWarning(packet.DumpData());

                var net = (UCNetwork<LSOpcode>)this.Network;

                net.SendPacket(packet);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex.StackTrace);
            }
        }

    }
}
