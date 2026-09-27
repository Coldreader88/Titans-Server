using System;
using Common.Account;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.LobbyServer.Database;
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
            Logger.ShowInfo(string.Format("User[{0}] disconnected.", account != null ? account.UserName : "UNKNOWN"));
        }

        /// <summary>
        /// 0x30000: check the client version and credentials, then send the result.
        /// </summary>
        public void OnRequestLogin(CM_REQUEST_LOGIN p)
        {
            try
            {
                this.account = new Account
                {
                    UserName = p.User.Name,
                    Password = p.User.Password,
                    Version = p.User.Version,
                    LastLoginIP = RemoteAddress,
                    LastLoginTime = DateTime.Now,
                };

                Logger.ShowInfo(string.Format("Login request: user {0}, client version {1}", p.User.Name, p.User.Version));

                int requiredVersion = Configuration.Instance.RequiredVersion;
                if (requiredVersion > 0 && p.User.Version != requiredVersion)
                {
                    Logger.ShowInfo(string.Format("Login refused for {0}: client version {1}, expected {2}.",
                        p.User.Name, p.User.Version, requiredVersion));
                    this.account.Status = Account.AuthenticationStatus.BAD_CLIENT;
                }
                else
                {
                    AccountDatabase.Instance.Authenticate(this.account, Configuration.Instance.AutoCreateAccounts, RemoteAddress);
                }

                // The plain password is only needed to check it.
                this.account.Password = null;

                if (this.account.Authenticated)
                {
                    Logger.ShowInfo(string.Format("User {0} logged in (account {1}).", account.UserName, account.AccountID));
                }

                this.Network.SendPacket(new SM_REQUEST_LOGIN(this));
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

        /// <summary>
        /// 0x30001: send the character list. Characters are not stored yet, so it is always empty.
        /// </summary>
        public void OnRequestCharacterList(CM_REQUEST_CHARACTER_LIST p)
        {
            if (this.account == null || !this.account.Authenticated)
            {
                Logger.ShowWarning("Character list requested before a successful login, disconnecting.");
                this.Network.Disconnect();
                return;
            }

            this.Network.SendPacket(new SM_CHARACTER_LIST(this));
        }

        private string RemoteAddress
        {
            get
            {
                try
                {
                    return ((System.Net.IPEndPoint)this.Network.Socket.RemoteEndPoint).Address.ToString();
                }
                catch (Exception)
                {
                    return "0.0.0.0";
                }
            }
        }
    }
}
