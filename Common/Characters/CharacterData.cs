using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SmartEngine.Core;

namespace Common.Characters
{
    /// <summary>
    /// Game data the character code needs from the DB folder: clothing looks
    /// (DB/Templates/CLOTHESLOOKSINFOTEMPLATE.DAT) and city spawn points (DB/Properties/cities.properties).
    /// Files are searched for next to the executable, in the working directory and in ../DB, like XORTable.dat.
    /// </summary>
    public static class CharacterData
    {
        private static readonly object loadLock = new object();
        private static Dictionary<int, int> wearIDs;
        private static Dictionary<City, int[]> cities;

        /// <summary>
        /// The looks id the client draws for a clothes item, or -1 when nothing is worn.
        /// Java reference: io/ClothesLooksInfoTemplate.java (unknown items give 0).
        /// </summary>
        public static int GetWearID(int itemID)
        {
            if (itemID == -1)
            {
                return -1;
            }

            EnsureLoaded();
            int wear;
            return wearIDs.TryGetValue(itemID, out wear) ? wear : 0;
        }

        /// <summary>
        /// Spawn point (x, y, z) for a starting city.
        /// </summary>
        public static int[] GetSpawn(City city)
        {
            EnsureLoaded();
            int[] position;
            if (!cities.TryGetValue(city, out position))
            {
                throw new ArgumentException("No spawn point for " + city + " in cities.properties.", "city");
            }
            return (int[])position.Clone();
        }

        private static void EnsureLoaded()
        {
            if (wearIDs != null)
            {
                return;
            }

            lock (loadLock)
            {
                if (wearIDs != null)
                {
                    return;
                }

                cities = LoadCities(FindFile("Properties", "cities.properties"));
                wearIDs = LoadWearIDs(FindFile("Templates", "CLOTHESLOOKSINFOTEMPLATE.DAT"));
            }
        }

        /// <summary>
        /// A 7 byte header, then 9 byte records: uint32 BE wear id, uint32 BE item id, byte style.
        /// </summary>
        private static Dictionary<int, int> LoadWearIDs(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var result = new Dictionary<int, int>();
            for (int i = 7; i + 9 <= bytes.Length; i += 9)
            {
                int wear = ReadIntBE(bytes, i);
                int item = ReadIntBE(bytes, i + 4);
                if (!result.ContainsKey(item))
                {
                    result.Add(item, wear);
                }
            }

            Logger.ShowInfo(string.Format("Loaded {0} clothing looks from {1}", result.Count, Path.GetFullPath(path)));
            return result;
        }

        private static Dictionary<City, int[]> LoadCities(string path)
        {
            var result = new Dictionary<City, int[]>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                City city;
                if (eq <= 0 || !Enum.TryParse(line.Substring(0, eq).Trim(), out city))
                {
                    continue;
                }

                var parts = line.Substring(eq + 1).Split(',');
                if (parts.Length != 3)
                {
                    continue;
                }

                result[city] = new[]
                {
                    int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                    int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                    int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
                };
            }
            return result;
        }

        private static int ReadIntBE(byte[] b, int i)
        {
            return (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        }

        /// <summary>
        /// Finds DB/<folder>/<fileName> next to the executable or the working directory.
        /// </summary>
        public static string FindFile(string folder, string fileName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                fileName,
                Path.Combine("DB", folder, fileName),
                Path.Combine("..", "DB", folder, fileName),
                Path.Combine(baseDir, fileName),
                Path.Combine(baseDir, "DB", folder, fileName),
                Path.Combine(baseDir, "..", "DB", folder, fileName),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException(
                "Could not find " + fileName + ". Copy DB/" + folder + "/" + fileName +
                " next to the server executable.", fileName);
        }
    }
}
