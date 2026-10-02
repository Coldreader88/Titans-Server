using System;
using System.Collections.Generic;
using System.IO;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// The colours clothes come in and the dyes that give them, from the client's DB/Templates files.
    ///
    /// A clothes item carries its colour as one byte, the slot of its template's colour list in
    /// TEMPLATECOLORINFO.DAT (item info option 4, which the official server sent for every piece of clothing:
    /// 81 00 for colour 0). The looks (0x800A) send that slot for each worn item, and the client turns it into
    /// the colour code of the list.
    ///
    /// <code>
    /// TEMPLATECOLORINFO.DAT: uint32 BE 100, UC size n, then per template:
    /// int32 BE template id, int32 BE mask of the figure slots left visible, UC size 256,
    /// 256 x (int32 BE colour code, -1 = unused slot; UC string name)
    /// </code>
    /// Clothes are dyed when they are made (0x28 action 3): recipes of dyeable clothes take a dye (35xxxx), and
    /// the request's colour index is the slot picked in the dye window. A dye offers the slots
    /// [<see cref="Dye.First"/>, <see cref="Dye.End"/>) (basic 0-13, light 32-60, dark 64-92, monotone 96-105)
    /// and needs Clothing Manufacturing (<see cref="Dye.Skill"/>); DYETEMPLATE.DAT ends each record with
    /// byte first, byte end, byte skill table, int16 skill index, int32 skill level x10 (client: dye window
    /// 0x568dcf, skill check 0x569eaa).
    /// </summary>
    public static class ClothesColours
    {
        private static readonly object loadLock = new object();
        private static Dictionary<int, HashSet<int>> colours;
        private static Dictionary<int, Dye> dyes;

        public class Dye
        {
            public int ID { get; set; }
            public int First { get; set; }
            public int End { get; set; }

            /// <summary>
            /// Clothing Manufacturing needed, x10 (0, 190, 390, 590).
            /// </summary>
            public int Skill { get; set; }

            public bool Offers(int slot)
            {
                return slot >= First && slot < End;
            }
        }

        /// <summary>
        /// Whether the clothes template has a colour in this slot. True for everything when the table is missing.
        /// </summary>
        public static bool Has(int templateID, int slot)
        {
            EnsureLoaded();
            if (colours.Count == 0)
            {
                return true;
            }
            HashSet<int> slots;
            return colours.TryGetValue(templateID, out slots) && slots.Contains(slot);
        }

        /// <summary>
        /// The dye with this template id, or null.
        /// </summary>
        public static Dye GetDye(int templateID)
        {
            EnsureLoaded();
            Dye dye;
            return dyes.TryGetValue(templateID, out dye) ? dye : null;
        }

        public static bool IsDye(int templateID)
        {
            return templateID / 10000 == 35;
        }

        internal static void EnsureLoaded()
        {
            if (colours != null)
            {
                return;
            }
            lock (loadLock)
            {
                if (colours != null)
                {
                    return;
                }
                var c = new Dictionary<int, HashSet<int>>();
                var y = new Dictionary<int, Dye>();
                try
                {
                    var r = new Production.Reader(File.ReadAllBytes(CharacterData.FindFile("Templates", "TEMPLATECOLORINFO.DAT")), 4);
                    int count = r.Size();
                    for (int i = 0; i < count; i++)
                    {
                        int id = r.Int();
                        r.Int();
                        int n = r.Size();
                        var slots = new HashSet<int>();
                        for (int k = 0; k < n; k++)
                        {
                            int code = r.Int();
                            string name = r.String();
                            if (code != -1 || name.Length > 0)
                            {
                                slots.Add(k);
                            }
                        }
                        c[id] = slots;
                    }
                    LoadDyes(File.ReadAllBytes(CharacterData.FindFile("Templates", "DYETEMPLATE.DAT")), y);
                    Logger.ShowInfo(string.Format("Loaded the colours of {0} clothes and {1} dyes.", c.Count, y.Count));
                }
                catch (Exception ex)
                {
                    Logger.ShowWarning("Could not load the clothes colours, any colour is allowed: " + ex.Message);
                    c.Clear();
                }
                dyes = y;
                colours = c;
            }
        }

        /// <summary>
        /// Dye records all end with the colour range and the skill; a record runs to the next dye id or the end.
        /// </summary>
        private static void LoadDyes(byte[] d, Dictionary<int, Dye> result)
        {
            var starts = new List<KeyValuePair<int, int>>();
            for (int id = 350000; id < 350100; id++)
            {
                int at = Find(d, id);
                if (at < 0)
                {
                    break;
                }
                starts.Add(new KeyValuePair<int, int>(id, at));
            }
            for (int i = 0; i < starts.Count; i++)
            {
                int end = i + 1 < starts.Count ? starts[i + 1].Value : d.Length;
                result[starts[i].Key] = new Dye
                {
                    ID = starts[i].Key,
                    First = d[end - 9],
                    End = d[end - 8],
                    Skill = (d[end - 4] << 24) | (d[end - 3] << 16) | (d[end - 2] << 8) | d[end - 1],
                };
            }
        }

        private static int Find(byte[] d, int id)
        {
            for (int p = 0; p + 4 <= d.Length; p++)
            {
                if (d[p] == (byte)(id >> 24) && d[p + 1] == (byte)(id >> 16) && d[p + 2] == (byte)(id >> 8) && d[p + 3] == (byte)id)
                {
                    return p;
                }
            }
            return -1;
        }
    }
}
