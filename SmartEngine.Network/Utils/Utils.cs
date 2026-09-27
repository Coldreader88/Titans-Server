using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartEngine.Network.Utils
{
    public static class Conversion
    {
        public static string Hex(byte Number)
        {
            return Number.ToString("X");
        }

        public static string Hex(uint Number)
        {
            return Number.ToString("X");
        }
    }
    public static class Conversions
    {
        public static byte[] String2Byte(string input)
        {
            byte[] bytes = new byte[input.Length * sizeof(byte)];
            int count = 0;
            for (int i = 0; i < input.Length; i++)
            {
                bytes[count] = (byte)((input.ToArray())[i]);
                count++;
            }
            return bytes;
        }

        public static byte[] XOR(byte[] bytes)
        {
            byte[] XORKey = new byte[16] { 0xA4, 0x9F, 0xD8, 0xB3, 0xF6, 0x8E, 0x39, 0xC2, 0x2D, 0xE0, 0x61, 0x75, 0x5C, 0x4B, 0x1A, 0x07 };
            byte[] bytesXOR = new byte[bytes.Count()];
            byte count = 0;
            for (long i = 0; i < bytes.Count(); i++)
            {
                if (count == XORKey.Count())
                    count = 0;
                bytesXOR[i] = (byte)(bytes[i] ^ XORKey[count]);
                count++;
            }
            return bytesXOR;
        }
        
        public static byte ToByte(string Value)
        {
            if (Value == null)
            {
                return 0;
            }

            long num2;
            num2 = Convert.ToInt64(Value, 0x10);
            return (byte)num2;

        }

        public static int ToInteger(string Value)
        {
            if (Value == null)
            {
                return 0;
            }
            long num2;
            num2 = Convert.ToInt64(Value, 0x10);
            return (int)num2;



        }
        public static string bytes2HexString(byte[] b)
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
        public static string bytes2HexString2(byte[] b)
        {
            string tmp = "1  2  3  4  5  6  7  8  9  10 11 12 13 14 15 16\n\n";
            int i;
            for (i = 0; i < b.Length; i++)
            {
                string tmp2 = b[i].ToString("X2");
                tmp = tmp + tmp2;
            }
            return tmp;
        }
        public static string uint2HexString(uint[] b)
        {
            string tmp = "";
            int i;
            if (b == null) return "";
            for (i = 0; i < b.Length; i++)
            {
                string tmp2 = Conversion.Hex(b[i]);
                if (tmp2.Length != 8)
                {
                    for (int j = 0; j < 8 - tmp2.Length; j++)
                    {
                        tmp2 = "0" + tmp2;
                    }
                }
                tmp = tmp + tmp2;
            }
            return tmp;
        }

        public static string ushort2HexString(ushort[] b)
        {
            string tmp = "";
            int i;
            if (b == null) return "";
            for (i = 0; i < b.Length; i++)
            {
                string tmp2 = b[i].ToString("X4");
                tmp = tmp + tmp2;
            }
            return tmp;
        }


        public static byte[] HexStr2Bytes(string s)
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

        public static uint[] HexStr2uint(string s)
        {
            uint[] b = new uint[s.Length / 8];
            int i;
            for (i = 0; i < s.Length / 8; i++)
            {
                //b[i] = (uint)Conversions.ToInteger("&H" + s.Substring(i * 8, 8));
                b[i] = (uint)Conversions.ToInteger(s.Substring(i * 8, 8));
            }
            return b;
        }
    }
}
