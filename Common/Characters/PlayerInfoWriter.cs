using Common.Network.Packets;

namespace Common.Characters
{
    /// <summary>
    /// The player info: everything the character select screen shows about one character (Lobby 0x38002),
    /// also sent by the game server after a flight between Earth and Space (0x805F).
    ///
    /// <code>
    /// 00 02, uint32 BE account id, uint32 BE character id, gender, 00, faction
    /// 0x8F options: 60 bytes, then uint32 BE rank
    /// UC string   name
    /// uint32 BE   creation time (Unix seconds)
    /// 0x8A score: 10 x uint32 BE (see ScoreSlot), then 0x80
    /// 0x8B containers: 11 x (uint32 BE container id, uint32 BE format)
    /// 0x82 medal points (Richmond, Newman): 2 x uint32 BE, character id
    /// uint16 BE 0x0095 combat skills: 21 x uint32 BE, character id
    /// uint16 BE 0x0187 construction skills: 7 x uint32 BE, character id
    /// uint16 BE 0x028A other skills: 10 x uint32 BE, character id, 03, 0x85 5 x uint32 BE, character id
    /// 0x83 strength, spirit, luck, their sum (uint32 BE each), character id
    /// Every skill and status value carries its Status Setting arrow in its top four bits.
    /// 00 01 gender
    /// 0x94 looks: 8 x (uint16 BE wear id, style), 00 00, skin, 00, face, 00 00, hair style, hair colour, 00, 26 x 00
    /// -1, -1, 0x82 8 x 00, 0x82 00 00, 0x80 00 00, character id
    /// uint16 BE zone, uint32 BE x, y, z, uint16 BE tilt (roty), roll (rotx), direction
    /// -1, -1, uint16 BE -1, 3 x uint32 BE 0 (transport)
    /// </code>
    /// Java reference: mina_loginserver NotifyPlayerInfo.java and mina_common net/writable/PlayerInfo.java.
    /// Two places follow the official server instead (UCGOCharCreation.pcap, "Macho Man"): the options
    /// block uses the official values for a new character, and the list after the score is empty (0x80)
    /// where Java sent 0x84 and four zero shorts.
    /// </summary>
    public static class PlayerInfoWriter
    {
        /// <summary>
        /// The options block the official server sent for a freshly created character.
        /// </summary>
        private static readonly byte[] DefaultOptions = BuildDefaultOptions();

