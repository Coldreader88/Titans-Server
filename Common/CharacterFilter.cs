using System.Text;

namespace Common
{
    /// <summary>
    /// The characters the client itself allows (DAT_0003 CHARAFILTER_NAME.LST and CHARAFILTER_CHAT.LST, bitmaps
    /// of allowed UTF-16 code units, here as ranges). The client checks them before it sends, and says
    /// "Nameに使用できない文字が含まれています" (the name has characters that cannot be used) for a bad name; the
    /// server checks again so a modified client cannot make names or send text the real one cannot show.
    /// </summary>
    public static class CharacterFilter
    {
        /// <summary>
        /// The client's name box takes at most 16 characters (LOGON_LAYOUT CREATECHARACTER).
        /// </summary>
        public const int MaxNameLength = 16;

        private static readonly int[] NameRanges =
        {
            0x0020, 0x0020, 0x0027, 0x0027, 0x0030, 0x0039, 0x003F, 0x005A, 0x005F, 0x005F,
            0x0061, 0x007A, 0x2070, 0x2070, 0x2074, 0x2079, 0x2080, 0x2089, 0x2160, 0x217F,
            0x3041, 0x3094, 0x309D, 0x309E, 0x30A1, 0x30FE, 0x4E00, 0x9FA5, 0xFF10, 0xFF19,
            0xFF20, 0xFF3A, 0xFF40, 0xFF5A, 0xFF66, 0xFF9F
        };

        private static readonly int[] ChatRanges =
        {
            0x0020, 0x007E, 0x00A1, 0x017F, 0x018F, 0x018F, 0x0192, 0x0192, 0x01A0, 0x01A1,
            0x01AF, 0x01B0, 0x01CD, 0x01DC, 0x01F5, 0x01F5, 0x01FA, 0x01FF, 0x0300, 0x0301,
            0x0303, 0x0303, 0x0305, 0x0305, 0x0309, 0x0309, 0x0323, 0x0323, 0x0340, 0x0341,
            0x0374, 0x0375, 0x037A, 0x037A, 0x037E, 0x037E, 0x0384, 0x038A, 0x038C, 0x038C,
            0x038E, 0x03CE, 0x0400, 0x0486, 0x0490, 0x04CC, 0x04D0, 0x04EB, 0x04EE, 0x04F5,
            0x04F8, 0x04F9, 0x1100, 0x1159, 0x1161, 0x11A2, 0x11A8, 0x11F9, 0x1E80, 0x1E85,
            0x1EA0, 0x1EF9, 0x2010, 0x2026, 0x2030, 0x203D, 0x203F, 0x2046, 0x2070, 0x2070,
            0x2074, 0x208E, 0x20A0, 0x20AB, 0x2153, 0x2181, 0x2190, 0x2199, 0x2200, 0x2209,
            0x220B, 0x220C, 0x220F, 0x2211, 0x2227, 0x222A, 0x2373, 0x2375, 0x3000, 0x3003,
            0x3005, 0x3006, 0x3008, 0x3024, 0x3026, 0x302B, 0x302D, 0x3030, 0x3033, 0x3035,
            0x3037, 0x3037, 0x3041, 0x3094, 0x3099, 0x309A, 0x309D, 0x309E, 0x30A1, 0x30FE,
            0x3105, 0x312C, 0x3131, 0x3163, 0x3165, 0x318C, 0x3192, 0x319F, 0x3300, 0x3357,
            0x4E00, 0x9FA5, 0xAC00, 0xD7A3, 0xFF00, 0xFF5E, 0xFF61, 0xFF9F, 0xFFA1, 0xFFBE,
            0xFFE8, 0xFFEE
        };

        private static bool In(int[] ranges, char c)
        {
            for (int i = 0; i < ranges.Length; i += 2)
            {
                if (c >= ranges[i] && c <= ranges[i + 1])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Why a character name cannot be used, or null. Names may not start or end with a space.
        /// </summary>
        public static string NameProblem(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            {
                return "the name must be 1 to " + MaxNameLength + " characters";
            }
            if (name[0] == ' ' || name[name.Length - 1] == ' ')
            {
                return "the name cannot start or end with a space";
            }
            foreach (char c in name)
            {
                if (!In(NameRanges, c))
                {
                    return string.Format("the name has a character that cannot be used (U+{0:X4})", (int)c);
                }
            }
            return null;
        }

        /// <summary>
        /// Whether every character of the text is one the client allows in chat.
        /// </summary>
        public static bool IsChatText(string text)
        {
            foreach (char c in text ?? string.Empty)
            {
                if (!In(ChatRanges, c))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The text without the characters the client does not allow in chat (control characters and the like).
        /// </summary>
        public static string CleanChat(string text)
        {
            if (IsChatText(text))
            {
                return text ?? string.Empty;
            }
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (In(ChatRanges, c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
