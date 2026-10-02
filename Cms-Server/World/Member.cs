namespace TitansUC.CmsServer.World
{
    /// <summary>
    /// What the CMS server needs to know about a character: for its own session, a friend, a team or a
    /// group chat member.
    /// </summary>
    public class Member
    {
        /// <summary>
        /// The id the client sees (characters.char_id with a leading "1", see Common.Characters.Character.ClientID).
        /// </summary>
        public uint ClientID { get; set; }

        public uint AccountID { get; set; }

        public string Name { get; set; }

        public byte Gender { get; set; }

        public byte Rank { get; set; }

        /// <summary>
        /// Team id (characters.team_id), -1 for none.
        /// </summary>
        public int TeamID { get; set; }

        /// <summary>
        /// characters.char_access: the GM tag (see <see cref="AccessLevel"/>).
        /// </summary>
        public int Access { get; set; }

        public Member Copy()
        {
            return (Member)MemberwiseClone();
        }
    }

    /// <summary>
    /// A player team (table team).
    /// </summary>
    public class Team
    {
        public int ID { get; set; }

        public string Name { get; set; }

        public uint LeaderID { get; set; }

        public string LeaderName { get; set; }

        /// <summary>
        /// Unix seconds.
        /// </summary>
        public uint Created { get; set; }
    }

    /// <summary>
    /// Chat types (the uint32 after the text in 0x03 / 0x8004), from the official captures. Types 0, 1
    /// and 3 also occur; what they mean is not confirmed. The client picks the recipients for every
    /// type; the server only passes the message on.
    /// </summary>
    /// <summary>
    /// Chat types (CM_CHAT_MSG / SM_CHAT_MSG). The client picks the recipients of every type itself; the server
    /// passes each message on as it is. From the captures: 3 is the faction chat ("Send to Zeon Chat", "Journal
    /// Zeon Talk"). 0 was only ever sent by the official server, from many players the capturing player was not
    /// near or in a team with, so it is probably a wide area or server channel; 1 was rare (a few greetings,
    /// "jhgh" sent to no one). Neither is known for sure.
    /// </summary>
    public static class ChatType
    {
        public const uint Wide = 0;
        public const uint Unknown1 = 1;
        public const uint Say = 2;
        public const uint Faction = 3;
        public const uint Tell = 4;
        public const uint Team = 5;
        public const uint GroupChat = 6;
        public const uint System = 7;
    }
}