        /// <param name="created">The creation time to report, when not the character's own (see the Lobby's
        /// character deletion wait).</param>
        public static void Write<T>(UCPacket<T> p, uint accountID, Character c, int vehicleTemplateID = 0, Transport transport = null,
            int? created = null)
        {
            uint id = c.ClientID;

            // Account info
            p.PutByte(0x00);
            p.PutByte(0x02);
            p.PutUIntBE(accountID);
            p.PutUIntBE(id);
            p.PutByte((byte)c.Gender);
            p.PutByte(0x00);
            p.PutByte((byte)c.Faction);

            // Options, ending with the rank
            p.PutSize(15);
            p.PutBytes(DefaultOptions);
            p.PutIntBE(c.Rank);

            p.PutUCString(c.Name);
            p.PutIntBE(created ?? c.Created);

            // Score: the ten counters of ScoreSlot (NPC kills and deaths, criminal count, previous offense,
            // player kills and deaths), then an empty list
            p.PutSize(ScoreSlot.Count);
            for (int i = 0; i < ScoreSlot.Count; i++)
            {
                p.PutIntBE(c.Scores[i]);
            }
            p.PutSize(0);

            // Containers
            p.PutSize(PlayerContainers.PlayerInfoList.Length);
            foreach (var container in PlayerContainers.PlayerInfoList)
            {
                p.PutUIntBE(PlayerContainers.GetUniqueID(id, container));
                p.PutIntBE(container.Item3);
            }

            // Medal points: Medal of Richmond, Medal of Newman
            p.PutSize(c.Medals.Length);
            foreach (int medal in c.Medals)
            {
                p.PutIntBE(medal);
            }
            p.PutUIntBE(id);

            // Combat skills
            p.PutUShortBE(0x0095);
            PutSkills(p, c, SkillTables.Combat);
            p.PutUIntBE(id);

            // Construction skills
            p.PutUShortBE(0x0187);
            PutSkills(p, c, SkillTables.Construction);
            p.PutUIntBE(id);

            // Other skills
            p.PutUShortBE(0x028A);
            PutSkills(p, c, SkillTables.Other);
            p.PutUIntBE(id);
            p.PutByte(0x03);
            p.PutSize(5);
            PutSkills(p, c, SkillTables.Extra);
            p.PutUIntBE(id);

            // Strength, spirit, luck
            int strength = c.GetSkill(Skill.STRENGTH);
            int spirit = c.GetSkill(Skill.SPIRIT);
            int luck = c.GetSkill(Skill.LUCK);
            p.PutSize(3);
            p.PutIntBE(WithManagement(c, Skill.STRENGTH, strength));
            p.PutIntBE(WithManagement(c, Skill.SPIRIT, spirit));
            p.PutIntBE(WithManagement(c, Skill.LUCK, luck));
            p.PutIntBE(strength + spirit + luck);
            p.PutUIntBE(id);

            p.PutByte(0x00);
            p.PutByte(0x01);
            p.PutByte((byte)c.Gender);

            // Looks (Java: Appearance.getHumanLooks)
            CharacterLooks.Write(p, c);

            // Vehicle (Java: PlayerLooksWriter): character id and vehicle template id when in one
            // (the shuttle after a flight between Earth and Space, 0x805F in Earth_To_Space.pcap)
            if (vehicleTemplateID > 0)
            {
                p.PutUIntBE(id);
                p.PutIntBE(vehicleTemplateID);
            }
            else
            {
                p.PutIntBE(-1);
                p.PutIntBE(-1);
            }
            p.PutSize(2);
            p.PutIntBE(0);
            p.PutIntBE(0);
            p.PutSize(2);
            p.PutByte(0);
            p.PutByte(0);
            p.PutSize(0);
            p.PutShortBE(0);
            p.PutUIntBE(id);

            // Position; unknown (0x7FFFFFFF, 0x7FFF) after a shuttle flight: the client places the arrival
            if (transport != null)
            {
                p.PutShortBE((short)c.Zone);
                p.PutIntBE(int.MaxValue);
                p.PutIntBE(int.MaxValue);
                p.PutIntBE(int.MaxValue);
                p.PutShortBE(short.MaxValue);
                p.PutShortBE(short.MaxValue);
                p.PutShortBE(short.MaxValue);
            }
            else
            {
                p.PutShortBE((short)c.Zone);
                p.PutIntBE(c.X);
                p.PutIntBE(c.Y);
                p.PutIntBE(c.Z);
                p.PutShortBE((short)c.RotY);
                p.PutShortBE((short)c.RotX);
                p.PutShortBE((short)c.Direction);
            }

            // Transport: after a shuttle flight between Earth and Space, the destination town and launch flag the
            // client sent when it bought the shuttle (in that order here, the other way round in 0x21), then where
            // it took off; otherwise -1, -1, -1, 0, 0, 0.
            if (transport != null)
            {
                p.PutIntBE(transport.Town);
                p.PutIntBE(transport.Launch);
                p.PutShortBE(-1);
                p.PutIntBE(transport.X);
                p.PutIntBE(transport.Y);
                p.PutIntBE(transport.Z);
            }
            else
            {
                p.PutIntBE(-1);
                p.PutIntBE(-1);
                p.PutShortBE(-1);
                p.PutIntBE(0);
                p.PutIntBE(0);
                p.PutIntBE(0);
            }
        }

        /// <summary>
        /// Writes each skill level as uint32 BE with its arrow in the top four bits; null writes 0.
        /// </summary>
        private static void PutSkills<T>(UCPacket<T> p, Character c, params Skill?[] skills)
        {
            foreach (var skill in skills)
            {
                p.PutIntBE(skill.HasValue ? WithManagement(c, skill.Value, c.GetSkill(skill.Value)) : 0);
            }
        }

        /// <summary>
        /// A skill or status value as the client reads it: the level in the low 28 bits, the Status Setting
        /// arrow (<see cref="SkillManagement"/>) in the top 4 (official player infos: 0x10000047 is 7.1 set to 1).
        /// </summary>
        private static int WithManagement(Character c, Skill skill, int value)
        {
            return (c.GetManagement(skill) << 28) | (value & 0x0FFFFFFF);
        }

        private static byte[] BuildDefaultOptions()
        {
            var options = new byte[60];
            for (int i = 2; i < 16; i++)
            {
                options[i] = 0xFF;
            }
            options[48] = 0x01;
            return options;
        }
    }

    /// <summary>
    /// A shuttle flight between Earth and Space, as the player info carries it on arrival: two values the
    /// client sent when it bought the shuttle (0x21 bytes 52-59: 1, 0x31 to Space and 0, 0x2E to Earth in the
    /// captures) and where the shuttle took off.
    /// </summary>
    /// <summary>
    /// A shuttle flight: 1 for a launch to Space or 0 for a re-entry to Earth, the destination town (TOWNINFO
    /// id, see the game server's ShuttleRoutes) and the take-off point.
    /// </summary>
    public class Transport
    {
        public int Launch { get; set; }
        public int Town { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}
