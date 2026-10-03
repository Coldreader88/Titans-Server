using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Names for the NPCs of a GM's #spawn squad, from DB/Npcs/names.txt (the Java server's name list; one name a line,
    /// # starts a comment). Read once, on first use.
    /// </summary>
    public static class NpcNames
    {
        private static readonly object loadLock = new object();
        private static readonly Random random = new Random();
        private static string[] names;

        private static string[] All()
        {
            lock (loadLock)
            {
                if (names != null)
                {
                    return names;
                }
                try
                {
                    names = File.ReadAllLines(CharacterData.FindFile("Npcs", "names.txt"))
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0 && !l.StartsWith("#"))
                        .Distinct()
                        .ToArray();
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not read the NPC names (DB/Npcs/names.txt): " + ex.Message);
                    names = new string[0];
                }
                return names;
            }
        }

        /// <summary>
        /// <paramref name="count"/> different random names ("Soldier n" when the list runs out).
        /// </summary>
        public static List<string> Pick(int count)
        {
            var all = All();
            var picked = new List<string>();
            lock (random)
            {
                foreach (int i in Enumerable.Range(0, all.Length).OrderBy(i => random.Next()).Take(count))
                {
                    picked.Add(all[i]);
                }
            }
            while (picked.Count < count)
            {
                picked.Add("Soldier " + (picked.Count + 1));
            }
            return picked;
        }
    }
}
