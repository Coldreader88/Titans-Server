using System;
using MySql.Data.MySqlClient;
using SmartEngine.Core;

namespace Common.Database
{
    /// <summary>
    /// Connection settings for the MySQL database shared by the Lobby and Game servers
    /// (DB/SQL/accounts.sql, DB/SQL/characters.sql).
    /// </summary>
    public static class DatabaseConnection
    {
        private static string connectionString;

        public static void Init(string host, int port, string database, string user, string password)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = (uint)port,
                Database = database,
                UserID = user,
                Password = password,
                CharacterSet = "utf8",
                Pooling = true,
            };
            connectionString = builder.ConnectionString;
        }

        /// <summary>
        /// Opens and closes a connection to check that the database is reachable.
        /// </summary>
        public static bool TestConnection()
        {
            try
            {
                using (Open())
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                return false;
            }
        }

        public static MySqlConnection Open()
        {
            if (connectionString == null)
            {
                throw new InvalidOperationException("DatabaseConnection.Init has not been called.");
            }

            var connection = new MySqlConnection(connectionString);
            connection.Open();
            return connection;
        }
    }
}
