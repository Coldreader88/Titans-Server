using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;

namespace Common.Database
{
    /// <summary>
    /// Writes the whole database to an .sql file (DROP TABLE, CREATE TABLE and INSERTs for every table), without
    /// needing mysqldump. Load one back with: mariadb -uroot -p titans-server &lt; file.sql. The game server runs
    /// it every GameServer.xml BackupHours and on the console command "backup", and keeps the newest BackupKeep.
    /// </summary>
    public static class Backup
    {
        private static readonly object running = new object();
        private const int RowsPerInsert = 200;

        /// <summary>
        /// Dumps the database into <paramref name="folder"/> and deletes all but the newest <paramref name="keep"/>
        /// backups there. Returns the new file.
        /// </summary>
        public static string Run(string folder, int keep)
        {
            lock (running)
            {
                Directory.CreateDirectory(folder);
                string name = "titans-server-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".sql";
                string path = Path.Combine(folder, name);
                string temp = path + ".part";
                using (var connection = DatabaseConnection.Open())
                using (var writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("-- Titans-Server database backup, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    writer.WriteLine("SET NAMES utf8mb4;");
                    writer.WriteLine("SET FOREIGN_KEY_CHECKS = 0;");
                    foreach (var table in Tables(connection))
                    {
                        DumpTable(connection, writer, table);
                    }
                    writer.WriteLine("SET FOREIGN_KEY_CHECKS = 1;");
                }
                File.Move(temp, path);

                foreach (var old in Directory.GetFiles(folder, "titans-server-*.sql").OrderByDescending(f => f).Skip(Math.Max(1, keep)))
                {
                    File.Delete(old);
                }
                return path;
            }
        }

        private static List<string> Tables(MySqlConnection connection)
        {
            var tables = new List<string>();
            using (var cmd = new MySqlCommand("SHOW FULL TABLES WHERE Table_type = 'BASE TABLE'", connection))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    tables.Add(r.GetString(0));
                }
            }
            return tables;
        }

        private static void DumpTable(MySqlConnection connection, StreamWriter writer, string table)
        {
            string quoted = "`" + table.Replace("`", "``") + "`";
            writer.WriteLine();
            writer.WriteLine("DROP TABLE IF EXISTS " + quoted + ";");
            using (var cmd = new MySqlCommand("SHOW CREATE TABLE " + quoted, connection))
            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    writer.WriteLine(r.GetString(1) + ";");
                }
            }

            using (var cmd = new MySqlCommand("SELECT * FROM " + quoted, connection))
            using (var r = cmd.ExecuteReader())
            {
                int rows = 0;
                var values = new string[r.FieldCount];
                while (r.Read())
                {
                    for (int i = 0; i < r.FieldCount; i++)
                    {
                        values[i] = Literal(r.IsDBNull(i) ? null : r.GetValue(i));
                    }
                    writer.Write(rows % RowsPerInsert == 0 ? (rows > 0 ? ";\n" : string.Empty) + "INSERT INTO " + quoted + " VALUES\n(" : ",\n(");
                    writer.Write(string.Join(",", values));
                    writer.Write(")");
                    rows++;
                }
                if (rows > 0)
                {
                    writer.WriteLine(";");
                }
            }
        }

        private static string Literal(object value)
        {
            if (value == null)
            {
                return "NULL";
            }
            if (value is byte[])
            {
                var bytes = (byte[])value;
                return bytes.Length == 0 ? "''" : "0x" + BitConverter.ToString(bytes).Replace("-", string.Empty);
            }
            if (value is bool)
            {
                return (bool)value ? "1" : "0";
            }
            if (value is DateTime)
            {
                return "'" + ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
            }
            if (value is TimeSpan)
            {
                return "'" + ((TimeSpan)value).ToString() + "'";
            }
            if (value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint ||
                value is long || value is ulong || value is float || value is double || value is decimal)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            return "'" + MySqlHelper.EscapeString(Convert.ToString(value, CultureInfo.InvariantCulture)) + "'";
        }
    }
}
