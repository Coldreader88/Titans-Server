using System;
using System.Text;
using Common.Network.Encryption.UCGO.Blowfish;
using Common.Network.Packets;
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

    /// <summary>
    /// 0x30000: the client sends the user name, client version and password.
    ///
    /// <code>
    /// UC string   user name (UTF-16LE)
    /// uint32 BE   client version (4265)
    /// UC size     password length in bytes, then the password: UTF-16LE, zero padded and
    ///             Blowfish encrypted with the user name (UTF-16LE plus two zero bytes) as key
    /// </code>
    /// Java reference: mina_loginserver RequestUserInfo.java.
    /// </summary>
    public class CM_REQUEST_LOGIN : UCPacket<LSOpcode>
    {
        private readonly UCUser _user = new UCUser();

        public CM_REQUEST_LOGIN()
        {
            this.ID = LSOpcode.CM_REQUEST_LOGIN;
        }

        public override Packet<LSOpcode> New()
        {
            return new CM_REQUEST_LOGIN();
        }

        public UCUser User
        {
            get { return _user; }
        }

        public override void OnProcess(Session<LSOpcode> client)
        {
            Read();

            ((UCLobbySession)client).OnRequestLogin(this);
        }

        /// <summary>
        /// Parses the packet body into <see cref="User"/>.
        /// </summary>
        public void Read()
        {
            User.Name = this.GetUCString().TrimEnd('\0').Trim();
            User.Version = this.GetUIntBE();

            var encryptedPassword = this.GetUCBytes();
            User.Password = DecryptPassword(encryptedPassword, User.Name);
        }

        /// <summary>
        /// Decrypts the login password. The key is the user name in UTF-16LE followed by two zero bytes.
        /// </summary>
        public static string DecryptPassword(byte[] encryptedPassword, string userName)
        {
            var nameBytes = Encoding.Unicode.GetBytes(userName);
            var key = new byte[nameBytes.Length + 2];
            Array.Copy(nameBytes, key, nameBytes.Length);

            var data = (byte[])encryptedPassword.Clone();
            new Blowfish(key).Decrypt(data, 0, data.Length);

            return Encoding.Unicode.GetString(data).TrimEnd('\0').Trim();
        }
    }
}
