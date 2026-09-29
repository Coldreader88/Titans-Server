using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Mining (0x32), from the client's tables MINEBLOCKTEMPLATE.DAT (what each mine block holds) and
    /// MININGTEMPLATE.DAT (how hard each ore is to get). The client works out the block it stands on from the
    /// mine grids (MINE*.TTN) and sends its id: mine id * 256 + the grid cell.
    ///
    /// After the usual 4-byte header and UC size count:
    /// <code>
    /// MINEBLOCKTEMPLATE: int32 block id, list of (int32 template, int32 amount) deposits, int32 refill (600 to
    ///     2700, -1 when empty), list of int32 types ([10], [10, 2] with a rare item), int32 rare chance,
    ///     int32 mining time in ms, int32, skill (byte group, byte 0, byte skill, int32 level in tenths, int32),
    ///     exp (byte, int32, int32), 3 x (byte, int32, int32)
    /// MININGTEMPLATE: int32 template, int32 time in ms, int32 success rate in 1/10000, int32, list of skills
    ///     (as above), list of exp (byte, int32, int32), byte, int32, int32
    /// </code>
    /// Both parse to their last byte. The client only reads the mining time; the success rates, skill levels
    /// and deposits are the server's. How much one success gives, and when a block fills up again, are not in
    /// the tables and no official capture of mining exists: those rules are ours (see <see cref="Dig"/>).
    /// </summary>
    public static class Mining
    {
        /// <summary>
        /// Result codes of 0x8032 and what the client shows for them (uc.exe 0x434389).
        /// </summary>
        public const ushort Success = 2;
        public const ushort Failed = 0x0C;
        public const ushort Exhausted = 0x2E;
        public const ushort SkillTooLow = 0x2F;
        public const ushort ContainerFull = 0x30;
        public const ushort Error = 1;

        private static readonly object loadLock = new object();
        private static readonly Random random = new Random();
        private static Dictionary<int, MineBlock> blocks;
        private static Dictionary<int, Ore> ores;

        public static MineBlock Block(int blockID)
        {
            EnsureLoaded();
            MineBlock b;
            return blocks.TryGetValue(blockID, out b) ? b : null;
        }

        public static int BlockCount
        {
            get
            {
                EnsureLoaded();
                return blocks.Count;
            }
        }

        /// <summary>
        /// One attempt on a block by a player with <paramref name="miningSkill"/> (tenths). Returns the result
        /// code, and on success the ore and how much came out of the block:
        /// <list type="bullet">
        /// <item>a block whose deposits are used up refills <see cref="MineBlock.RefillSeconds"/> after the last
        /// ore was taken from it (no deposit left: 0x2E);</item>
        /// <item>only ores whose MINING level (and the block's) the player reaches can come out (else 0x2F);</item>
        /// <item>one is picked, weighted by what is left of each, then its success rate is rolled (else 0x0C);</item>
        /// <item>a success gives 5 plus one per 10 MINING levels, at most what is left.</item>
        /// </list>
        /// </summary>
        public static ushort Dig(MineBlock block, int miningSkill, out int templateID, out int amount)
        {
            templateID = 0;
            amount = 0;
            lock (block)
            {
                int now = GameWorld.UnixTime();
                if (block.EmptySince > 0 && now - block.EmptySince >= Math.Max(60, block.RefillSeconds))
                {
                    block.Refill();
                }
                var left = block.Deposits.Where(d => d.Left > 0).ToList();
                if (left.Count == 0)
                {
                    return Exhausted;
                }
                var usable = left.Where(d => miningSkill >= block.SkillLevel && miningSkill >= SkillFor(d.TemplateID)).ToList();
                if (usable.Count == 0)
                {
                    return SkillTooLow;
                }

                Deposit pick;
                double roll;
                lock (random)
                {
                    int ticket = random.Next(usable.Sum(d => d.Left));
                    pick = usable.First(d => (ticket -= d.Left) < 0);
                    roll = random.NextDouble() * 10000;
                }
                if (roll >= RateFor(pick.TemplateID))
                {
                    return Failed;
                }

                templateID = pick.TemplateID;
                amount = Math.Min(pick.Left, 5 + miningSkill / 100);
                pick.Left -= amount;
                if (block.Deposits.All(d => d.Left <= 0))
                {
                    block.EmptySince = now;
                }
                return Success;
            }
        }

        private static int SkillFor(int templateID)
        {
            Ore ore;
            return ores.TryGetValue(templateID, out ore) ? ore.SkillLevel : 0;
        }

        private static int RateFor(int templateID)
        {
            Ore ore;
            return ores.TryGetValue(templateID, out ore) ? ore.SuccessRate : 5000;
        }

        private static void EnsureLoaded()
        {
            if (blocks != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (blocks != null)
                {
                    return;
                }
                var loadedOres = new Dictionary<int, Ore>();
                var loadedBlocks = new Dictionary<int, MineBlock>();
                try
                {
                    var r = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "MININGTEMPLATE.DAT")), 4);
                    for (int n = r.Size(); n > 0; n--)
                    {
                        var ore = new Ore { TemplateID = r.Int() };
                        r.Int();
                        ore.SuccessRate = r.Int();
                        r.Int();
                        for (int k = r.Size(); k > 0; k--)
                        {
                            ore.SkillLevel = Math.Max(ore.SkillLevel, ReadSkill(r));
                        }
                        for (int k = r.Size(); k > 0; k--)
                        {
                            SkipExp(r);
                        }
                        SkipExp(r);
                        loadedOres[ore.TemplateID] = ore;
                    }

                    r = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "MINEBLOCKTEMPLATE.DAT")), 4);
                    for (int n = r.Size(); n > 0; n--)
                    {
                        var block = new MineBlock { ID = r.Int() };
                        for (int k = r.Size(); k > 0; k--)
                        {
                            int template = r.Int(), amount = r.Int();
                            block.Deposits.Add(new Deposit { TemplateID = template, Amount = amount, Left = amount });
                        }
                        block.RefillSeconds = r.Int();
                        for (int k = r.Size(); k > 0; k--)
                        {
                            r.Int();
                        }
                        block.RareChance = r.Int();
                        block.MiningTimeMs = r.Int();
                        r.Int();
                        block.SkillLevel = ReadSkill(r);
                        for (int k = 0; k < 4; k++)
                        {
                            SkipExp(r);
                        }
                        loadedBlocks[block.ID] = block;
                    }
                    if (r.Position != r.Length)
                    {
                        Logger.ShowWarning(string.Format("MINEBLOCKTEMPLATE.DAT: {0} bytes left.", r.Length - r.Position));
                    }
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load the mining tables: " + ex.Message);
                }
                Logger.ShowInfo(string.Format("Loaded {0} mine blocks and {1} ores.", loadedBlocks.Count, loadedOres.Count));
                ores = loadedOres;
                blocks = loadedBlocks;
            }
        }

        /// <summary>
        /// A skill requirement: byte group, byte 0, byte skill, int32 level in tenths, int32. Only MINING
        /// (group 1, skill 0) is ever asked for; returns its level.
        /// </summary>
        private static int ReadSkill(Production.Reader r)
        {
            r.Byte();
            r.Byte();
            r.Byte();
            int level = r.Int();
            r.Int();
            return level;
        }

        private static void SkipExp(Production.Reader r)
        {
            r.Byte();
            r.Int();
            r.Int();
        }

        private class Ore
        {
            public int TemplateID { get; set; }
            public int SuccessRate { get; set; }
            public int SkillLevel { get; set; }
        }
    }

    public class MineBlock
    {
        public MineBlock()
        {
            Deposits = new List<Deposit>();
        }

        public int ID { get; set; }
        public int MineID { get { return ID >> 8; } }
        public List<Deposit> Deposits { get; private set; }
        public int RefillSeconds { get; set; }
        public int RareChance { get; set; }
        public int MiningTimeMs { get; set; }
        public int SkillLevel { get; set; }

        /// <summary>
        /// When the last ore was taken out (Unix seconds; 0 while some is left).
        /// </summary>
        public int EmptySince { get; set; }

        public void Refill()
        {
            foreach (var d in Deposits)
            {
                d.Left = d.Amount;
            }
            EmptySince = 0;
        }
    }

    public class Deposit
    {
        public int TemplateID { get; set; }
        public int Amount { get; set; }
        public int Left { get; set; }
    }
}
