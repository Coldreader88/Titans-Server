using System;
using System.Collections.Generic;
using System.Globalization;
using Common.Characters;
using MySql.Data.MySqlClient;
using SmartEngine.Core;

namespace Common.Database
{
    /// <summary>
    /// Reads and creates characters. Uses the Java server's tables (DB/SQL/characters.sql) and its
    /// conventions so existing Java databases keep working:
    /// characters.char_id is the database id; appearance and garments rows use that id, while skills
    /// and container rows use the client id (see <see cref="Character.ClientID"/>).
    /// Uses the connection settings of <see cref="DatabaseConnection"/>.
    /// </summary>
    public class CharacterDatabase
    {
        static readonly CharacterDatabase instance = new CharacterDatabase();

        public static CharacterDatabase Instance { get { return instance; } }

        private const string SelectCharacter =
            "SELECT c.char_id, c.acc_id, c.char_name, c.slot, c.char_score, c.char_lost, c.char_money, " +
            "c.char_access, c.zone, c.x, c.y, c.z, c.rotx, c.roty, c.direction, c.date_created, c.team_id, " +
            "a.face, a.faction, a.gender, a.skin, a.hairstyle, a.haircolor, a.`rank`, " +
            "g.dress, g.top, g.coat, g.bottom, g.shoes, g.gloves, g.hat, g.glasses " +
            "FROM characters c " +
            "LEFT JOIN appearance a ON a.char_id = c.char_id " +
            "LEFT JOIN garments g ON g.char_id = c.char_id ";

