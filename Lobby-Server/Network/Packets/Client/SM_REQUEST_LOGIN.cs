using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    public class SM_REQUEST_LOGIN : UCPacket<LSOpcode>
    {
        private UCLobbySession client = null;

        public SM_REQUEST_LOGIN(UCLobbySession client)
        {
            this.ID = LSOpcode.SM_REQUEST_LOGIN;

            this.client = client;

            this.BuildResponse();
        }

        private void BuildResponse()
        {
            //00 00 00 15 FF FF FF FF FF FF FF FF 00 00 00 00

            try
            {

                switch (client.account.Status)
                {
                    case Common.Account.Account.AuthenticationStatus.SUCCESS:
                        {

                            this.PutUInt((uint)client.account.Status, true);
                            this.PutInt(client.Network.Socket.Handle.ToInt32(), true); //login ticket?
                            this.PutUInt(client.account.AccountID, true);
                            this.PutUInt(client.account.GMLevel, true);
                            break;
                        }

                    case Common.Account.Account.AuthenticationStatus.WRONG_VERSION:
                    case Common.Account.Account.AuthenticationStatus.BLOCKED:
                    case Common.Account.Account.AuthenticationStatus.BAD_CREDENTIALS:
                    default:
                        {
                            this.PutUInt((uint)client.account.Status - 1, true);
                            this.PutLong(-1);
                            this.PutUInt(0);
                            break;
                        }
                }

                //Logger.ShowWarning(DumpData2());

            }catch(Exception e)
            {
                Logger.ShowError(e);
            }

        }
    }
}
