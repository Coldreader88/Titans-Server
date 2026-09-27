using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Account
{
    public class Account
    {
        public enum AuthenticationStatus : uint
        {
            SUCCESS = 1,
            WRONG_VERSION,
            BAD_CREDENTIALS,
            BLOCKED = 0x15,
            TEST = 0x16,
        }

        public uint AccountID { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public byte GMLevel { get; set; }
        public uint Version { get; set; }
        public AuthenticationStatus Status { get; set; }
        public string LastLoginIP { get; set; }
        public DateTime LastLoginTime { get; set; }


        public Account()
        {
            AccountID = 0xFF;
            UserName = "UNKNOWN";
            Password = "UNKNOWN";
            GMLevel = 0;
            Status = AuthenticationStatus.BLOCKED;
            LastLoginIP = "0.0.0.0";
            LastLoginTime = DateTime.Now;
        }

    }
}
