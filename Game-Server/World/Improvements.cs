using System;
using System.Collections.Generic;
using System.IO;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Vehicle upgrades (0x28 action 8), read from the client's VEHICLEIMPROVEMENTLEVELINFO.DAT: for each upgrade
    /// table (a vehicle template's <see cref="VehicleTemplate.ModelIndex"/>) and level 1-8, the skills it needs and
    /// the three materials, one per kind of upgrade. The client keeps the levels in the vehicle's stats list (the
    /// 19th int, <see cref="StatsIndex"/>) as nibbles: power (type 8) in bits 0-3, defence (type 10) in bits 4-7,
    /// hit (type 9) in bits 8-11; the three together can reach 8 at most (UCClient 0x7a79b0-0x7a79d0, 0x7e3d18).
    /// The official Zaku II F2 captures show 0, 1, 0x11 and 0x111 there after a power, a defence and a hit upgrade.
    ///
    /// Record, after the 4-byte header and UC size count: int32 table, byte level, list of skills (as in
    /// <see cref="Production"/>), list of (byte, int32, int32), int32 seconds, int32 success rate in 1/10000,
    /// int32, byte, int32, int32, three (int32 material template, int32 amount), byte (1 on level 8).
    /// Levels 1-7 use the enhancement (510021), accuracy (510022) and defensive (510023) improvement packages,
    /// level 8 one Ginius, Mosk or Tem part (510026-510028).
    /// </summary>
    public static class Improvements
    {
        public const int StatsIndex = 18;
        public const int MaxTotal = 8;
        public const int Power = 8;
        public const int Hit = 9;
        public const int Defence = 10;

        private static readonly object loadLock = new object();
        private static Dictionary<long, Level> levels;

        public class Level
        {
            public int Table;
            public int Number;
            public List<KeyValuePair<Skill, int>> Skills;
            public int Seconds;
            public int SuccessRate;

            /// <summary>
            /// The material of each type: power, hit, defence (template, amount).
            /// </summary>
            public KeyValuePair<int, int>[] Materials = new KeyValuePair<int, int>[3];
        }

        public static Level Get(int table, int level)
        {
            EnsureLoaded();
            Level l;
            return levels.TryGetValue(((long)table << 8) | (uint)level, out l) ? l : null;
        }

        /// <summary>
        /// GameServer.xml UpgradeChance: the chance in percent of a level 1 upgrade.
        /// </summary>
        public static int FirstLevelChance = 65;

        /// <summary>
        /// The chance in 1/10000 that an upgrade to <paramref name="level"/> works for a player with the skills:
        /// <see cref="FirstLevelChance"/> at level 1, 5% less for each level above, at least 5%. Ours: the table
        /// says 100%, but the official server failed half of the level 1 upgrades in the captures.
        /// </summary>
        public static int Chance(int level)
        {
            return Math.Max(Math.Min(500, FirstLevelChance * 100), FirstLevelChance * 100 - 500 * (level - 1));
        }

        /// <summary>
        /// The level of one type (<see cref="Power"/>, <see cref="Hit"/>, <see cref="Defence"/>) in the packed value.
        /// </summary>
        public static int LevelOf(int packed, int type)
        {
            return (packed >> ShiftOf(type)) & 0xF;
        }

        public static int WithLevel(int packed, int type, int level)
        {
            int shift = ShiftOf(type);
            return (packed & ~(0xF << shift)) | ((level & 0xF) << shift);
        }

        public static int Total(int packed)
        {
            return LevelOf(packed, Power) + LevelOf(packed, Hit) + LevelOf(packed, Defence);
        }

        private static int ShiftOf(int type)
        {
            switch (type)
            {
                case Power: return 0;
                case Defence: return 4;
                case Hit: return 8;
            }
            throw new ArgumentException("upgrade type " + type);
        }

        /// <summary>
        /// The type a material is for at this level (material 1 power, 2 hit, 3 defence), or 0.
        /// </summary>
        public static int TypeOf(Level level, int templateID)
        {
            for (int i = 0; i < 3; i++)
            {
                if (level.Materials[i].Key == templateID)
                {
                    return Power + i;
                }
            }
            return 0;
        }

        private static void EnsureLoaded()
        {
            if (levels != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (levels != null)
                {
                    return;
                }
                var result = new Dictionary<long, Level>();
                try
                {
                    var r = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "VEHICLEIMPROVEMENTLEVELINFO.DAT")), 4);
                    for (int n = r.Size(); n > 0; n--)
                    {
                        var l = new Level { Table = r.Int(), Number = r.Byte() };
                        l.Skills = Production.ReadSkills(r);
                        for (int k = r.Size(); k > 0; k--)
                        {
                            r.Byte();
                            r.Int();
                            r.Int();
                        }
                        l.Seconds = r.Int();
                        l.SuccessRate = r.Int();
                        r.Int();
                        r.Byte();
                        r.Int();
                        r.Int();
                        for (int i = 0; i < 3; i++)
                        {
                            int template = r.Int();
                            l.Materials[i] = new KeyValuePair<int, int>(template, r.Int());
                        }
                        r.Byte();
                        result[((long)l.Table << 8) | (uint)l.Number] = l;
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load the vehicle upgrades: " + ex.Message);
                }
                Logger.ShowInfo(string.Format("Loaded {0} vehicle upgrade levels.", result.Count));
                levels = result;
            }
        }
    }
}
