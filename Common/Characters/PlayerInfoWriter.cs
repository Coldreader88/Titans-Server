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
    /// 0x8A score: 10 x uint32 BE, then 0x80
    /// 0x8B containers: 11 x (uint32 BE container id, uint32 BE format)
    /// 0x82 medals: 2 x uint32 BE, character id
    /// uint16 BE 0x0095 combat skills: 21 x uint32 BE, character id
    /// uint16 BE 0x0187 construction skills: 7 x uint32 BE, character id
    /// uint16 BE 0x028A other skills: 10 x uint32 BE, character id, 03, 0x85 5 x uint32 BE, character id
    /// 0x83 strength, spirit, luck, their sum (uint32 BE each), character id
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

        public static void Write<T>(UCPacket<T> p, uint accountID, Character c, int vehicleTemplateID = 0, Transport transport = null)
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
            p.PutIntBE(c.Created);

            // Score (Java: PlayerScoreWriter; characters.char_score is the player wins)
            p.PutSize(10);
            p.PutIntBE(0);           // enemy NPC wins
            p.PutIntBE(0);           // enemy NPC losses
            p.PutIntBE(c.Score);     // player wins
            p.PutIntBE(0);           // player losses
            p.PutIntBE(0);           // penalty
            for (int i = 0; i < 5; i++)
            {
                p.PutIntBE(0);
            }
            p.PutSize(0);

            // Containers
            p.PutSize(PlayerContainers.PlayerInfoList.Length);
            foreach (var container in PlayerContainers.PlayerInfoList)
            {
                p.PutUIntBE(PlayerContainers.GetUniqueID(id, container));
                p.PutIntBE(container.Item3);
            }

            // Medals
            p.PutSize(2);
            p.PutIntBE(0);
            p.PutIntBE(0);
            p.PutUIntBE(id);

            // Combat skills
            p.PutUShortBE(0x0095);
            PutSkills(p, c, Skill.MOBILE_SUIT, Skill.MOBILE_ARMOR, null, Skill.FIGHTER, Skill.SPACE_ENGAGEMENT,
                Skill.GROUND_ENGAGEMENT, null, Skill.AIR_ENGAGEMENT, Skill.BEAMCARTRIDGE_WEAPON, Skill.SHELLFIRING_WEAPON,
                null, Skill.WEAPON_MANIPULATION, Skill.SHOOTING, Skill.SNIPING, Skill.CQB, Skill.HANDTOHAND_COMBAT,
                Skill.TACTICS, Skill.AMBAC, Skill.DEFENCE, Skill.EVASION, Skill.EMERGENCY_REPAIR);
            p.PutUIntBE(id);

            // Construction skills
            p.PutUShortBE(0x0187);
            PutSkills(p, c, Skill.MINING, Skill.REFINERY, Skill.MSMA_CONSTRUCTION, Skill.BATTLESHIP_CONSTRUCTION,
                Skill.ARMS_CONSTRUCTION, null, null);
            p.PutUIntBE(id);

            // Other skills
            p.PutUShortBE(0x028A);
            PutSkills(p, c, null, null, null, null, null, Skill.CLOTHING_MANUFACTURING, null, null, null, null);
            p.PutUIntBE(id);
            p.PutByte(0x03);
            p.PutSize(5);
            PutSkills(p, c, null, null, null, null, null);
            p.PutUIntBE(id);

            // Strength, spirit, luck
            int strength = c.GetSkill(Skill.STRENGTH);
            int spirit = c.GetSkill(Skill.SPIRIT);
            int luck = c.GetSkill(Skill.LUCK);
            p.PutSize(3);
            p.PutIntBE(strength);
            p.PutIntBE(spirit);
            p.PutIntBE(luck);
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

            // Transport: after a shuttle flight between Earth and Space, what the client sent when it bought the
            // shuttle (two ints, then where it took off); otherwise -1, -1, -1, 0, 0, 0.
            if (transport != null)
            {
                p.PutIntBE(transport.A);
                p.PutIntBE(transport.B);
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
        /// Writes each skill level as uint32 BE; null writes 0.
        /// </summary>
        private static void PutSkills<T>(UCPacket<T> p, Character c, params Skill?[] skills)
        {
            foreach (var skill in skills)
            {
                p.PutIntBE(skill.HasValue ? c.GetSkill(skill.Value) : 0);
            }
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
    public class Transport
    {
        public int A { get; set; }
        public int B { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}
