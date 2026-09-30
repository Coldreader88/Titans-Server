using System;
using System.Globalization;
using MySql.Data.MySqlClient;

namespace Common.Database
{
    /// <summary>
    /// Banned accounts, in the Java server's accounts columns: status 0 is banned for good (1 is normal), and
    /// ban_time (a date) bans until that day. The Lobby refuses a banned account's login with 0x0B (BLOCKED, the
    /// Java server's BAD_STATUS) once its password is right. Bans are set on the Lobby and game server consoles.
    /// </summary>
    public static class AccountBans
    {
        /// <summary>
        /// Whether the account is banned now; <paramref name="until"/> says for how long ("for good" or a date).
        /// </summary>
        public static bool IsBanned(MySqlConnection connection, uint accountID, out string until)
        {
            until = null;
            using (var cmd = new MySqlCommand("SELECT status, ban_time FROM accounts WHERE acc_id = @acc", connection))
            {
                cmd.Parameters.AddWithValue("@acc", accountID);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        return false;
                    }
                    if (!r.IsDBNull(0) && Convert.ToInt32(r.GetValue(0)) == 0)
                    {
                        until = "for good";
                        return true;
                    }
                    if (!r.IsDBNull(1))
                    {
                        var date = Convert.ToDateTime(r.GetValue(1), CultureInfo.InvariantCulture).Date;
                        if (date > DateTime.Today)
                        {
                            until = "until " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Bans the account of a user name (Lobby) for <paramref name="days"/> days, or for good when days is 0;
        /// a negative number lifts the ban. Returns what happened, for the console.
        /// </summary>
        public static string Ban(string accountName, int days)
        {
            using (var connection = DatabaseConnection.Open())
            {
                uint id = 0;
                using (var cmd = new MySqlCommand("SELECT acc_id FROM accounts WHERE name = @name LIMIT 1", connection))
                {
                    cmd.Parameters.AddWithValue("@name", accountName);
                    var v = cmd.ExecuteScalar();
                    if (v != null && !(v is DBNull))
                    {
                        id = Convert.ToUInt32(v);
                    }
                }
                if (id == 0)
                {
                    return "There is no account called " + accountName + ".";
                }
                return Ban(connection, id, accountName, days);
            }
        }

        /// <summary>
        /// The same for the account of a character (game server console). Returns what happened, and the account.
        /// </summary>
        public static string BanCharacter(string characterName, int days, out uint accountID)
        {
            accountID = 0;
            using (var connection = DatabaseConnection.Open())
            {
                string account = null;
                using (var cmd = new MySqlCommand(
                    "SELECT a.acc_id, a.name FROM characters c JOIN accounts a ON a.acc_id = c.acc_id WHERE c.char_name = @name LIMIT 1", connection))
                {
                    cmd.Parameters.AddWithValue("@name", characterName);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            accountID = Convert.ToUInt32(r.GetValue(0));
                            account = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                        }
                    }
                }
                if (accountID == 0)
                {
                    return "There is no character called " + characterName + ".";
                }
                return Ban(connection, accountID, account + " (" + characterName + ")", days);
            }
        }

        private static string Ban(MySqlConnection connection, uint accountID, string who, int days)
        {
            string sql;
            string said;
            if (days < 0)
            {
                sql = "UPDATE accounts SET status = 1, ban_time = NULL WHERE acc_id = @acc";
                said = "The ban on {0} is lifted.";
            }
            else if (days == 0)
            {
                sql = "UPDATE accounts SET status = 0, ban_time = NULL WHERE acc_id = @acc";
                said = "{0} is banned for good.";
            }
            else
            {
                sql = "UPDATE accounts SET status = 1, ban_time = DATE_ADD(CURDATE(), INTERVAL @days DAY) WHERE acc_id = @acc";
                said = "{0} is banned for {1} days.";
            }
            using (var cmd = new MySqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@acc", accountID);
                cmd.Parameters.AddWithValue("@days", days);
                cmd.ExecuteNonQuery();
            }
            if (days >= 0)
            {
                // A session key handed out before the ban no longer gets the player into a game server.
                using (var cmd = new MySqlCommand("DELETE FROM login_sessions WHERE acc_id = @acc", connection))
                {
                    cmd.Parameters.AddWithValue("@acc", accountID);
                    cmd.ExecuteNonQuery();
                }
            }
            return string.Format(said, who, days);
        }
    }
}
