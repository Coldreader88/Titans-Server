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
    /// Uses the table layout and password format of the Java server (sql/MySQL Account.sql,
    /// see DB/SQL/accounts.sql): passwords are stored as lowercase hex SHA-1, so existing
    /// Java databases keep working.
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

            string passwordHash = HashPassword(account.Password);

            try
            {
                using (var connection = Open())
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT acc_id, acc_level, password FROM accounts WHERE name = @name LIMIT 1", connection))
                    {
                        cmd.Parameters.AddWithValue("@name", account.UserName);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var storedHash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                                if (!string.Equals(storedHash, passwordHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    Logger.ShowInfo(string.Format("Login failed for {0}: wrong password.", account.UserName));
                                    return;
                                }

                                account.AccountID = Convert.ToUInt32(reader.GetValue(0));
                                account.GMLevel = reader.IsDBNull(1)
                                    ? (byte)Account.AccountLevel.PLAYER
                                    : Convert.ToByte(reader.GetValue(1));
                                account.Status = Account.AuthenticationStatus.SUCCESS;
                                return;
                            }
                        }
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
                        cmd.Parameters.AddWithValue("@password", passwordHash);
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
        /// Lowercase hex SHA-1 of the password's ISO-8859-1 bytes, as the Java server stored it.
        /// </summary>
        public static string HashPassword(string password)
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

        internal MySqlConnection Open()
        {
            return DatabaseConnection.Open();
        }
    }
}
