using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartEngine.Network;
using Common.IO;
using SmartEngine.Network.IO;
using SmartEngine.Network.IO;

namespace Common.Network.Packets
{
    public enum PacketType
    {
        UNKNOWN,
        SERVER,
        CLIENT,
    }

    public class UCPacket<T> : Packet<T>
    {

        public UCHeader Header { get; set; }

        public PacketType Type { get; set; }

        public byte[] OriginalPacket { get; set; }

        /// <summary>
        /// Opcode
        /// </summary>
        public override T ID { get; set; }

        public UCPacket() : base()
        {
            this.Type = PacketType.UNKNOWN;
            this.Header = new UCHeader();
        }

        public UCPacket(byte[] buffer, UCHeader header = null)
            : base()
        {
            this.Type = PacketType.UNKNOWN;

            this.Header = header;

        }

        public override string GetString(ushort index)
        {
            var strlen = this.GetByte(index) - 0x80;

            var res = Encoding.Unicode.GetString(this.GetBytes((ushort)strlen));

            return res;
        }

        public override void PutString(string s, ushort index)
        {
            this.PutByte((byte)(0x80 + s.Length));
            this.PutBytes(Encoding.Unicode.GetBytes(s));
        }

        public string GetUCString()
        {
            var strlen = this.GetByte() -0x80;

            var res = Encoding.Unicode.GetString(this.GetBytes((ushort)(strlen * 2)));

            return res;
        }

        public void PutUCString(string str)
        {
            this.PutByte((byte)(0x80 + str.Length));
            this.PutBytes(Encoding.Unicode.GetBytes(str));
        }

        public string DumpData2()
        {
            string tmp2 = "";

            var buff = new ByteBuffer();

            buff.WriteBytes(this.ToArray());

            for (int i = 0; i < buff.Buffer.Length; i++)
            {
                tmp2 += (String.Format("{0:X2} ", buff.Buffer[i]));
                if (((i + 1) % 16 == 0) && (i != 0))
                {
                    tmp2 += "\r\n";
                }
            }
            return tmp2;
        }

    }

    public class UCLoginPacket : UCPacket<Network.Packets.ISOpcode>
    {

        public UCLoginPacket() : base()
        {
            
        }

        public UCLoginPacket(byte[] buffer)
            : base(buffer)
        {

        }
    }
}
