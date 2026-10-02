using System;
using System.Threading;
using MySql.Data.MySqlClient;
using SmartEngine.Core;

namespace Common.Database
{
    /// <summary>
    /// Server-wide state shared through the database (table server_state): whether the game is closed for
    /// maintenance (#shutdown), and a heartbeat per server (the database's own UNIX time, so the servers' clocks
    /// do not matter). The Lobby and game servers write it; the Login-Server reads it for the launcher's status.
    /// </summary>
    public static class ServerState
    {
        private const string Create =
            "CREATE TABLE IF NOT EXISTS server_state (name VARCHAR(32) NOT NULL PRIMARY KEY, value BIGINT NOT NULL DEFAULT 0)";

        public static void SetMaintenance(bool closed)
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(Create, connection))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("REPLACE INTO server_state (name, value) VALUES ('maintenance', @v)", connection))
                {
                    cmd.Parameters.AddWithValue("@v", closed ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Notes that the named server is running now.
        /// </summary>
        public static void Beat(string server)
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(Create, connection))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("REPLACE INTO server_state (name, value) VALUES (@n, UNIX_TIMESTAMP())", connection))
                {
                    cmd.Parameters.AddWithValue("@n", "beat_" + server);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Beats for the named server every few seconds for as long as the process runs. Keep the returned timer.
        /// </summary>
        public static Timer StartBeating(string server, int seconds = 10)
        {
            return new Timer(_ =>
            {
                try
                {
                    Beat(server);
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Cannot write the server heartbeat: " + ex.Message);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Seconds since the named server last beat, or -1 when it never did.
        /// </summary>
        public static long SecondsSinceBeat(string server)
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(Create, connection))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("SELECT UNIX_TIMESTAMP() - value FROM server_state WHERE name = @n", connection))
                {
                    cmd.Parameters.AddWithValue("@n", "beat_" + server);
                    var v = cmd.ExecuteScalar();
                    return v == null || v is DBNull ? -1 : Convert.ToInt64(v);
                }
            }
        }

        /// <summary>
        /// Whether the game is closed for maintenance; false when nothing was written yet.
        /// </summary>
        public static bool IsMaintenance()
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(Create, connection))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("SELECT value FROM server_state WHERE name = 'maintenance'", connection))
                {
                    var v = cmd.ExecuteScalar();
                    return v != null && !(v is DBNull) && Convert.ToInt32(v) != 0;
                }
            }
        }
    }
}
