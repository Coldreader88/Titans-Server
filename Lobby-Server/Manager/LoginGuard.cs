using System;
using System.Collections.Generic;

namespace TitansUC.LobbyServer.Manager
{
    /// <summary>
    /// Slows down password guessing: after <c>LoginFailLimit</c> wrong passwords from one address, that address
    /// is refused for <c>LoginLockMinutes</c>; after four times as many wrong passwords for one user name (from
    /// any address), that name is refused too. Failures older than the lock time are forgotten, and a good login
    /// clears its address and name. Kept in memory only, so a Lobby restart clears every lock.
    /// </summary>
    public static class LoginGuard
    {
        private class Entry
        {
            public int Failures;
            public DateTime LastFailure;
            public DateTime LockedUntil;
        }

        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        /// <summary>
        /// True when this address or user name is locked out right now.
        /// </summary>
        public static bool IsLocked(string address, string userName, DateTime now)
        {
            lock (entries)
            {
                return Locked(AddressKey(address), now) || Locked(NameKey(userName), now);
            }
        }

        /// <summary>
        /// Counts a wrong password; returns true when this failure locked the address or name.
        /// </summary>
        public static bool Failed(string address, string userName, int limit, int lockMinutes, DateTime now)
        {
            if (limit <= 0)
            {
                return false;
            }
            lock (entries)
            {
                bool a = Count(AddressKey(address), limit, lockMinutes, now);
                bool n = Count(NameKey(userName), limit * 4, lockMinutes, now);
                if (entries.Count > 10000)
                {
                    Prune(lockMinutes, now);
                }
                return a || n;
            }
        }

        public static void Succeeded(string address, string userName)
        {
            lock (entries)
            {
                entries.Remove(AddressKey(address));
                entries.Remove(NameKey(userName));
            }
        }

        /// <summary>
        /// Lifts every lock (Lobby console "unlock").
        /// </summary>
        public static int Clear()
        {
            lock (entries)
            {
                int n = entries.Count;
                entries.Clear();
                return n;
            }
        }

        private static string AddressKey(string address)
        {
            return "ip:" + (address ?? string.Empty);
        }

        private static string NameKey(string userName)
        {
            return "name:" + (userName ?? string.Empty).ToLowerInvariant();
        }

        private static bool Locked(string key, DateTime now)
        {
            Entry e;
            return entries.TryGetValue(key, out e) && e.LockedUntil > now;
        }

        private static bool Count(string key, int limit, int lockMinutes, DateTime now)
        {
            Entry e;
            if (!entries.TryGetValue(key, out e) || now - e.LastFailure > TimeSpan.FromMinutes(lockMinutes))
            {
                e = new Entry();
                entries[key] = e;
            }
            e.Failures++;
            e.LastFailure = now;
            if (e.Failures >= limit)
            {
                e.Failures = 0;
                e.LockedUntil = now.AddMinutes(lockMinutes);
                return true;
            }
            return false;
        }

        private static void Prune(int lockMinutes, DateTime now)
        {
            var old = new List<string>();
            foreach (var pair in entries)
            {
                if (pair.Value.LockedUntil <= now && now - pair.Value.LastFailure > TimeSpan.FromMinutes(lockMinutes))
                {
                    old.Add(pair.Key);
                }
            }
            foreach (var key in old)
            {
                entries.Remove(key);
            }
        }
    }
}
