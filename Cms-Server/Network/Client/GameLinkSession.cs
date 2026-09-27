using System;
using System.Security.Cryptography;
using System.Text;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.CmsServer.Network.Packets.Server;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Network.Client
{
    /// <summary>
    /// A game server connected to the CMS server (Java: the CGServer sessions). It must send the link
    /// password first; anything else before that closes the connection.
    /// </summary>
    public class GameLinkSession : Session<CGOpcode>
    {
        public bool Authenticated { get; private set; }

        public void Send(Packet<CGOpcode> p)
        {
            var network = this.Network;
            if (Authenticated && network != null && !network.Disconnected)
            {
                network.SendPacket(p);
            }
        }

        public override void OnDisconnect()
        {
            if (Authenticated)
            {
                Logger.ShowWarning("A game server disconnected from the CMS server.");
            }
        }

        public void OnHello(string password)
        {
            if (!SameText(password, Configuration.Instance.GameLinkPassword))
            {
                Logger.ShowWarning("A game server sent the wrong link password (CMSServer.xml GameLinkPassword, GameServer.xml ChatPassword); disconnecting it.");
                this.Network.Disconnect();
                return;
            }

            Authenticated = true;
            Logger.ShowInfo("A game server connected to the CMS server.");
        }

        public void OnNpcChat(uint npcID, string message)
        {
            if (!CheckAuthenticated())
            {
                return;
            }
            foreach (var session in CmsWorld.Instance.Players)
            {
                session.Send(new SM_CHAT_MSG(npcID, message, ChatType.Say, session.CharacterID));
            }
        }

        public void OnSystemMessage(uint characterID, string message)
        {
            if (!CheckAuthenticated())
            {
                return;
            }

            if (characterID == 0)
            {
                CmsWorld.Instance.SystemMessage(message);
                return;
            }

            var session = CmsWorld.Instance.Get(characterID);
            if (session != null)
            {
                CmsWorld.Instance.SystemMessage(session, message);
            }
        }

        private bool CheckAuthenticated()
        {
            if (!Authenticated)
            {
                Logger.ShowWarning("A game server sent a packet before the link password; disconnecting it.");
                this.Network.Disconnect();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Compares in constant time, so the password cannot be guessed from response times.
        /// </summary>
        private static bool SameText(string a, string b)
        {
            using (var sha = SHA256.Create())
            {
                var x = sha.ComputeHash(Encoding.UTF8.GetBytes(a ?? string.Empty));
                var y = sha.ComputeHash(Encoding.UTF8.GetBytes(b ?? string.Empty));
                int diff = 0;
                for (int i = 0; i < x.Length; i++)
                {
                    diff |= x[i] ^ y[i];
                }
                return diff == 0;
            }
        }
    }
}
