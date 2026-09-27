using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.IO;
using Common.Network.Encryption.UCGO;
using Common.Network.Encryption.UCGO.Blowfish;
using Simias.Encryption;
using SmartEngine.Core;

namespace Common.Network.Encryption
{
    public class UCEncryption : SmartEngine.Network.Encryption
    {

        private UCGO.Blowfish.Blowfish blowfish;
        private XORMask xormask;
        private Random rand;

        public short XORKey {
            get { return xormask.XORTABLE[new Random().Next(0, xormask.XORTABLE.Count)]; }
        }

        private int byteArrayToInt(byte[] buf, int ofs)
        {
            return (buf[ofs + 3] << 24) | ((buf[ofs + 2] & 0x0ff) << 16) | ((buf[ofs + 1] & 0x0ff) << 8) | (buf[ofs] & 0x0ff);
        }

        private void intToByteArray(int value, byte[] buf, int ofs)
        {
            buf[ofs+3] = (byte)((value >> 24) & 0x0ff);
            buf[ofs+2] = (byte)((value >> 16) & 0x0ff);
            buf[ofs+1] = (byte)((value >>  8) & 0x0ff);
            buf[ofs] = (byte)  value;
        }

        public UCEncryption()
        {
            blowfish = new Blowfish("chrTCPPassword");
            xormask = new XORMask();
            rand = new Random(DateTime.Now.Millisecond);
        }

        public void Encrypt(byte[] data, int length)
        {
            short randVal = (short)(rand.Next(65536) & 0x0FFFF);
            int KEY = xormask.Keygen(randVal);
            int XORSize = byteArrayToInt(data, 16);
            Logger.ShowWarning("KEY:" + KEY);
            xormask.Encrypt(data, 64 + XORSize, KEY);
            KEY = randVal & 0x0FFFF;
            intToByteArray(KEY, data, 4);
            blowfish.Encrypt(data, length);
        }

        public void Decrypt(byte[] data, int length)
        {
            blowfish.Decrypt(data, 0, length);
            short randVal = (short)(((data[5] & 0x0ff) << 8) | (data[4] & 0x0ff));
            int KEY = xormask.Keygen(randVal);
            int XORSize = byteArrayToInt(data, 16);
            XORSize ^= KEY;
            xormask.Decrypt(data, 64 + XORSize, KEY);
            KEY = (KEY >> 16) & 0x0FFFF;
            intToByteArray(KEY, data, 4);
        }

        public override SmartEngine.Network.Encryption Create()
        {
            return new UCEncryption();
        }

        public override void Encrypt(byte[] src, int offset, int len)
        {
            short randVal = (short)(rand.Next(65536) & 0x0FFFF);
            int KEY = xormask.Keygen(randVal);
            int XORSize = byteArrayToInt(src, 16);
            Logger.ShowWarning("KEY:" + KEY);
            xormask.Encrypt(src, 64 + XORSize, KEY);
            KEY = randVal & 0x0FFFF;
            intToByteArray(KEY, src, 4);
            blowfish.Encrypt(src, len);
        }

        public override void Decrypt(byte[] src, int offset, int len)
        {
            blowfish.Decrypt(src, offset, len);
            short randVal = (short)(((src[5] & 0x0ff) << 8) | (src[4] & 0x0ff));
            int KEY = xormask.Keygen(randVal);
            int XORSize = byteArrayToInt(src, 16);
            XORSize ^= KEY;
            xormask.Decrypt(src, 64 + XORSize, KEY);
            KEY = (KEY >> 16) & 0x0FFFF;
            intToByteArray(KEY, src, 4);
        }

    }
}
