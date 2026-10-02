using System;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;

namespace Common.Database
{
    /// <summary>
    /// Session keys that let the Game server trust a character handed over by the Lobby server.
    ///
    /// The official login reply (0x38000) carries a random 32 bit key. The client sends it back when it
    /// asks for the game server address (0x30005) and when it logs in to the game server (0x41), so the
    /// Lobby stores the key with the chosen character and the Game server checks it. The Java server
    /// sent 0 and trusted any character id; this closes that hole.
    /// Table: login_sessions (DB/SQL/accounts.sql). The Lobby creates it when it is missing.
    ///
    /// The session also keeps the address the player logged in to the Lobby from: the game and chat servers only
    /// accept the character from that address (an empty ip, from before the column existed, matches any).
    /// </summary>
    public class LoginSessionDatabase
    {
        static readonly LoginSessionDatabase instance = new LoginSessionDatabase();

        public static LoginSessionDatabase Instance { get { return instance; } }

        /// <summary>
        /// How long after the game server handoff the client may log in to the game server.
        /// </summary>
        public static readonly TimeSpan HandoffLifetime = TimeSpan.FromMinutes(5);

        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

        public void EnsureTable()
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "CREATE TABLE IF NOT EXISTS `login_sessions` (" +
                "`acc_id` int(10) NOT NULL, " +
                "`session_key` int(10) unsigned NOT NULL, " +
                "`char_id` int(10) unsigned NOT NULL default '0', " +
                "`updated` int(10) NOT NULL default '0', " +
                "`ip` varchar(45) NOT NULL default '', " +
                "PRIMARY KEY (`acc_id`)) ENGINE=MyISAM DEFAULT CHARSET=utf8", connection))
            {
                cmd.ExecuteNonQuery();
            }
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM information_schema.COLUMNS " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'login_sessions' AND COLUMN_NAME = 'ip'", connection))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    {
                        return;
                    }
                }
                using (var cmd = new MySqlCommand(
                    "ALTER TABLE login_sessions ADD COLUMN `ip` varchar(45) NOT NULL default ''", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// The client's IP address as text ("" when it cannot be read).
        /// </summary>
        public static string AddressOf(System.Net.Sockets.Socket socket)
        {
            try
            {
                return ((System.Net.IPEndPoint)socket.RemoteEndPoint).Address.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Starts a new session for the account, logged in from <paramref name="address"/>, and returns its key
        /// (never 0). Replaces any older session.
        /// </summary>
        public uint Begin(uint accountID, string address)
        {
            uint key = NewKey();
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "REPLACE INTO login_sessions (acc_id, session_key, char_id, updated, ip) VALUES (@acc, @key, 0, @now, @ip)", connection))
            {
                cmd.Parameters.AddWithValue("@acc", accountID);
                cmd.Parameters.AddWithValue("@ip", address ?? string.Empty);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", Now());
                cmd.ExecuteNonQuery();
            }
            return key;
        }

        /// <summary>
        /// Records the character the account is taking to the game server. Returns false when the key is
        /// not the account's current key.
        /// </summary>
        public bool SelectCharacter(uint accountID, uint key, uint characterClientID)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "UPDATE login_sessions SET char_id = @char, updated = @now WHERE acc_id = @acc AND session_key = @key", connection))
            {
                cmd.Parameters.AddWithValue("@char", characterClientID);
                cmd.Parameters.AddWithValue("@now", Now());
                cmd.Parameters.AddWithValue("@acc", accountID);
                cmd.Parameters.AddWithValue("@key", key);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>
        /// Checks a game server login: the key must belong to the account that took this character to the
        /// game server within <see cref="HandoffLifetime"/>, from the same address. Returns that account id, or 0.
        /// </summary>
        public uint Verify(uint key, uint characterClientID, string address)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "SELECT acc_id FROM login_sessions WHERE session_key = @key AND char_id = @char AND updated >= @since " +
                "AND (ip = '' OR ip = @ip) LIMIT 1",
                connection))
            {
                cmd.Parameters.AddWithValue("@ip", address ?? string.Empty);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@char", characterClientID);
                cmd.Parameters.AddWithValue("@since", Now() - (int)HandoffLifetime.TotalSeconds);
                var result = cmd.ExecuteScalar();
                return result == null || result is DBNull ? 0 : Convert.ToUInt32(result);
            }
        }

        /// <summary>
        /// Starts the <see cref="HandoffLifetime"/> again for a game server login with this key (a shuttle flight
        /// between Earth and Space reconnects with the same key).
        /// </summary>
        public void Refresh(uint key, uint characterClientID)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "UPDATE login_sessions SET updated = @now WHERE session_key = @key AND char_id = @char", connection))
            {
                cmd.Parameters.AddWithValue("@now", Now());
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@char", characterClientID);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Checks that an account took this character to the game server in its current Lobby session, and logged
        /// in to the Lobby from <paramref name="address"/> (the CMS login carries no session key).
        /// </summary>
        public bool IsSelected(uint characterClientID, string address)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM login_sessions WHERE char_id = @char AND (ip = '' OR ip = @ip)", connection))
            {
                cmd.Parameters.AddWithValue("@char", characterClientID);
                cmd.Parameters.AddWithValue("@ip", address ?? string.Empty);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private uint NewKey()
        {
            var bytes = new byte[4];
            uint key;
            do
            {
                lock (random)
                {
                    random.GetBytes(bytes);
                }
                key = BitConverter.ToUInt32(bytes, 0);
            } while (key == 0);
            return key;
        }

        private static int Now()
        {
            return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
