using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Common.Characters;
using Common.Database;
using MySql.Data.MySqlClient;
using SmartEngine.Core;
using TitansUC.CmsServer.World;

namespace TitansUC.CmsServer.Database
{
    /// <summary>
    /// The CMS server's tables: characters and appearance (read only, from the Lobby), team (player
    /// teams), teams (NPC squads, for their names), friends and tele_bookmark (GM bookmarks).
    ///
    /// team, teams and tele_bookmark are the Java server's tables (Java.zip sql/). The Java server kept
    /// friend lists in memory only, so friends is new. team gets a created column. Both are added when
    /// missing (DB/SQL/cms.sql has the same statements). Character ids in team and friends are the ids
    /// the client sees (Character.ClientID), like in characters.team_id.
    /// </summary>
    public class CmsDatabase
    {
        static readonly CmsDatabase instance = new CmsDatabase();

        public static CmsDatabase Instance { get { return instance; } }

        /// <summary>
        /// Player team ids: the Java server gave teams the id chain digit 5 (IDAccessChain.TEAM) in front of
        /// a counter starting at 1500000, so the first team is 51500000.
        /// </summary>
        public const int FirstTeamID = 51500000;

        /// <summary>
        /// NPC squad ids start with the digit 6 (IDAccessChain.DATABASE_SQUAD) followed by 1500000 + teams.team_id.
        /// </summary>
        private const string SquadPrefix = "6";
        private const int SquadOffset = 1500000;

        private readonly object teamLock = new object();

        private const string SelectMember =
            "SELECT c.char_id, c.acc_id, c.char_name, c.char_access, c.team_id, a.gender, a.rank " +
            "FROM characters c LEFT JOIN appearance a ON a.char_id = c.char_id ";

