using System;
using System.Globalization;

namespace Common.Characters
{
    /// <summary>
    /// A player character as stored in the characters, appearance, garments and skills tables
    /// (Java reference: sql/characters.sql, sql/appearance.sql, sql/garments.sql, sql/char_skills.sql).
    /// </summary>
    public class Character
    {
        public const int MaxSlots = 2;

        public Character()
        {
            Name = string.Empty;
            Zone = Zone.EARTH;
            Gender = Gender.MALE;
            Faction = Faction.FEDERATION;
            TeamID = -1;
            Garments = new Apparel[Enum.GetValues(typeof(ApparelType)).Length];
            for (int i = 0; i < Garments.Length; i++)
            {
                Garments[i] = Apparel.None;
            }
            Skills = new int[Enum.GetValues(typeof(Skill)).Length];
        }

        /// <summary>
        /// characters.char_id.
        /// </summary>
        public uint ID { get; set; }

        /// <summary>
        /// The id the client sees. The Java server prefixes the database id with its player id
        /// chain digit "1" (IDAccessChain.PLAYER), so char_id 17 is 117 on the wire. The skills and
        /// container tables are keyed by this id too.
        /// </summary>
        public uint ClientID
        {
            get { return ToClientID(ID); }
        }

        public uint AccountID { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// 1 or 2.
        /// </summary>
        public int Slot { get; set; }

        public int Score { get; set; }
        public int Lost { get; set; }
        public int Money { get; set; }
        public int Access { get; set; }
        public Zone Zone { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        /// <summary>
        /// characters.rotx (the Java server's rotation "roll").
        /// </summary>
        public int RotX { get; set; }

        /// <summary>
        /// characters.roty (the Java server's rotation "tilt").
        /// </summary>
        public int RotY { get; set; }

        public int Direction { get; set; }

        /// <summary>
        /// Creation time in Unix seconds (characters.date_created). The client uses it to decide
        /// whether the character may be deleted yet.
        /// </summary>
        public int Created { get; set; }

        public int TeamID { get; set; }

        public int Face { get; set; }
        public Faction Faction { get; set; }
        public Gender Gender { get; set; }
        public int Skin { get; set; }
        public int HairStyle { get; set; }
        public int HairColor { get; set; }
        public int Rank { get; set; }

        /// <summary>
        /// Worn clothes, indexed by <see cref="ApparelType"/>.
        /// </summary>
        public Apparel[] Garments { get; private set; }

        /// <summary>
        /// Skill levels, indexed by <see cref="Skill"/>.
        /// </summary>
        public int[] Skills { get; private set; }

        public Apparel GetApparel(ApparelType type)
        {
            return Garments[(int)type];
        }

        public void SetApparel(ApparelType type, Apparel apparel)
        {
            Garments[(int)type] = apparel;
        }

        public int GetSkill(Skill skill)
        {
            return Skills[(int)skill];
        }

        public void SetSkill(Skill skill, int level)
        {
            Skills[(int)skill] = level;
        }

        public static uint ToClientID(uint id)
        {
            return uint.Parse("1" + id.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Turns a client id back into the database id (117 -> 17). Returns false for ids that do not
        /// start with the player digit "1".
        /// </summary>
        public static bool TryFromClientID(uint clientID, out uint id)
        {
            id = 0;
            var text = clientID.ToString(CultureInfo.InvariantCulture);
            return text.Length > 1 && text[0] == '1' &&
                uint.TryParse(text.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
        }
    }

    /// <summary>
    /// One row of the container table: an item template in one of the character's containers.
    /// </summary>
    public class CharacterItem
    {
        /// <summary>
        /// The static id of the container holding the item (see <see cref="PlayerContainers"/>).
        /// </summary>
        public int ContainerID { get; set; }

        /// <summary>
        /// The item's template id.
        /// </summary>
        public int ItemID { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// Stack size, or for a vehicle its engine id (-1 = the template's engine).
        /// </summary>
        public int Amount { get; set; }
    }
}
