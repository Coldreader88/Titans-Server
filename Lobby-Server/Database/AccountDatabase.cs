using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Common.Account;
using Common.Database;
using MySql.Data.MySqlClient;
using SmartEngine.Core;

namespace TitansUC.LobbyServer.Database
{
    /// <summary>
    /// Checks user names and passwords against the accounts table.
    ///
    /// Uses the table layout of the Java server (sql/MySQL Account.sql, see DB/SQL/accounts.sql). Passwords are
    /// stored salted, as "pbkdf2$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;" (PBKDF2-HMAC-SHA256, base64). The Java server stored
    /// unsalted lowercase hex SHA-1: those still log in, and are rewritten in the new form at their next login.
    /// </summary>
    public class AccountDatabase
    {
        static readonly AccountDatabase instance = new AccountDatabase();

        public static AccountDatabase Instance { get { return instance; } }

        public void Init(string host, int port, string database, string user, string password)
        {
            DatabaseConnection.Init(host, port, database, user, password);
        }

        /// <summary>
        /// Opens and closes a connection to check that the database is reachable.
        /// </summary>
        public bool TestConnection()
        {
            return DatabaseConnection.TestConnection();
        }

        /// <summary>
        /// Authenticates <paramref name="account"/> (UserName and Password must be set) and fills in
        /// AccountID, GMLevel and Status. When the name is unknown and <paramref name="autoCreate"/>
        /// is set, a new account is created with the given password.
        /// </summary>
        public void Authenticate(Account account, bool autoCreate, string remoteAddress)
        {
            account.Status = Account.AuthenticationStatus.WRONG_INPUT;

            if (string.IsNullOrEmpty(account.UserName) || string.IsNullOrEmpty(account.Password))
            {
                return;
            }

            try
            {
                using (var connection = Open())
                {
                    bool upgrade = false;
                    using (var cmd = new MySqlCommand(
                        "SELECT acc_id, acc_level, password FROM accounts WHERE name = @name LIMIT 1", connection))
                    {
                        cmd.Parameters.AddWithValue("@name", account.UserName);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var storedHash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                                if (!CheckPassword(account.Password, storedHash, out upgrade))
                                {
                                    Logger.ShowInfo(string.Format("Login failed for {0}: wrong password.", account.UserName));
                                    return;
                                }

                                account.AccountID = Convert.ToUInt32(reader.GetValue(0));
                                account.GMLevel = reader.IsDBNull(1)
                                    ? (byte)Account.AccountLevel.PLAYER
                                    : Convert.ToByte(reader.GetValue(1));
                                account.Status = Account.AuthenticationStatus.SUCCESS;
                            }
                        }
                    }

                    string until;
                    if (account.Status == Account.AuthenticationStatus.SUCCESS &&
                        Common.Database.AccountBans.IsBanned(connection, account.AccountID, out until))
                    {
                        Logger.ShowInfo(string.Format("Login refused for {0}: banned {1}.", account.UserName, until));
                        account.Status = Account.AuthenticationStatus.BLOCKED;
                        return;
                    }
                    if (account.Status == Account.AuthenticationStatus.SUCCESS && !upgrade)
                    {
                        return;
                    }

                    if (account.Status == Account.AuthenticationStatus.SUCCESS)
                    {
                        // An old unsalted SHA-1 password: store it salted now that we have it.
                        SetPassword(connection, account.AccountID, account.Password);
                        Logger.ShowInfo(string.Format("Rehashed the password of {0}.", account.UserName));
                        return;
                    }

                    if (!autoCreate)
                    {
                        Logger.ShowInfo(string.Format("Login failed for {0}: no such account.", account.UserName));
                        return;
                    }

                    using (var cmd = new MySqlCommand(
                        "INSERT INTO accounts (name, password, acc_level, creation_date, last_ip) " +
                        "VALUES (@name, @password, @level, @created, @ip)", connection))
                    {
                        cmd.Parameters.AddWithValue("@name", account.UserName);
                        cmd.Parameters.AddWithValue("@password", HashPassword(account.Password));
                        cmd.Parameters.AddWithValue("@level", (int)Account.AccountLevel.PLAYER);
                        cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                        cmd.Parameters.AddWithValue("@ip", remoteAddress ?? string.Empty);
                        cmd.ExecuteNonQuery();

                        account.AccountID = (uint)cmd.LastInsertedId;
                        account.GMLevel = (byte)Account.AccountLevel.PLAYER;
                        account.Status = Account.AuthenticationStatus.SUCCESS;

                        Logger.ShowInfo(string.Format("Created account {0} (id {1}).", account.UserName, account.AccountID));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                account.Status = Account.AuthenticationStatus.WRONG_INPUT;
            }
        }

