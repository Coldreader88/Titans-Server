using Common.Characters;
using Common.Network.Packets;

namespace TitansUC.LobbyServer.Network.Packets.Client
{
    /// <summary>
    /// 0x38002: everything the character select screen shows about one character.
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
    /// -1, -1, uint16 BE -1, 3 x uint32 BE 0
    /// </code>
    /// Java reference: mina_loginserver NotifyPlayerInfo.java and mina_common net/writable/PlayerInfo.java.
    /// Two places follow the official server instead (UCGOCharCreation.pcap, "Macho Man"): the options
    /// block uses the official values for a new character, and the list after the score is empty (0x80)
    /// where Java sent 0x84 and four zero shorts.
    /// </summary>
    public class SM_PLAYER_INFO : UCPacket<LSOpcode>
    {
        /// <summary>
        /// The options block the official server sent for a freshly created character.
        /// </summary>
        private static readonly byte[] DefaultOptions = BuildDefaultOptions();

        public SM_PLAYER_INFO(uint accountID, Character c)
        {
            this.ID = LSOpcode.SM_PLAYER_INFO;

            uint id = c.ClientID;

            // Account info
            this.PutByte(0x00);
            this.PutByte(0x02);
            this.PutUIntBE(accountID);
            this.PutUIntBE(id);
            this.PutByte((byte)c.Gender);
            this.PutByte(0x00);
            this.PutByte((byte)c.Faction);

            // Options, ending with the rank
            this.PutSize(15);
            this.PutBytes(DefaultOptions);
            this.PutIntBE(c.Rank);

            this.PutUCString(c.Name);
            this.PutIntBE(c.Created);

            // Score (Java: PlayerScoreWriter; characters.char_score is the player wins)
            this.PutSize(10);
            this.PutIntBE(0);           // enemy NPC wins
            this.PutIntBE(0);           // enemy NPC losses
            this.PutIntBE(c.Score);     // player wins
            this.PutIntBE(0);           // player losses
            this.PutIntBE(0);           // penalty
            for (int i = 0; i < 5; i++)
            {
                this.PutIntBE(0);
            }
            this.PutSize(0);

            // Containers
            this.PutSize(PlayerContainers.PlayerInfoList.Length);
            foreach (var container in PlayerContainers.PlayerInfoList)
            {
                this.PutUIntBE(PlayerContainers.GetUniqueID(id, container));
                this.PutIntBE(container.Item3);
            }

            // Medals
            this.PutSize(2);
            this.PutIntBE(0);
            this.PutIntBE(0);
            this.PutUIntBE(id);

            // Combat skills
            this.PutUShortBE(0x0095);
            PutSkills(c, Skill.MOBILE_SUIT, Skill.MOBILE_ARMOR, null, Skill.FIGHTER, Skill.SPACE_ENGAGEMENT,
                Skill.GROUND_ENGAGEMENT, null, Skill.AIR_ENGAGEMENT, Skill.BEAMCARTRIDGE_WEAPON, Skill.SHELLFIRING_WEAPON,
                null, Skill.WEAPON_MANIPULATION, Skill.SHOOTING, Skill.SNIPING, Skill.CQB, Skill.HANDTOHAND_COMBAT,
                Skill.TACTICS, Skill.AMBAC, Skill.DEFENCE, Skill.EVASION, Skill.EMERGENCY_REPAIR);
            this.PutUIntBE(id);

            // Construction skills
            this.PutUShortBE(0x0187);
            PutSkills(c, Skill.MINING, Skill.REFINERY, Skill.MSMA_CONSTRUCTION, Skill.BATTLESHIP_CONSTRUCTION,
                Skill.ARMS_CONSTRUCTION, null, null);
            this.PutUIntBE(id);

            // Other skills
            this.PutUShortBE(0x028A);
            PutSkills(c, null, null, null, null, null, Skill.CLOTHING_MANUFACTURING, null, null, null, null);
            this.PutUIntBE(id);
            this.PutByte(0x03);
            this.PutSize(5);
            PutSkills(c, null, null, null, null, null);
            this.PutUIntBE(id);

            // Strength, spirit, luck
            int strength = c.GetSkill(Skill.STRENGTH);
            int spirit = c.GetSkill(Skill.SPIRIT);
            int luck = c.GetSkill(Skill.LUCK);
            this.PutSize(3);
            this.PutIntBE(strength);
            this.PutIntBE(spirit);
            this.PutIntBE(luck);
            this.PutIntBE(strength + spirit + luck);
            this.PutUIntBE(id);

            this.PutByte(0x00);
            this.PutByte(0x01);
            this.PutByte((byte)c.Gender);

            // Looks (Java: Appearance.getHumanLooks)
            CharacterLooks.Write(this, c);

            // Vehicle (Java: PlayerLooksWriter): character id and vehicle id when in a mobile suit
            this.PutIntBE(-1);
            this.PutIntBE(-1);
            this.PutSize(2);
            this.PutIntBE(0);
            this.PutIntBE(0);
            this.PutSize(2);
            this.PutByte(0);
            this.PutByte(0);
            this.PutSize(0);
            this.PutShortBE(0);
            this.PutUIntBE(id);

            // Position
            this.PutShortBE((short)c.Zone);
            this.PutIntBE(c.X);
            this.PutIntBE(c.Y);
            this.PutIntBE(c.Z);
            this.PutShortBE((short)c.RotY);
            this.PutShortBE((short)c.RotX);
            this.PutShortBE((short)c.Direction);

            // Transport
            this.PutIntBE(-1);
            this.PutIntBE(-1);
            this.PutShortBE(-1);
            this.PutIntBE(0);
            this.PutIntBE(0);
            this.PutIntBE(0);
        }

        /// <summary>
        /// Writes each skill level as uint32 BE; null writes 0.
        /// </summary>
        private void PutSkills(Character c, params Skill?[] skills)
        {
            foreach (var skill in skills)
            {
                this.PutIntBE(skill.HasValue ? c.GetSkill(skill.Value) : 0);
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
}
