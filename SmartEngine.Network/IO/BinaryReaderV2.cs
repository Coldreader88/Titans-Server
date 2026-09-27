using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using System.IO;

using SmartEngine.Core;

namespace SmartEngine.Network.IO
{
    public class BinaryReaderV2 : BinaryReader
    {

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

        /*
        *Reads a Double Null-Terminated Unicode string from the stream  
        */
        public string ReadUString()
        {
            long ori = this.BaseStream.Position;
            long offset = ori;
            string res;
            this.BaseStream.Position = ori;
            short c;
            int size = 0;
            do
            {
                c = this.ReadInt16();
                size++;
            } while (c != 0);

            this.BaseStream.Position = offset;
            res = Encoding.Unicode.GetString(this.ReadBytes(size * 2)).Trim('\0');
            return res;
        }

        /*
         *Reads a Double Null-Terminated Unicode string from the stream
         *at the specified offset.
         */
        public string ReadUString(int offset)
        {
            long ori = this.BaseStream.Position;
            string res;
            this.BaseStream.Position = offset;
            short c;
            int size = 0;
            do
            {
                c = this.ReadInt16();
                size++;
            } while (c != 0);

            this.BaseStream.Position = offset;
            res = Encoding.Unicode.GetString(this.ReadBytes(size * 2)).Trim('\0');
            this.BaseStream.Position = ori;
            return res;
        }

        /*
         * Gets the Stream's Data as a Byte Array
         */
        public byte[] GetData()
        {
            return this.ReadBytes((int)this.baseStream.Length);
        }

        /*
         *Cleans up the Underlying Stream and the BinaryReaderV2 
         */
        public void SuperClose()
        {
            try
            {
                this.Close();
                baseStream.Close();
            }
            catch (System.Exception ex)
            {
                Logger.ShowError(ex.Message);
            }
        }
    }
}
