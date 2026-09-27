namespace Common.Characters
{
    /// <summary>
    /// What a new character starts with. Java reference: model/item/EFStarter.java and ZeonStarter.java.
    /// </summary>
    public static class StarterKit
    {
        public const int EFUniform = 240006;       // EF uniform SR
        public const int ZeonUniform = 240022;     // ZEON uniform SR
        public const int GMTrainer = 410042;
        public const int Zaku1 = 410001;

        /// <summary>
        /// Dresses the character and sets its starting skills.
        /// </summary>
        public static void Apply(Character character)
        {
            for (int i = 0; i < character.Garments.Length; i++)
            {
                character.Garments[i] = Apparel.None;
            }

            if (character.Faction == Faction.FEDERATION)
            {
                character.SetApparel(ApparelType.DRESS, new Apparel(EFUniform, 0));
                character.SetSkill(Skill.MOBILE_SUIT, 50);
                character.SetSkill(Skill.MSMA_CONSTRUCTION, 300);
            }
            else
            {
                character.SetApparel(ApparelType.DRESS, new Apparel(ZeonUniform, 0));
                character.SetSkill(Skill.MOBILE_SUIT, 100);
                character.SetSkill(Skill.MSMA_CONSTRUCTION, 300);
                character.SetSkill(Skill.CQB, 150);
            }
        }

        /// <summary>
        /// The mobile suit placed in the new character's hangar: item id and name.
        /// </summary>
        public static void GetHangarItem(Faction faction, out int itemID, out string name)
        {
            if (faction == Faction.FEDERATION)
            {
                itemID = GMTrainer;
                name = "GM TRAINER";
            }
            else
            {
                itemID = Zaku1;
                name = "ZAKU I";
            }
        }
    }
}
