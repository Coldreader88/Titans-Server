using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartEngine.Network.Utils;

namespace Common
{
    public static class Utils
    {
        /// <summary>
        /// Splits a GM command line ("items weapon zaku 2") into words at white space; "double quotes" keep
        /// spaces inside one word ("tp \"Char Aznable\" Amuro").
        /// </summary>
        public static List<string> SplitCommand(string line)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            bool quoted = false, any = false;
            foreach (char ch in line ?? string.Empty)
            {
                if (ch == '"')
                {
                    quoted = !quoted;
                    any = true;
                }
                else if (!quoted && char.IsWhiteSpace(ch))
                {
                    if (any)
                    {
                        words.Add(word.ToString());
                        word.Clear();
                        any = false;
                    }
                }
                else
                {
                    word.Append(ch);
                    any = true;
                }
            }
            if (any)
            {
                words.Add(word.ToString());
            }
            return words;
        }

        public static unsafe Guid ToGUID(this uint id)
        {
            Random random = new Random((int)id);
            byte[] buf = new byte[8];
            random.NextBytes(buf);
            return new Guid((int)(id ^ 0x85118708), (short)random.Next(), (short)random.Next(), buf);
        }

        public static unsafe uint ToUInt(this Guid id)
        {
            fixed (byte* ptr = id.ToByteArray())
            {
                return *(uint*)ptr ^ 0x85118708;
            }
        }

        public static byte[] slot2GuidBytes(int i)
        {
            byte[] guid = Conversions.HexStr2Bytes(((uint)i).ToGUID().ToString().Replace("-", ""));

            byte[] temp = new byte[4];
            Array.Copy(guid, 0, temp, 0, 4);
            temp = temp.Reverse().ToArray();
            Array.Copy(temp, 0, guid, 0, 4);
            temp = new byte[2];
            Array.Copy(guid, 4, temp, 0, 2);
            temp = temp.Reverse().ToArray();
            Array.Copy(temp, 0, guid, 4, 2);
            temp = new byte[2];
            Array.Copy(guid, 6, temp, 0, 2);
            temp = temp.Reverse().ToArray();
            Array.Copy(temp, 0, guid, 6, 2);
            return guid;
        }

        public static string TranslateText(string input, string languagePair)
        {
            string url = String.Format("http://www.google.com/translate_t?hl=en&ie=UTF8&text={0}&langpair={1}", input, languagePair);
            System.Net.WebClient webClient = new System.Net.WebClient();
            webClient.Encoding = System.Text.Encoding.UTF8;
            string result = webClient.DownloadString(url);
            result = result.Substring(result.IndexOf("<span title=\"") + "<span title=\"".Length);
            result = result.Substring(result.IndexOf(">") + 1);
            result = result.Substring(0, result.IndexOf("</span>"));

            //System.Diagnostics.Debug.WriteLine("TranslateResult> " + result);
            return result.Trim();
        }

        public static string TranslateText(string input)
        {
            var res = TranslateText(input, "ko|ja");

            return TranslateText(res, "ja|en");
        }
    }
}
