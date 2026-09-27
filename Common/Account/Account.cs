using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Account
{
    public class Account
    {
        /// <summary>
        /// Result codes for the login reply (0x38000). Values from the Java reference
        /// (mina_loginserver NotifyUserInfo.java).
        /// </summary>
        public enum AuthenticationStatus : uint
        {
            SUCCESS = 0x01,
            WRONG_INPUT = 0x09,
            BLOCKED = 0x0B,
            BAD_CLIENT = 0x0C,
            LOGIN_TOO_SOON = 0x15,
            ALREADY_ONLINE = 0x6A,
        }

        /// <summary>
        /// Account levels, stored in accounts.acc_level and sent to the client as the GM tag.
        /// Values from the Java reference (mina_common model/appearance/Tag.java).
        /// </summary>
        public enum AccountLevel : byte
        {
            GM = 4,
            VIP = 5,
            EVENT = 7,
            ADMIN = 9,
            PLAYER = 10,
        }

        public uint AccountID { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public byte GMLevel { get; set; }

        /// <summary>
        /// Random key sent in the login reply; the client presents it to get the game server address
        /// and to log in to the game server (see Common.Database.LoginSessionDatabase).
        /// </summary>
        public uint SessionKey { get; set; }
        public uint Version { get; set; }
        public AuthenticationStatus Status { get; set; }
        public bool Authenticated { get { return Status == AuthenticationStatus.SUCCESS; } }
        public string LastLoginIP { get; set; }
        public DateTime LastLoginTime { get; set; }


        public Account()
        {
            AccountID = 0;
            UserName = "UNKNOWN";
            Password = "UNKNOWN";
            GMLevel = (byte)AccountLevel.PLAYER;
            Status = AuthenticationStatus.WRONG_INPUT;
            LastLoginIP = "0.0.0.0";
            LastLoginTime = DateTime.Now;
        }

    }
}