        /// <summary>
        /// The account's characters in slot order (at most <see cref="Character.MaxSlots"/>).
        /// Like the Java server (CharacterResourceLoader), a lone character in slot 2 is moved to slot 1.
        /// </summary>
        public List<Character> LoadCharacters(uint accountID)
        {
            var result = new List<Character>();

            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(
                    SelectCharacter + "WHERE c.acc_id = @acc AND c.slot BETWEEN 1 AND @slots ORDER BY c.slot, c.char_id",
                    connection))
                {
                    cmd.Parameters.AddWithValue("@acc", accountID);
                    cmd.Parameters.AddWithValue("@slots", Character.MaxSlots);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var character = ReadCharacter(reader);
                            if (!result.Exists(c => c.Slot == character.Slot))
                            {
                                result.Add(character);
                            }
                        }
                    }
                }

                if (result.Count == 1 && result[0].Slot != 1)
                {
                    Logger.ShowInfo(string.Format("Moving character {0} back to slot 1.", result[0].Name));
                    result[0].Slot = 1;
                    using (var cmd = new MySqlCommand("UPDATE characters SET slot = 1 WHERE char_id = @id", connection))
                    {
                        cmd.Parameters.AddWithValue("@id", result[0].ID);
                        cmd.ExecuteNonQuery();
                    }
                }

                foreach (var character in result)
                {
                    LoadSkills(connection, character);
                }
            }

            return result;
        }

        /// <summary>
        /// Loads one character by its client id (see <see cref="Character.ClientID"/>), or returns null
        /// when there is no such character.
        /// </summary>
        public Character LoadCharacterByClientID(uint clientID)
        {
            uint id;
            if (!Character.TryFromClientID(clientID, out id))
            {
                return null;
            }

            using (var connection = DatabaseConnection.Open())
            {
                Character character = null;
                using (var cmd = new MySqlCommand(SelectCharacter + "WHERE c.char_id = @id LIMIT 1", connection))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            character = ReadCharacter(reader);
                        }
                    }
                }

                if (character != null)
                {
                    LoadSkills(connection, character);
                }
                return character;
            }
        }

        /// <summary>
        /// Deletes a character of <paramref name="accountID"/> with its appearance, garments, skills and
        /// items. Returns false when the account has no such character.
        /// Java reference: CharacterResourceSaver.delete (which left skills and items behind).
        /// </summary>
        public bool Delete(uint accountID, Character character)
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand("DELETE FROM characters WHERE acc_id = @acc AND char_id = @id", connection))
                {
                    cmd.Parameters.AddWithValue("@acc", accountID);
                    cmd.Parameters.AddWithValue("@id", character.ID);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        return false;
                    }
                }

                ExecuteForID(connection, "DELETE FROM appearance WHERE char_id = @id", character.ID);
                ExecuteForID(connection, "DELETE FROM garments WHERE char_id = @id", character.ID.ToString(CultureInfo.InvariantCulture));
                ExecuteForID(connection, "DELETE FROM skills WHERE char_id = @id", character.ClientID);
                ExecuteForID(connection, "DELETE FROM container WHERE char_id = @id", character.ClientID);
                return true;
            }
        }

        /// <summary>
        /// Saves where the character is (zone, position and rotation).
        /// </summary>
        public void SavePosition(Character character)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "UPDATE characters SET zone = @zone, x = @x, y = @y, z = @z, rotx = @rotx, roty = @roty, direction = @dir " +
                "WHERE char_id = @id", connection))
            {
                cmd.Parameters.AddWithValue("@zone", (int)character.Zone);
                cmd.Parameters.AddWithValue("@x", character.X);
                cmd.Parameters.AddWithValue("@y", character.Y);
                cmd.Parameters.AddWithValue("@z", character.Z);
                cmd.Parameters.AddWithValue("@rotx", character.RotX);
                cmd.Parameters.AddWithValue("@roty", character.RotY);
                cmd.Parameters.AddWithValue("@dir", character.Direction);
                cmd.Parameters.AddWithValue("@id", character.ID);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// The character's items (container table rows), in insertion order.
        /// </summary>
        public List<CharacterItem> LoadItems(Character character)
        {
            var result = new List<CharacterItem>();
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand(
                "SELECT container_id, item_id, item_name, item_amount FROM container WHERE char_id = @id", connection))
            {
                cmd.Parameters.AddWithValue("@id", character.ClientID);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new CharacterItem
                        {
                            ContainerID = GetInt(reader, "container_id", 0),
                            ItemID = GetInt(reader, "item_id", -1),
                            Name = GetString(reader, "item_name"),
                            Amount = GetInt(reader, "item_amount", 0),
                        });
                    }
                }
            }
            return result;
        }

        private static void ExecuteForID(MySqlConnection connection, string sql, object id)
        {
            using (var cmd = new MySqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// True when any character already uses <paramref name="name"/> (case-insensitive).
        /// </summary>
        public bool NameExists(string name)
        {
            using (var connection = DatabaseConnection.Open())
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM characters WHERE LOWER(char_name) = LOWER(@name)", connection))
            {
                cmd.Parameters.AddWithValue("@name", name);
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// Inserts a new character with its appearance, garments and skills, and puts
        /// <paramref name="hangarItemID"/> in its hangar. Sets <see cref="Character.ID"/>.
        /// Java reference: CharacterResourceSaver.create and CharacterSQLManager.save.
        /// </summary>
        public void Create(Character character, int hangarItemID, string hangarItemName)
        {
            using (var connection = DatabaseConnection.Open())
            {
                using (var cmd = new MySqlCommand(
                    "INSERT INTO characters (acc_id, char_name, slot, char_score, char_lost, char_money, char_access, " +
                    "zone, x, y, z, rotx, roty, direction, date_created, team_id) VALUES " +
                    "(@acc, @name, @slot, @score, @lost, @money, @access, @zone, @x, @y, @z, @rotx, @roty, @dir, @created, @team)",
                    connection))
                {
                    cmd.Parameters.AddWithValue("@acc", character.AccountID);
                    cmd.Parameters.AddWithValue("@name", character.Name);
                    cmd.Parameters.AddWithValue("@slot", character.Slot);
                    cmd.Parameters.AddWithValue("@score", character.Score);
                    cmd.Parameters.AddWithValue("@lost", character.Lost);
                    cmd.Parameters.AddWithValue("@money", character.Money);
                    cmd.Parameters.AddWithValue("@access", character.Access);
                    cmd.Parameters.AddWithValue("@zone", (int)character.Zone);
                    cmd.Parameters.AddWithValue("@x", character.X);
                    cmd.Parameters.AddWithValue("@y", character.Y);
                    cmd.Parameters.AddWithValue("@z", character.Z);
                    cmd.Parameters.AddWithValue("@rotx", character.RotX);
                    cmd.Parameters.AddWithValue("@roty", character.RotY);
                    cmd.Parameters.AddWithValue("@dir", character.Direction);
                    cmd.Parameters.AddWithValue("@created", character.Created.ToString(CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@team", character.TeamID);
                    cmd.ExecuteNonQuery();

                    character.ID = (uint)cmd.LastInsertedId;
                }

                using (var cmd = new MySqlCommand(
                    "INSERT INTO appearance (char_id, face, faction, gender, skin, hairstyle, haircolor, `rank`) VALUES " +
                    "(@id, @face, @faction, @gender, @skin, @hairstyle, @haircolor, @rank)", connection))
                {
                    cmd.Parameters.AddWithValue("@id", character.ID);
                    cmd.Parameters.AddWithValue("@face", character.Face);
                    cmd.Parameters.AddWithValue("@faction", (int)character.Faction);
                    cmd.Parameters.AddWithValue("@gender", (int)character.Gender);
                    cmd.Parameters.AddWithValue("@skin", character.Skin);
                    cmd.Parameters.AddWithValue("@hairstyle", character.HairStyle);
                    cmd.Parameters.AddWithValue("@haircolor", character.HairColor);
                    cmd.Parameters.AddWithValue("@rank", character.Rank);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand(
                    "INSERT INTO garments (char_id, hat, glasses, coat, top, bottom, gloves, dress, shoes) VALUES " +
                    "(@id, @hat, @glasses, @coat, @top, @bottom, @gloves, @dress, @shoes)", connection))
                {
                    cmd.Parameters.AddWithValue("@id", character.ID.ToString(CultureInfo.InvariantCulture));
                    foreach (ApparelType type in Enum.GetValues(typeof(ApparelType)))
                    {
                        cmd.Parameters.AddWithValue("@" + type.ToString().ToLowerInvariant(), character.GetApparel(type).ToString());
                    }
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new MySqlCommand(
                    "INSERT INTO skills (char_id, skill_idx, skill_level, skill_exp) VALUES (@id, @idx, @level, 0)", connection))
                {
                    cmd.Parameters.AddWithValue("@id", character.ClientID);
                    var idx = cmd.Parameters.Add("@idx", MySqlDbType.Int32);
                    var level = cmd.Parameters.Add("@level", MySqlDbType.Int32);
                    foreach (Skill skill in Enum.GetValues(typeof(Skill)))
                    {
                        idx.Value = (int)skill;
                        level.Value = character.GetSkill(skill);
                        cmd.ExecuteNonQuery();
                    }
                }

                // item_amount holds a vehicle's engine id; -1 keeps the template's engine when the Java
                // game server loads it (RequestItemsQuery).
                using (var cmd = new MySqlCommand(
                    "INSERT INTO container (char_id, container_id, container_name, item_id, item_name, item_amount, child) " +
                    "VALUES (@id, @container, 'hangar', @item, @itemName, -1, '')", connection))
                {
                    cmd.Parameters.AddWithValue("@id", character.ClientID);
                    cmd.Parameters.AddWithValue("@container", PlayerContainers.Hangar);
                    cmd.Parameters.AddWithValue("@item", hangarItemID);
                    cmd.Parameters.AddWithValue("@itemName", hangarItemName);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static Character ReadCharacter(MySqlDataReader r)
        {
            var character = new Character
            {
                ID = Convert.ToUInt32(r["char_id"]),
                AccountID = Convert.ToUInt32(r["acc_id"]),
                Name = GetString(r, "char_name"),
                Slot = GetInt(r, "slot", 1),
                Score = GetInt(r, "char_score", 0),
                Lost = GetInt(r, "char_lost", 0),
                Money = GetInt(r, "char_money", 0),
                Access = GetInt(r, "char_access", 0),
                X = GetInt(r, "x", 0),
                Y = GetInt(r, "y", 0),
                Z = GetInt(r, "z", 0),
                RotX = GetInt(r, "rotx", 0),
                RotY = GetInt(r, "roty", 0),
                Direction = GetInt(r, "direction", 0),
                TeamID = GetInt(r, "team_id", -1),
                Face = GetInt(r, "face", 0),
                Skin = GetInt(r, "skin", 1),
                HairStyle = GetInt(r, "hairstyle", 1),
                HairColor = GetInt(r, "haircolor", 1),
                Rank = GetInt(r, "rank", 0),
            };

            character.Zone = GetInt(r, "zone", 1) == (int)Zone.SPACE ? Zone.SPACE : Zone.EARTH;
            character.Faction = GetInt(r, "faction", 1) == (int)Faction.ZEON ? Faction.ZEON : Faction.FEDERATION;
            character.Gender = GetInt(r, "gender", 1) == (int)Gender.FEMALE ? Gender.FEMALE : Gender.MALE;

            int created;
            character.Created = int.TryParse(GetString(r, "date_created"), NumberStyles.Integer, CultureInfo.InvariantCulture, out created)
                ? created
                : 0;

            foreach (ApparelType type in Enum.GetValues(typeof(ApparelType)))
            {
                character.SetApparel(type, Apparel.Parse(GetString(r, type.ToString().ToLowerInvariant())));
            }

            return character;
        }

        private static void LoadSkills(MySqlConnection connection, Character character)
        {
            using (var cmd = new MySqlCommand("SELECT skill_idx, skill_level FROM skills WHERE char_id = @id", connection))
            {
                cmd.Parameters.AddWithValue("@id", character.ClientID);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int idx = Convert.ToInt32(reader.GetValue(0));
                        if (idx >= 0 && idx < character.Skills.Length)
                        {
                            character.Skills[idx] = Convert.ToInt32(reader.GetValue(1));
                        }
                    }
                }
            }
        }

        private static string GetString(MySqlDataReader r, string column)
        {
            var value = r[column];
            return value == null || value is DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(MySqlDataReader r, string column, int fallback)
        {
            var value = r[column];
            return value == null || value is DBNull ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
