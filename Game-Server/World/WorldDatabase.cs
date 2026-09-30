using System;
using System.Collections.Generic;
using System.Threading;
using Common.Characters;
using Common.Database;
using MySql.Data.MySqlClient;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// What the game server keeps in the database besides the characters: everything lying on the ground in
    /// its zone (items, vehicles, wrecks, with their cargo), so the world is as it was after a restart, and
    /// the shuttle flights between the Earth and Space servers. Java reference: GameServer.shutdown
    /// (DeleteGroundItems + InsertGroundItemQuery, config save_ground_items) and RequestGroundItemsQuery.
    /// The tables are created when missing (DB/SQL/world.sql has the same definitions).
    /// </summary>
    public static class WorldDatabase
    {
        /// <summary>
        /// Seconds between saves of the ground while it changes.
        /// </summary>
        public const int SaveSeconds = 10;

        private static Timer timer;
        private static int saving;

        private const string GroundTable =
            "CREATE TABLE IF NOT EXISTS ground_items (" +
            " id INT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            " zone SMALLINT NOT NULL," +
            " item_id INT NOT NULL," +
            " item_name VARCHAR(64) NOT NULL DEFAULT ''," +
            " item_amount INT NOT NULL DEFAULT 1," +
            " child TEXT," +
            " vehicle TINYINT NOT NULL DEFAULT 0," +
            " wreck TINYINT NOT NULL DEFAULT 0," +
            " x INT NOT NULL, y INT NOT NULL, z INT NOT NULL," +
            " rotation VARCHAR(12) NOT NULL DEFAULT '000000000000'," +
            " owner_id INT UNSIGNED NOT NULL," +
            " placed INT NOT NULL," +
            " KEY zone (zone))";

        private const string FlightTable =
            "CREATE TABLE IF NOT EXISTS flights (" +
            " char_id INT UNSIGNED NOT NULL PRIMARY KEY," +
            " cluster SMALLINT NOT NULL," +
            " vehicle_uid INT UNSIGNED NOT NULL DEFAULT 0," +
            " transport_a INT NOT NULL, transport_b INT NOT NULL," +
            " x INT NOT NULL, y INT NOT NULL, z INT NOT NULL)";

        public static void EnsureTables()
        {
            using (var connection = DatabaseConnection.Open())
            {
                foreach (var sql in new[] { GroundTable, FlightTable })
                {
                    using (var cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Puts back on the ground what was saved for this zone, and starts saving it while it changes.
        /// </summary>
        public static void LoadGround(ushort zone)
        {
            int count = 0;
            int now = GameWorld.UnixTime();
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("SELECT item_id, item_name, item_amount, child, vehicle, wreck, x, y, z, rotation, owner_id, placed " +
                "FROM ground_items WHERE zone = @zone", connection))
            {
                cmd.Parameters.AddWithValue("@zone", zone);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new CharacterItem
                        {
                            ItemID = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Amount = reader.GetInt32(2),
                            Children = reader.IsDBNull(3) ? null : reader.GetString(3),
                        };
                        bool vehicle = reader.GetInt32(4) != 0;
                        var node = vehicle ? PlayerInventory.BuildVehicle(row)
                            : PlayerInventory.ApplyState(PlayerInventory.NewItem(row.ItemID, row.Amount > 0 ? row.Amount : 1,
                                string.IsNullOrEmpty(row.Name) ? null : row.Name), row.Children);
                        bool wreck = reader.GetInt32(5) != 0;
                        if (wreck)
                        {
                            node.Health = 0;
                        }
                        var item = new GroundItem(node, zone, reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8),
                            ParseRotation(reader.GetString(9)), reader.GetUInt32(10))
                        {
                            IsWreck = wreck,
                            Placed = reader.GetInt32(11),
                        };
                        if (item.CanExpire && item.Expires <= now)
                        {
                            continue;
                        }
                        GameWorld.Instance.Place(item);
                        count++;
                    }
                }
            }
            GameWorld.Instance.TakeGroundChanged();
            Logger.ShowInfo(string.Format("Put back {0} items and vehicles on the ground.", count));
        }

        public static void StartSaving(ushort zone)
        {
            timer = new Timer(_ =>
            {
                if (GameWorld.Instance.TakeGroundChanged())
                {
                    SaveGround(zone);
                }
            }, null, SaveSeconds * 1000, SaveSeconds * 1000);
        }

        /// <summary>
        /// Writes everything on the ground in this zone (replacing what was saved before).
        /// </summary>
        public static void SaveGround(ushort zone)
        {
            if (Interlocked.Exchange(ref saving, 1) == 1)
            {
                GameWorld.Instance.GroundChanged();
                return;
            }
            try
            {
                var items = GameWorld.Instance.AllGround();
                using (var connection = DatabaseConnection.Open())
                using (var transaction = connection.BeginTransaction())
                {
                    using (var cmd = new MySqlCommand("DELETE FROM ground_items WHERE zone = @zone", connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@zone", zone);
                        cmd.ExecuteNonQuery();
                    }
                    foreach (var g in items)
                    {
                        if (g.ClusterID != zone || g.IsTower)
                        {
                            continue;
                        }
                        CharacterItem row;
                        lock (g.Node)
                        {
                            row = PlayerInventory.Describe(0, g.Node, g.IsVehicle);
                        }
                        using (var cmd = new MySqlCommand(
                            "INSERT INTO ground_items (zone, item_id, item_name, item_amount, child, vehicle, wreck, x, y, z, rotation, owner_id, placed) " +
                            "VALUES (@zone, @item, @name, @amount, @child, @vehicle, @wreck, @x, @y, @z, @rotation, @owner, @placed)", connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@zone", zone);
                            cmd.Parameters.AddWithValue("@item", row.ItemID);
                            cmd.Parameters.AddWithValue("@name", row.Name ?? string.Empty);
                            cmd.Parameters.AddWithValue("@amount", row.Amount);
                            cmd.Parameters.AddWithValue("@child", row.Children ?? string.Empty);
                            cmd.Parameters.AddWithValue("@vehicle", g.IsVehicle ? 1 : 0);
                            cmd.Parameters.AddWithValue("@wreck", g.IsWreck ? 1 : 0);
                            cmd.Parameters.AddWithValue("@x", g.X);
                            cmd.Parameters.AddWithValue("@y", g.Y);
                            cmd.Parameters.AddWithValue("@z", g.Z);
                            cmd.Parameters.AddWithValue("@rotation", BitConverter.ToString(g.Rotation ?? new byte[6]).Replace("-", ""));
                            cmd.Parameters.AddWithValue("@owner", g.OwnerID);
                            cmd.Parameters.AddWithValue("@placed", g.Placed);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
                GameWorld.Instance.GroundChanged();
            }
            finally
            {
                Interlocked.Exchange(ref saving, 0);
            }
        }

        private static byte[] ParseRotation(string hex)
        {
            var bytes = new byte[6];
            for (int i = 0; i < 6 && i * 2 + 1 < (hex ?? "").Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Records a shuttle flight to the other server: which side, and where the shuttle was bought (the
        /// transport part of the player info the other server sends after the flight, 0x805F).
        /// </summary>
        public static void SaveFlight(uint characterID, ushort cluster, uint shuttleUniqueID, Transport transport)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("REPLACE INTO flights (char_id, cluster, vehicle_uid, transport_a, transport_b, x, y, z) " +
                "VALUES (@id, @cluster, @uid, @a, @b, @x, @y, @z)", connection))
            {
                cmd.Parameters.AddWithValue("@id", characterID);
                cmd.Parameters.AddWithValue("@cluster", cluster);
                cmd.Parameters.AddWithValue("@uid", shuttleUniqueID);
                cmd.Parameters.AddWithValue("@a", transport.A);
                cmd.Parameters.AddWithValue("@b", transport.B);
                cmd.Parameters.AddWithValue("@x", transport.X);
                cmd.Parameters.AddWithValue("@y", transport.Y);
                cmd.Parameters.AddWithValue("@z", transport.Z);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// The unique id the shuttle had on the other side, when the character is arriving from a flight; 0
        /// otherwise.
        /// </summary>
        public static uint FlightShuttleID(uint characterID)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("SELECT vehicle_uid FROM flights WHERE char_id = @id", connection))
            {
                cmd.Parameters.AddWithValue("@id", characterID);
                var value = cmd.ExecuteScalar();
                return value == null || value is DBNull ? 0 : Convert.ToUInt32(value);
            }
        }

        /// <summary>
        /// The character's flight, removed; null when they are not on one.
        /// </summary>
        public static Transport TakeFlight(uint characterID)
        {
            Transport transport = null;
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand("SELECT transport_a, transport_b, x, y, z FROM flights WHERE char_id = @id", connection))
                {
                    cmd.Parameters.AddWithValue("@id", characterID);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            transport = new Transport
                            {
                                A = reader.GetInt32(0), B = reader.GetInt32(1),
                                X = reader.GetInt32(2), Y = reader.GetInt32(3), Z = reader.GetInt32(4),
                            };
                        }
                    }
                }
                using (var cmd = new MySqlCommand("DELETE FROM flights WHERE char_id = @id", connection))
                {
                    cmd.Parameters.AddWithValue("@id", characterID);
                    cmd.ExecuteNonQuery();
                }
            }
            return transport;
        }
    }
}
