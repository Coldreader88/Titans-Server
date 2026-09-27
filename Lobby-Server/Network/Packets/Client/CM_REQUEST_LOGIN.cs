using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Network.Encryption.UCGO.Blowfish;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using TitansUC.LobbyServer.Network.Client;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    public class UCUser
    {
        public UCUser(string name = "NULL", string password = "NULL", uint version = 0)
        {
            Name = name;
            Password = password;
            Version = version;
        }

        public string Name { get; set; }
        public string Password { get; set; }
        public uint Version { get; set; }
    }

    public class CM_REQUEST_LOGIN : UCPacket<LSOpcode>
    {
        private UCUser _user = new UCUser();

        public CM_REQUEST_LOGIN()
        {
            this.ID = LSOpcode.CM_REQUEST_LOGIN;
        }

        public override Packet<LSOpcode> New()
        {
            return new CM_REQUEST_LOGIN();
        }

        public override void OnProcess(Session<LSOpcode> client)
        {
            //Logger.ShowWarning(DumpData2());

            User.Name = this.GetUCString();

            Logger.ShowInfo("UserName: {0}", User.Name);

            User.Version = this.GetUInt(true);

            Logger.ShowInfo("Version: {0}", User.Version);

            User.Password = this.GetUCString();

            Logger.ShowInfo("Password: {0}", User.Password);

            ((UCLobbySession)client).OnRequestLogin(this);
        }

        public UCUser User
        {
            /*get
            {

                user.Name = this.GetUCString();
                user.Version = this.GetUInt();

                var pSize = this.GetByte() - 0x80;
                var pBytes = this.GetBytes((ushort)pSize);

                var blf = new Blowfish(user.Name);

                blf.Decrypt(pBytes, 0, pBytes.Length);

                user.Password = Encoding.Unicode.GetString(pBytes);

                user.Name = user.Name.Trim();
                user.Password = user.Password.Trim();

                return user;
            }*/

            get { return _user; }

        }

    }
}
