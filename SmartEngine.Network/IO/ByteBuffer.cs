using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using System.IO;

using SmartEngine.Core;

namespace SmartEngine.Network.IO
{
    public class ByteBuffer : BinaryWriter
    {
        /*
        private Stream baseStream;

        public long Position
        {
            get { return this.BaseStream.Position; }
            set { this.BaseStream.Position = value; }
        }

        public BinaryReaderV2(Stream input)
             : base(input)
        {
            baseStream = this.BaseStream;
        }

        public BinaryReaderV2(byte[] data)
            : base(new MemoryStream(data))
        {
            baseStream = this.BaseStream;
        }
        */

        private MemoryStream _base;

        public byte[] Buffer
        {
            get
            {
                return this.ToArray();
            }
        }

        public long Offset
        {
            get { return this.BaseStream.Position; }
            set { this.BaseStream.Position = value; }
        }

        public int Capacity
        {
            get
            {
                return ((MemoryStream)this.BaseStream).Capacity;
            }

            set
            {
                ((MemoryStream)this.BaseStream).Capacity = value;
            }
        }

        public ByteBuffer(int Capacity)
            : base(new MemoryStream(Capacity))
        {
            _base = (MemoryStream)this.BaseStream;

            
        }

        public ByteBuffer() 
             : base(new MemoryStream())
        {
           _base = (MemoryStream)this.BaseStream;
        }

        public ByteBuffer(Stream stream)
            : base(new MemoryStream())
        {
            _base = (MemoryStream)this.BaseStream;


            this.Write(new BinaryReaderV2(stream).GetData());
        }

        public ByteBuffer(Packet<uint> packet)
            : base(new MemoryStream())
        {
            _base = (MemoryStream)this.BaseStream;


            packet.CopyTo(this.BaseStream);
        }

        public ByteBuffer(ByteBuffer buffer)
            : base(new MemoryStream())
        {
            _base = (MemoryStream)this.BaseStream;


            buffer.BaseStream.CopyTo(this.BaseStream);
        }

        public long Seek(long offset, SeekOrigin loc)
        {
            return _base.Seek(offset, loc);
        }

        public byte[] ToArray()
        {
            this.Flush();
            return _base.ToArray();
        }

        public void WriteByte(int value = 0)
        {
            this.Write((byte)value);
        }

        public void WriteByte(object value)
        {
            this.Write(Convert.ToByte(value));
        }

        public void PutByte(int value = 0)
        {
            this.WriteByte(value);
        }

        public void PutByte(object value)
        {
            this.WriteByte(value);
        }

        public void WriteBytes(byte[] data)
        {
            this.Write(data);
        }

        public void PutBytes(byte[] data)
        {
            this.Write(data);
        }

        public void WriteShort(int value = 0)
        {
            this.Write(short.Parse(value.ToString()));
        }

        public void PutShort(int value = 0)
        {
            this.WriteShort(value);
        }

        public void WriteUShort(int value = 0)
        {
            this.Write((ushort)value);
        }

        public void PutUShort(int value = 0)
        {
            this.WriteUShort(value);
        }

        public void WriteInt(int value = 0)
        {
            this.Write(value);
        }

        public void PutInt(int value = 0)
        {
            this.WriteInt(value);
        }

        public void WriteUInt(uint value = 0)
        {
            this.Write(value);
        }

        public void PutUInt(uint value = 0)
        {
            this.WriteUInt(value);
        }

        public void WriteLong(long value = 0)
        {
            this.Write(value);
        }

        public void PutLong(long value = 0)
        {
            this.WriteLong(value);
        }

        public void WriteULong(ulong value = 0)
        {
            this.Write(value);
        }

        public void PutULong(ulong value = 0)
        {
            this.WriteULong(value);
        }

        public void Padd(int value = 0, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                this.WriteByte(value);
            }
        }

        public void Padd(int count = 1)
        {
            this.Padd(0, count);
        }

        public void WriteString(string str)
        {
            byte[] strData = Global.Encoding.GetBytes(str);

            this.WriteUShort(strData.Length);
            this.WriteBytes(strData);
        }

        public void Write(Stream stream)
        {
            this.Write(new ByteBuffer(stream).Buffer);
        }

        public void Write(ByteBuffer stream)
        {
            this.Write(stream.Buffer);
        }

        public void Write(Writable writable)
        {
            this.Write(writable.Write());
        }


        public string ReadString()
        {
            var br = new BinaryReaderV2(this.Buffer);

            ushort len = br.ReadUInt16();

            var strBytes = br.ReadBytes(len);

            return Encoding.Unicode.GetString(strBytes);
        }

        public byte ReadByte()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadByte();
        }

        public short ReadShort()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadInt16();
        }

        public ushort ReadUShort()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadUInt16();
        }

        public int ReadInt()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadInt32();
        }

        public uint ReadUInt()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadUInt32();
        }

        public long ReadLong()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadInt64();
        }

        public ulong ReadULong()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadUInt64();
        }

        public float ReadFloat()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadSingle();
        }

        public double ReadDouble()
        {
            var br = new BinaryReaderV2(this.Buffer);

            return br.ReadDouble();
        }

        public string DumpData()
        {
            string tmp2 = "";
            for (int i = 0; i < this.Buffer.Length; i++)
            {
                tmp2 += (String.Format("{0:X2} ", this.ToArray()[i]));
                if (((i + 1) % 16 == 0) && (i != 0))
                {
                    tmp2 += "\r\n";
                }
            }
            return tmp2;
        }

        public void DisplayData()
        {
            try
            {
                Console.WriteLine();
                string dataHeader = "";
                dataHeader += "=================================================\r\n";
                dataHeader += " 1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 |\r\n";
                dataHeader += "=================================================\r\n";
                Logger.ShowInfo(string.Format("Packet Data:\r\n\n" + dataHeader + "{0}\n", this.DumpData()));
            }
            catch (System.Exception ex)
            {
                Logger.ShowError(ex);
            }
        }

    }
}
