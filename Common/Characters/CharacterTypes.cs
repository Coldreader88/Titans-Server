using System;

namespace Common.Characters
{
    // Character traits and ids, with the values the client and the Java reference use
    // (mina_common model/appearance, model/skill/SkillSet.java, model/City.java, model/Zone.java).

    public enum Gender : byte
    {
        MALE = 1,
        FEMALE = 2,
    }

    public enum Faction : byte
    {
        FEDERATION = 1,
        ZEON = 2,
        UNKNOWN = 3,
    }

    public enum Zone : short
    {
        EARTH = 1,
        SPACE = 2,
    }

    /// <summary>
    /// Starting cities offered by character creation, in client order. Spawn points are in
    /// DB/Properties/cities.properties.
    /// </summary>
    public enum City : byte
    {
        SYDNEY,
        PERTH,
        CANBERRA,
        ADELAIDE,
        MELBOURNE,
        DARWIN,
        BRISBANE,
        SOUTHERN_CROSS,
    }

    /// <summary>
    /// Clothing slots, in the order of the garments table columns and the player info looks list.
    /// </summary>
    public enum ApparelType
    {
        DRESS,
        TOP,
        COAT,
        BOTTOM,
        SHOES,
        GLOVES,
        HAT,
        GLASSES,
    }

    /// <summary>
    /// Skills. The enum value is the skills.skill_idx the Java server stores (its SkillSet ordinal),
    /// so keep this order.
    /// </summary>
    public enum Skill
    {
        STRENGTH,
        SPIRIT,
        LUCK,
        MOBILE_SUIT,
        MOBILE_ARMOR,
        FIGHTER,
        SPACE_ENGAGEMENT,
        GROUND_ENGAGEMENT,
        AIR_ENGAGEMENT,
        SHOOTING,
        SNIPING,
        CQB,
        DEFENCE,
        WEAPON_MANIPULATION,
        HANDTOHAND_COMBAT,
        TACTICS,
        SHELLFIRING_WEAPON,
        BEAMCARTRIDGE_WEAPON,
        AMBAC,
        EVASION,
        EMERGENCY_REPAIR,
        REFINERY,
        MSMA_CONSTRUCTION,
        BATTLESHIP_CONSTRUCTION,
        ARMS_CONSTRUCTION,
        MINING,
        CLOTHING_MANUFACTURING,
    }

    /// <summary>
    /// One worn piece of clothing: the clothes item id (-1 = nothing) and its colour style.
    /// </summary>
    public struct Apparel
    {
        public static readonly Apparel None = new Apparel(-1, 0);

        public Apparel(int itemID, int style)
            : this()
        {
            ItemID = itemID;
            Style = style;
        }

        public int ItemID { get; set; }
        public int Style { get; set; }

        /// <summary>
        /// Garments table format: "itemid,style".
        /// </summary>
        public override string ToString()
        {
            return ItemID + "," + Style;
        }

        public static Apparel Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return None;
            }

            var parts = value.Split(',');
            int id, style;
            if (parts.Length < 2 || !int.TryParse(parts[0].Trim(), out id) || !int.TryParse(parts[1].Trim(), out style))
            {
                return None;
            }
            return new Apparel(id, style);
        }
    }

    /// <summary>
    /// The player's fixed containers. A container's id on the client is the character's client id
    /// plus the static id (Java: StaticContainer, DB/Properties/container.properties).
    /// </summary>
    public static class PlayerContainers
    {
        public const int Multi = 0x14;
        public const int Singleton = 0x13;

        public const int Backpack = 110001;
        public const int Bank = 110002;
        public const int Hangar = 110003;
        public const int SwapPack = 110004;
        public const int Productive = 110006;
        public const int SelfStorage = 110007;
        public const int House = 110008;
        public const int Realestate = 110009;
        public const int Weared = 120001;
        public const int Money = 500000;
        public const int Credit = 500000;

        /// <summary>
        /// The containers sent in the player info, in client order: name, static id, format.
        /// </summary>
        public static readonly Tuple<string, int, int>[] PlayerInfoList =
        {
            Tuple.Create("backpack", Backpack, Multi),
            Tuple.Create("weared", Weared, Multi),
            Tuple.Create("bank", Bank, Multi),
            Tuple.Create("money", Money, Singleton),
            Tuple.Create("hangar", Hangar, Multi),
            Tuple.Create("selfstorage", SelfStorage, Multi),
            Tuple.Create("house", House, Multi),
            Tuple.Create("productive", Productive, Multi),
            Tuple.Create("realestate", Realestate, Multi),
            Tuple.Create("swappack", SwapPack, Multi),
            Tuple.Create("credit", Credit, Singleton),
        };

        /// <summary>
        /// The unique id of one of a character's containers: character id + static id. Money and credit
        /// share the static id 500000, so credit gets one more to keep the ids apart (the client asks
        /// for each container by its unique id, and the official server used different ones).
        /// </summary>
        public static uint GetUniqueID(uint characterClientID, Tuple<string, int, int> container)
        {
            uint id = unchecked(characterClientID + (uint)container.Item2);
            return container.Item1 == "credit" ? id + 1 : id;
        }
    }
}
