using Common.Account;
using Common.Network.Packets;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38000: the login result.
    ///
    /// <code>
    /// Success:  uint32 BE 1, uint32 BE session key, uint32 BE account id, uint32 BE account level (GM tag)
    /// Failure:  uint32 BE error code, 0xFFFFFFFF, 0xFFFFFFFF, uint32 BE 0
    /// </code>
    /// Java reference: mina_loginserver NotifyUserInfo.java, which sent 0 as the session key; the official
    /// server sent a random key (UCGOZone-Login.pcap), which the client sends back in 0x30005 and 0x41.
    /// </summary>
    public class SM_REQUEST_LOGIN : UCPacket<LSOpcode>
    {
        public SM_REQUEST_LOGIN(UCLobbySession client)
        {
            this.ID = LSOpcode.SM_REQUEST_LOGIN;

            var account = client.account;

            this.PutUIntBE((uint)account.Status);

            if (account.Status == Account.AuthenticationStatus.SUCCESS)
            {
                this.PutUIntBE(account.SessionKey);
                this.PutUIntBE(account.AccountID);
                this.PutUIntBE(account.GMLevel);
            }
            else
            {
                this.PutUIntBE(0xFFFFFFFF);
                this.PutUIntBE(0xFFFFFFFF);
                this.PutUIntBE(0);
            }
        }
    }
}