        public void EnsureTables()
        {
            using (var connection = DatabaseConnection.Open())
            {
                Execute(connection,
                    "CREATE TABLE IF NOT EXISTS `team` (" +
                    "`creatorName` text COLLATE utf8_bin NOT NULL, " +
                    "`creatorID` int(11) NOT NULL, " +
                    "`name` text CHARACTER SET utf8 NOT NULL, " +
                    "`id` int(11) NOT NULL, " +
                    "`created` int(11) NOT NULL DEFAULT '0'" +
                    ") ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_bin");

                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() " +
                    "AND TABLE_NAME = 'team' AND COLUMN_NAME = 'created'", connection))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        Logger.ShowInfo("Adding the created column to the team table.");
                        Execute(connection, "ALTER TABLE `team` ADD COLUMN `created` int(11) NOT NULL DEFAULT '0'");
                    }
                }

                Execute(connection,
                    "CREATE TABLE IF NOT EXISTS `friends` (" +
                    "`char_id` int(10) unsigned NOT NULL, " +
                    "`friend_id` int(10) unsigned NOT NULL, " +
                    "PRIMARY KEY (`char_id`, `friend_id`), KEY `friend_id` (`friend_id`)" +
                    ") ENGINE=MyISAM DEFAULT CHARSET=utf8");
            }
        }

        #region Characters

        /// <summary>
        /// Loads a character by the id the client sees, or null.
        /// </summary>
        public Member LoadMember(uint clientID)
        {
            return LoadMembers(new[] { clientID }).FirstOrDefault();
        }

        /// <summary>
        /// Loads the characters with these client ids that exist, in no particular order.
        /// </summary>
        public List<Member> LoadMembers(IEnumerable<uint> clientIDs)
        {
            var ids = new List<uint>();
            foreach (uint clientID in clientIDs.Distinct())
            {
                uint id;
                if (Character.TryFromClientID(clientID, out id))
                {
                    ids.Add(id);
                }
            }

            var members = new List<Member>();
            if (ids.Count == 0)
            {
                return members;
            }

            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                SelectMember + "WHERE c.char_id IN (" + string.Join(", ", ids.Select(i => i.ToString(CultureInfo.InvariantCulture))) + ")",
                connection))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    members.Add(ReadMember(r));
                }
            }
            return members;
        }

        /// <summary>
        /// The characters in a team.
        /// </summary>
        public List<Member> LoadTeamMembers(int teamID)
        {
            var members = new List<Member>();
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(SelectMember + "WHERE c.team_id = @team ORDER BY c.char_id", connection))
            {
                cmd.Parameters.AddWithValue("@team", teamID);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        members.Add(ReadMember(r));
                    }
                }
            }
            return members;
        }

        private static Member ReadMember(MySqlDataReader r)
        {
            return new Member
            {
                ClientID = Character.ToClientID(Convert.ToUInt32(r["char_id"], CultureInfo.InvariantCulture)),
                AccountID = (uint)GetInt(r, "acc_id", 0),
                Name = r["char_name"] as string ?? string.Empty,
                Access = GetInt(r, "char_access", AccessLevel.PlayerTag),
                TeamID = GetInt(r, "team_id", -1),
                Gender = (byte)GetInt(r, "gender", 1),
                Rank = (byte)GetInt(r, "rank", 1),
            };
        }

        private void SetCharacterTeam(MySqlConnection connection, uint clientID, int teamID)
        {
            uint id;
            if (!Character.TryFromClientID(clientID, out id))
            {
                return;
            }
            using (var cmd = new MySqlCommand("UPDATE characters SET team_id = @team WHERE char_id = @id", connection))
            {
                cmd.Parameters.AddWithValue("@team", teamID);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Teams

        public Team LoadTeam(int teamID)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("SELECT id, name, creatorID, creatorName, created FROM team WHERE id = @id LIMIT 1", connection))
            {
                cmd.Parameters.AddWithValue("@id", teamID);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        return null;
                    }
                    return new Team
                    {
                        ID = GetInt(r, "id", teamID),
                        Name = r["name"] as string ?? string.Empty,
                        LeaderID = (uint)GetInt(r, "creatorID", 0),
                        LeaderName = r["creatorName"] as string ?? string.Empty,
                        Created = (uint)GetInt(r, "created", 0),
                    };
                }
            }
        }

        public bool TeamNameExists(string name)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM team WHERE LOWER(name) = LOWER(@name)", connection))
            {
                cmd.Parameters.AddWithValue("@name", name);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// Creates a team led by <paramref name="leader"/> and puts the leader in it.
        /// </summary>
        public Team CreateTeam(string name, Member leader, uint now)
        {
            lock (teamLock)
            using (var connection = DatabaseConnection.Open())
            {
                int id;
                using (var cmd = new MySqlCommand("SELECT MAX(id) FROM team", connection))
                {
                    var max = cmd.ExecuteScalar();
                    id = max == null || max is DBNull ? FirstTeamID : Math.Max(FirstTeamID, Convert.ToInt32(max) + 1);
                }

                using (var cmd = new MySqlCommand(
                    "INSERT INTO team (creatorName, creatorID, name, id, created) VALUES (@leaderName, @leader, @name, @id, @created)", connection))
                {
                    cmd.Parameters.AddWithValue("@leaderName", leader.Name);
                    cmd.Parameters.AddWithValue("@leader", leader.ClientID);
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@created", now);
                    cmd.ExecuteNonQuery();
                }

                SetCharacterTeam(connection, leader.ClientID, id);

                return new Team { ID = id, Name = name, LeaderID = leader.ClientID, LeaderName = leader.Name, Created = now };
            }
        }

        public void SetTeam(uint clientID, int teamID)
        {
            using (var connection = DatabaseConnection.Open())
            {
                SetCharacterTeam(connection, clientID, teamID);
            }
        }

        public void SetTeamLeader(int teamID, Member leader)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("UPDATE team SET creatorID = @leader, creatorName = @leaderName WHERE id = @id", connection))
            {
                cmd.Parameters.AddWithValue("@leader", leader.ClientID);
                cmd.Parameters.AddWithValue("@leaderName", leader.Name);
                cmd.Parameters.AddWithValue("@id", teamID);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteTeam(int teamID)
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand("DELETE FROM team WHERE id = @id", connection))
                {
                    cmd.Parameters.AddWithValue("@id", teamID);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new MySqlCommand("UPDATE characters SET team_id = -1 WHERE team_id = @id", connection))
                {
                    cmd.Parameters.AddWithValue("@id", teamID);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// The name to show for a team id: a player team, or an NPC squad from the teams table.
        /// Returns null when it is neither. Java reference: NotifyTeamName.java.
        /// </summary>
        public string LoadTeamName(int teamID)
        {
            var team = LoadTeam(teamID);
            if (team != null)
            {
                return team.Name;
            }

            var text = teamID.ToString(CultureInfo.InvariantCulture);
            int squad;
            if (teamID > 0 && text.StartsWith(SquadPrefix, StringComparison.Ordinal) && text.Length > 1 &&
                int.TryParse(text.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out squad))
            {
                using (var connection = DatabaseConnection.Open())
                using (var cmd = new MySqlCommand("SELECT team_name FROM teams WHERE team_id = @id LIMIT 1", connection))
                {
                    cmd.Parameters.AddWithValue("@id", squad - SquadOffset);
                    try
                    {
                        return cmd.ExecuteScalar() as string;
                    }
                    catch (MySqlException ex)
                    {
                        // The NPC teams table is optional.
                        Logger.ShowWarning("Cannot read the teams table: " + ex.Message);
                    }
                }
            }
            return null;
        }

        #endregion

        #region Friends

        public List<uint> LoadFriendIDs(uint clientID)
        {
            return LoadIDs("SELECT friend_id FROM friends WHERE char_id = @id ORDER BY friend_id", clientID);
        }

        /// <summary>
        /// The characters that have <paramref name="clientID"/> on their friend list.
        /// </summary>
        public List<uint> LoadFriendOf(uint clientID)
        {
            return LoadIDs("SELECT char_id FROM friends WHERE friend_id = @id", clientID);
        }

        public void AddFriend(uint clientID, uint friendID)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("INSERT IGNORE INTO friends (char_id, friend_id) VALUES (@id, @friend)", connection))
            {
                cmd.Parameters.AddWithValue("@id", clientID);
                cmd.Parameters.AddWithValue("@friend", friendID);
                cmd.ExecuteNonQuery();
            }
        }

        public bool DeleteFriend(uint clientID, uint friendID)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("DELETE FROM friends WHERE char_id = @id AND friend_id = @friend", connection))
            {
                cmd.Parameters.AddWithValue("@id", clientID);
                cmd.Parameters.AddWithValue("@friend", friendID);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        #endregion

        /// <summary>
        /// A GM teleport bookmark whose name contains <paramref name="name"/>: x, y, z. Null when none matches.
        /// Java reference: RequestBookmarkQuery.java.
        /// </summary>
        public int[] LoadBookmark(string name)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "SELECT x, y, z FROM tele_bookmark WHERE name LIKE @name ORDER BY LENGTH(name) LIMIT 1", connection))
            {
                cmd.Parameters.AddWithValue("@name", "%" + name.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%");
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        return null;
                    }
                    return new[] { GetInt(r, "x", 0), GetInt(r, "y", 0), GetInt(r, "z", 0) };
                }
            }
        }

        private static List<uint> LoadIDs(string sql, uint clientID)
        {
            var ids = new List<uint>();
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@id", clientID);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ids.Add(Convert.ToUInt32(r[0], CultureInfo.InvariantCulture));
                    }
                }
            }
            return ids;
        }

        private static void Execute(MySqlConnection connection, string sql)
        {
            using (var cmd = new MySqlCommand(sql, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static int GetInt(MySqlDataReader r, string column, int fallback)
        {
            var value = r[column];
            return value == null || value is DBNull ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