        /// <summary>
        /// PBKDF2 iterations for new password hashes (stored with each hash, so raising it keeps old hashes valid).
        /// </summary>
        public const int Iterations = 100000;

        private static readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

        /// <summary>
        /// A new salted hash of <paramref name="password"/>: "pbkdf2$iterations$salt$hash".
        /// </summary>
        public static string HashPassword(string password)
        {
            var salt = new byte[16];
            lock (random)
            {
                random.GetBytes(salt);
            }
            return string.Format("pbkdf2${0}${1}${2}", Iterations, Convert.ToBase64String(salt),
                Convert.ToBase64String(Pbkdf2(password, salt, Iterations)));
        }

        /// <summary>
        /// Whether <paramref name="password"/> matches <paramref name="stored"/>, a salted hash or an old unsalted
        /// SHA-1 (then <paramref name="upgrade"/> is set).
        /// </summary>
        public static bool CheckPassword(string password, string stored, out bool upgrade)
        {
            upgrade = false;
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
            {
                return false;
            }
            var parts = stored.Split('$');
            if (parts.Length == 4 && parts[0] == "pbkdf2")
            {
                int iterations;
                byte[] salt, hash;
                try
                {
                    iterations = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    salt = Convert.FromBase64String(parts[2]);
                    hash = Convert.FromBase64String(parts[3]);
                }
                catch (FormatException)
                {
                    return false;
                }
                return iterations > 0 && SameBytes(Pbkdf2(password, salt, iterations), hash);
            }
            if (stored.Length == 40 && SameBytes(Encoding.ASCII.GetBytes(LegacyHash(password)), Encoding.ASCII.GetBytes(stored.ToLowerInvariant())))
            {
                upgrade = true;
                return true;
            }
            return false;
        }

        private static byte[] Pbkdf2(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(32);
            }
        }

        /// <summary>
        /// Compares in constant time.
        /// </summary>
        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        /// <summary>
        /// Lowercase hex SHA-1 of the password's ISO-8859-1 bytes, as the Java server stored it.
        /// </summary>
        public static string LegacyHash(string password)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.GetEncoding(28591).GetBytes(password));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static void SetPassword(MySqlConnection connection, uint accountID, string password)
        {
            using (var cmd = new MySqlCommand("UPDATE accounts SET password = @password WHERE acc_id = @id", connection))
            {
                cmd.Parameters.AddWithValue("@password", HashPassword(password));
                cmd.Parameters.AddWithValue("@id", accountID);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Widens accounts.password for the salted hashes (the Java table had 45 characters).
        /// </summary>
        public void EnsurePasswordColumn()
        {
            using (var connection = Open())
            {
                using (var cmd = new MySqlCommand(
                    "SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.COLUMNS " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'accounts' AND COLUMN_NAME = 'password'", connection))
                {
                    var length = cmd.ExecuteScalar();
                    if (length == null || length is DBNull || Convert.ToInt64(length) >= 128)
                    {
                        return;
                    }
                }
                using (var cmd = new MySqlCommand(
                    "ALTER TABLE accounts MODIFY `password` varchar(128) CHARACTER SET utf8 COLLATE utf8_bin DEFAULT NULL", connection))
                {
                    cmd.ExecuteNonQuery();
                }
                Logger.ShowInfo("Widened accounts.password to 128 characters for salted password hashes.");
            }
        }

        /// <summary>
        /// Creates an account, or sets the password (and level) of an existing one: the Lobby console's
        /// "account" command. Returns what it did.
        /// </summary>
        public string CreateOrUpdate(string name, string password, int level)
        {
            using (var connection = Open())
            {
                object id;
                using (var cmd = new MySqlCommand("SELECT acc_id FROM accounts WHERE name = @name LIMIT 1", connection))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    id = cmd.ExecuteScalar();
                }
                if (id != null && !(id is DBNull))
                {
                    using (var cmd = new MySqlCommand("UPDATE accounts SET password = @password, acc_level = @level WHERE acc_id = @id", connection))
                    {
                        cmd.Parameters.AddWithValue("@password", HashPassword(password));
                        cmd.Parameters.AddWithValue("@level", level);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                    return string.Format("Account {0} (id {1}): new password, level {2}.", name, id, level);
                }
                using (var cmd = new MySqlCommand(
                    "INSERT INTO accounts (name, password, acc_level, creation_date) VALUES (@name, @password, @level, @created)", connection))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@password", HashPassword(password));
                    cmd.Parameters.AddWithValue("@level", level);
                    cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    cmd.ExecuteNonQuery();
                    return string.Format("Created account {0} (id {1}), level {2}.", name, cmd.LastInsertedId, level);
                }
            }
        }

        internal MySqlConnection Open()
        {
            return DatabaseConnection.Open();
        }
    }
}
