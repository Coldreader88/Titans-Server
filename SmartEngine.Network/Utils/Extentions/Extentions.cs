using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartEngine.Network.Utils.Extentions
{
    public static class Extentions
    {
        public static string[] Tokenize(this String str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return new string[] {""};
            }
            else
            {
                return str.Split(' ');
            }
            //return str.Split(new char[] { ' ', '.', '?' },
            //    StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public static uint ToUint(this object obj)
        {
            if (obj is string)
            {
                return uint.Parse(obj.ToString());
            }
            else
            {
                return System.Convert.ToUInt32(obj); ;
            }
        }

        public static int ToInt(this object obj)
        {
            if (obj is string)
            {
                return int.Parse(obj.ToString());
            }
            else
            {
                return System.Convert.ToInt32(obj); ;
            }
        }

        public static string ToUString(this Packet<int> obj, bool sizedString = true)
        {
            int size = 0;

            if (sizedString)
            {
                size = obj.GetUShort()*2;
            }
            else
            {
                size = Convert.ToInt32(obj.Length - obj.Position);
            }

            var msgBytes = obj.GetBytes((ushort) size);

            return Encoding.Unicode.GetString(msgBytes);
        }

        public static string ToUString<T>(this Packet<T> obj, bool sizedString = true)
        {
            int size = 0;

            if (sizedString)
            {
                size = obj.GetUShort() * 2;
            }
            else
            {
                size = Convert.ToInt32(obj.Length - obj.Position);
            }

            var msgBytes = obj.GetBytes((ushort)size);

            return Encoding.Unicode.GetString(msgBytes);
        }

        public static DateTime ToDateTime<T>(this Packet<T> obj, bool bBigEndian = false)
        {
            return !bBigEndian ? new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((double)(Convert.ToUInt64(obj.GetUInt()))) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((double)(Global.LittleToBigEndian(Convert.ToUInt64(obj.GetUInt()))));
        }

        public static DateTime ToDateTime(this uint obj, bool bBigEndian = false, bool bLocalTime = true)
        {
            //var timeStamp = !bBigEndian ? new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((double) (obj)) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((double)(Global.LittleToBigEndian(obj)));
            var timeStamp = !bBigEndian ? Global.FromUnixTime(obj) : Global.FromUnixTime(Global.LittleToBigEndian(obj));
            return !bLocalTime ? timeStamp : timeStamp.ToLocalTime();
        }

        public static DateTime ToDateTime(this ulong obj)
        {
            var bBigEndian = false;

            var timeStamp = !bBigEndian ? new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((System.Convert.ToDouble(obj))) : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds((System.Convert.ToDouble(Global.LittleToBigEndian(obj))));
            return timeStamp;
        }

        public static short ToShort(this object obj)
        {
            if (obj is string)
            {
                return short.Parse(obj.ToString());
            }
            else
            {
                return System.Convert.ToInt16(obj);
            }
        }

        public static ushort ToUShort(this object obj)
        {
            if (obj is string)
            {
                return ushort.Parse(obj.ToString());
            }
            else
            {
                return System.Convert.ToUInt16(obj);
            }
        }

        public static byte[] GetBytes(this string obj)
        {
            if (obj is string)
            {
                return Encoding.ASCII.GetBytes(obj);
            }
            else
            {
                throw new NotSupportedException("only string is supported for this method.");
            }
        }

        public static Regex ToRegex(this string obj, string expression)
        {
            return new Regex(expression);
        }

        /*
        public static bool ToBool(this object obj)
        {
            if (obj is string)
            {
                return bool.Parse(ulong.Parse(obj.ToString()).ToString());
            }
            else
            {
                return System.Convert.ToBoolean(ulong.Parse(obj.ToString()));
            }
        }
        */

        public static bool ToBool(this string obj)
        {
            if (obj != null)
            {
                return System.Convert.ToBoolean(ulong.Parse(obj));
            }
            else
            {
                return false;
            }
        }

        public static T To<T>(this object obj) where T: new()
        {
            return (T) obj;
        }

        public static List<byte> ToBytes(this string obj, string splitter = ",")
        {
            var splitArgs = splitter.Split(' ');

            var splitChars = new char[splitArgs.Count()];

            for (int i = 0; i < splitArgs.Count(); i++)
            {
                splitChars[i] = System.Convert.ToChar(splitArgs[i]);
            }

            var array = obj.Split(splitChars);

            var t = new List<byte>();

            foreach (var ele in array)
            {
                t.Add(Convert.ToByte(ele));
            }

            return t;
        }

        public static List<ushort> ToUShortList(this string obj, string splitter = ",")
        {
            var splitArgs = splitter.Split(' ');

            var splitChars = new char[splitArgs.Count()];

            for (int i = 0; i < splitArgs.Count(); i++)
            {
                splitChars[i] = System.Convert.ToChar(splitArgs[i]);
            }

            var array = obj.Split(splitChars);

            var t = new List<ushort>();

            foreach (var ele in array)
            {
                t.Add(Convert.ToUInt16(ele));
            }

            return t;
        }

        public static List<uint> ToUintList(this string obj, string splitter = ",")
        {
            var splitArgs = splitter.Split(' ');

            var splitChars = new char[splitArgs.Count()];

            for (int i = 0; i < splitArgs.Count(); i++)
            {
                splitChars[i] = System.Convert.ToChar(splitArgs[i]);
            }

            var array = obj.Split(splitChars);

            var t = new List<uint>();

            foreach(var ele in array)
            {
                t.Add(Convert.ToUInt32(ele));
            }

            return t;
        }

        public static List<ulong> ToULongList(this string obj, string splitter = ",")
        {
            var splitArgs = splitter.Split(' ');

            var splitChars = new char[splitArgs.Count()];

            for (int i = 0; i < splitArgs.Count(); i++)
            {
                splitChars[i] = System.Convert.ToChar(splitArgs[i]);
            }

            var array = obj.Split(splitChars);

            var t = new List<ulong>();

            foreach(var ele in array)
            {
                t.Add(Convert.ToUInt64(ele));
            }

            return t;
        }

        public static string ToHexString(this byte[] b)
        {
            string tmp = "";
            int i;
            for (i = 0; i < b.Length; i++)
            {
                string tmp2 = b[i].ToString("X2");
                tmp = tmp + tmp2;
            }
            return tmp;
        }

        public static byte[] HexStringToBytes(this string s)
        {
            byte[] b = new byte[s.Length / 2];
            int i;
            for (i = 0; i < s.Length / 2; i++)
            {
                //b[i] = Conversions.ToByte( "&H" + s.Substring( i * 2, 2 ) );
                b[i] = Conversions.ToByte(s.Substring(i * 2, 2));
            }
            return b;
        }
    }
}
