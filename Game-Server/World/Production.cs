using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// What the productive container (the factory) can make, read from the client's production tables:
    /// RAWMATERIALPRODUCTIONTEMPLATE.DAT (refining), WEAPONPRODUCTIONTEMPLATE.DAT (weapons and shields),
    /// VEHICLEPRODUCTIONTEMPLATE.DAT (vehicles, which can also be dismantled) and CLOTHESPRODUCTIONTEMPLATE.DAT.
    ///
    /// After the usual 4-byte header and UC size count, every record is (int32 BE, UC size lists):
    /// <code>
    /// int32       product template
    /// list        ingredients: int32 template (-1 any), int16 range (29 engines, 35 dyes; -1 when a template
    ///             is named), list of int32 sub-types (<see cref="ItemTemplate.Kind"/>), int32 amount, int32 0
    /// list        products: int32 template, int32 amount, byte 0
    /// list        given back when it fails: int32 template, int32 share in 1/10000 (always 7000), byte 1
    /// list        skills: byte group, byte 0, byte skill (the player info's construction list: 1 refinery,
    ///             2 MS/MA, 3 battleship, 4 arms; group 2 skill 5 clothing), int32 level needed in tenths, int32 1300
    /// list        byte, int32, int32 1009 (unknown)
    /// list        int32 (unknown ids)
    /// int32       seconds it takes, int32 success rate in 1/10000, int32, byte, int32, int32 (unknown)
    /// weapons and clothes: list of the EX product (int32), list of the skill level in tenths it needs
    ///             (int32, empty: no EX), int32 its chance in 1/10000
    /// vehicles:   byte 1, the skills list again for dismantling, int32 seconds, int32 success rate, int32,
    ///             list of int32
    /// </code>
    /// Every file parses to its last byte with that layout. It matches the official captures: steel is 2 iron
    /// ore each and took about 7 seconds (Steel_Refine_Success.pcap), a Zaku II F2 is 60 super high tensile
    /// steel and a rocket or jet engine and took 30 (Zaku_F2A_Craft_Success.pcap), a failed one gave 42 of the
    /// 60 back (Zaku_F2A_Craft_Fail.pcap), and a tank/fighter cannon came out as the EX one
    /// (Tank_Cannon_Success.pcap).
    /// </summary>
    public static class Production
    {
        private static readonly object loadLock = new object();
        private static Dictionary<int, Recipe> recipes;

        public static Recipe Get(int productID)
        {
            EnsureLoaded();
            Recipe r;
            return recipes.TryGetValue(productID, out r) ? r : null;
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return recipes.Count;
            }
        }

        private static void EnsureLoaded()
        {
            if (recipes != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (recipes != null)
                {
                    return;
                }
                var result = new Dictionary<int, Recipe>();
                Load("RAWMATERIALPRODUCTIONTEMPLATE.DAT", Recipe.Kinds.Refine, result);
                Load("WEAPONPRODUCTIONTEMPLATE.DAT", Recipe.Kinds.Weapon, result);
                Load("VEHICLEPRODUCTIONTEMPLATE.DAT", Recipe.Kinds.Vehicle, result);
                Load("CLOTHESPRODUCTIONTEMPLATE.DAT", Recipe.Kinds.Clothes, result);
                Logger.ShowInfo(string.Format("Loaded {0} production recipes.", result.Count));
                recipes = result;
            }
        }

        private static void Load(string file, Recipe.Kinds kind, Dictionary<int, Recipe> result)
        {
            try
            {
                var r = new Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", file)), 4);
                int count = r.Size();
                for (int i = 0; i < count; i++)
                {
                    var recipe = Read(r, kind);
                    result[recipe.ProductID] = recipe;
                }
                if (r.Position != r.Length)
                {
                    Logger.ShowWarning(string.Format("{0}: {1} bytes left after {2} recipes.", file, r.Length - r.Position, count));
                }
            }
            catch (Exception ex)
            {
                Logger.ShowWarning(string.Format("Could not load {0}: {1}", file, ex.Message));
            }
        }

        private static Recipe Read(Reader r, Recipe.Kinds kind)
        {
            var recipe = new Recipe { Kind = kind, ProductID = r.Int() };
            for (int n = r.Size(); n > 0; n--)
            {
                var input = new Ingredient { TemplateID = r.Int(), Range = r.Short() };
                for (int k = r.Size(); k > 0; k--)
                {
                    input.Kinds.Add(r.Int());
                }
                input.Amount = r.Int();
                r.Int();
                recipe.Inputs.Add(input);
            }
            for (int n = r.Size(); n > 0; n--)
            {
                r.Int();
                recipe.Yield = Math.Max(1, r.Int());
                r.Byte();
            }
            for (int n = r.Size(); n > 0; n--)
            {
                recipe.Returns.Add(new KeyValuePair<int, int>(r.Int(), r.Int()));
                r.Byte();
            }
            recipe.Skills = ReadSkills(r);
            for (int n = r.Size(); n > 0; n--)
            {
                r.Byte();
                r.Int();
                r.Int();
            }
            for (int n = r.Size(); n > 0; n--)
            {
                r.Int();
            }
            recipe.Seconds = r.Int();
            recipe.SuccessRate = r.Int();
            r.Int();
            r.Byte();
            r.Int();
            r.Int();

            if (kind == Recipe.Kinds.Weapon || kind == Recipe.Kinds.Clothes)
            {
                for (int n = r.Size(); n > 0; n--)
                {
                    recipe.ExID = r.Int();
                }
                recipe.ExSkill = -1;
                for (int n = r.Size(); n > 0; n--)
                {
                    recipe.ExSkill = r.Int();
                }
                recipe.ExRate = r.Int();
                if (recipe.ExSkill < 0 || recipe.ExID <= 0)
                {
                    recipe.ExID = 0;
                }
            }
            else if (kind == Recipe.Kinds.Vehicle)
            {
                recipe.CanDismantle = r.Byte() != 0;
                recipe.DismantleSkills = ReadSkills(r);
                recipe.DismantleSeconds = r.Int();
                recipe.DismantleRate = r.Int();
                r.Int();
                for (int n = r.Size(); n > 0; n--)
                {
                    r.Int();
                }
            }
            return recipe;
        }

        private static List<KeyValuePair<Skill, int>> ReadSkills(Reader r)
        {
            var skills = new List<KeyValuePair<Skill, int>>();
            for (int n = r.Size(); n > 0; n--)
            {
                int group = r.Byte();
                r.Byte();
                int index = r.Byte();
                int level = r.Int();
                r.Int();
                var skill = SkillOf(group, index);
                if (skill.HasValue)
                {
                    skills.Add(new KeyValuePair<Skill, int>(skill.Value, level));
                }
            }
            return skills;
        }

        /// <summary>
        /// A construction skill by its place in the player info (PlayerInfoWriter): group 1 is mining,
        /// refinery, MS/MA, battleship, arms; group 2 has clothing at 5.
        /// </summary>
        private static Skill? SkillOf(int group, int index)
        {
            if (group == 1)
            {
                switch (index)
                {
                    case 0: return Skill.MINING;
                    case 1: return Skill.REFINERY;
                    case 2: return Skill.MSMA_CONSTRUCTION;
                    case 3: return Skill.BATTLESHIP_CONSTRUCTION;
                    case 4: return Skill.ARMS_CONSTRUCTION;
                }
            }
            if (group == 2 && index == 5)
            {
                return Skill.CLOTHING_MANUFACTURING;
            }
            return null;
        }

        internal class Reader
        {
            private readonly byte[] d;

            public Reader(byte[] data, int position)
            {
                d = data;
                Position = position;
            }

            public int Position { get; private set; }
            public int Length { get { return d.Length; } }

            public int Int()
            {
                int p = Position;
                Position += 4;
                return (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
            }

            public short Short()
            {
                int p = Position;
                Position += 2;
                return (short)((d[p] << 8) | d[p + 1]);
            }

            public byte Byte()
            {
                return d[Position++];
            }

            public int Size()
            {
                int value = 0, shift = 0;
                while (true)
                {
                    byte b = d[Position++];
                    value |= (b & 0x7F) << shift;
                    shift += 7;
                    if ((b & 0x80) != 0)
                    {
                        return value;
                    }
                }
            }
        }
    }

    public class Recipe
    {
        public enum Kinds { Refine, Weapon, Vehicle, Clothes }

        public Kinds Kind { get; set; }
        public int ProductID { get; set; }

        /// <summary>
        /// How many products one batch of <see cref="Inputs"/> makes (1 in every table).
        /// </summary>
        public int Yield { get; set; }

        public List<Ingredient> Inputs { get; private set; }

        /// <summary>
        /// What a failure gives back: ingredient template and its share in 1/10000.
        /// </summary>
        public List<KeyValuePair<int, int>> Returns { get; private set; }

        /// <summary>
        /// The skills it takes and the level (in tenths) each needs for the full success rate.
        /// </summary>
        public List<KeyValuePair<Skill, int>> Skills { get; set; }

        public int Seconds { get; set; }

        /// <summary>
        /// In 1/10000.
        /// </summary>
        public int SuccessRate { get; set; }

        /// <summary>
        /// The EX product (0 for none), the skill level in tenths it needs and its chance in 1/10000.
        /// </summary>
        public int ExID { get; set; }
        public int ExSkill { get; set; }
        public int ExRate { get; set; }

        public bool CanDismantle { get; set; }
        public List<KeyValuePair<Skill, int>> DismantleSkills { get; set; }
        public int DismantleSeconds { get; set; }
        public int DismantleRate { get; set; }

        public Recipe()
        {
            Yield = 1;
            Inputs = new List<Ingredient>();
            Returns = new List<KeyValuePair<int, int>>();
            Skills = new List<KeyValuePair<Skill, int>>();
            DismantleSkills = new List<KeyValuePair<Skill, int>>();
        }

        /// <summary>
        /// The chance in 1/10000 that a player with these skills succeeds: the table's rate when their best
        /// matching skill reaches the level it asks for, less in proportion when it does not (the official
        /// server failed a Zaku II F2 whose table rate is 100% for a player below its 44.9 MS/MA
        /// construction). The scaling below the level is ours; the captures only show that it can fail.
        /// </summary>
        public static int Chance(Character c, List<KeyValuePair<Skill, int>> skills, int rate)
        {
            if (skills.Count == 0)
            {
                return rate;
            }
            int needed = skills.Max(s => s.Value);
            int best = skills.Max(s => c.GetSkill(s.Key));
            if (best >= needed)
            {
                return rate;
            }
            return (int)((long)rate * (best + 10) / (needed + 10));
        }

        /// <summary>
        /// The player's best level in the skills a recipe uses.
        /// </summary>
        public static int BestSkill(Character c, List<KeyValuePair<Skill, int>> skills)
        {
            return skills.Count == 0 ? 0 : skills.Max(s => c.GetSkill(s.Key));
        }
    }

    public class Ingredient
    {
        /// <summary>
        /// The template it must be, or -1 for any in <see cref="Range"/> of one of <see cref="Kinds"/>.
        /// </summary>
        public int TemplateID { get; set; }
        public int Range { get; set; }
        public List<int> Kinds { get; private set; }
        public int Amount { get; set; }

        public Ingredient()
        {
            Kinds = new List<int>();
        }

        public bool Matches(int templateID)
        {
            if (TemplateID != -1)
            {
                return templateID == TemplateID;
            }
            if (templateID / 10000 != Range)
            {
                return false;
            }
            var t = ItemTemplates.Get(templateID);
            return Kinds.Count == 0 || (t != null && Kinds.Contains(t.Kind));
        }
    }
}
