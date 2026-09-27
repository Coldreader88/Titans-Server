//
//Bytebuffer.CS
//
//Copyright(C) 2008  Eric Dyoniziak
//
// This code is used to create a dynamic buffer for reading and writing binary
// data. It has pre-built read and write functions for making specific operations
// needed for packet creation in SWG, however it is easily expandable for
// any generic data type and for any situation where data serialization is needed.
//
// This program is free software; you can redistribute it and/or modify
// it freely. You must however leave this copyright notice in its original form.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
//
using System;
using System.IO;
using System.Text;

namespace Bytebuffer
{
    public class Bytebuffer
    {
        //==========================================================================================
        #region Private Members
        long mReadPos;
        long mWritePos;
        private MemoryStream mData;
        private BinaryReader mBinRead;
        private BinaryWriter mBinWrite;
        #endregion
        //==========================================================================================
        #region Public Class Members
        public Bytebuffer()
        {
            mData = new MemoryStream();
            mBinRead = new BinaryReader(mData);
            mBinWrite = new BinaryWriter(mData);
            mReadPos = 0;
            mWritePos = 0;
        }
        //==========================================================================================
        public int GetSize()
        {
            return (int)mData.Length;
        }
        //==========================================================================================
        public int GetBytesLeft()
        {
            return (int)(mWritePos - mReadPos);
        }
        //==========================================================================================
        public void PopEndBytes(int size)
        {
            mData.SetLength(mData.Length - size);
            mWritePos -= size;
        }
        //==========================================================================================
        public byte[] getBuffer()
        {
            return mData.ToArray();
        }
        //==========================================================================================
        public byte[] getBufferLeft()
        {
            byte[] returnArray = new byte[GetBytesLeft()];
            mData.Read(returnArray, 0, GetBytesLeft());
            mData.Position = mReadPos;
            return returnArray;
            
        }
        //==========================================================================================
        public void Clear()
        {
            mReadPos = 0;
            mWritePos = 0;
            mData.Dispose();
            mData = new MemoryStream();
            mBinRead = new BinaryReader(mData);
            mBinWrite = new BinaryWriter(mData);
        }
        #endregion
        //==========================================================================================
        #region Read Functions
        public byte readBYTE()
        {
            mData.Position = mReadPos;
            byte data = (byte)mData.ReadByte();
            mReadPos = mData.Position;
            return data;
        }
        //==========================================================================================
        public ushort readSHORT()
        {
            mData.Position = mReadPos;
            ushort data =  mBinRead.ReadUInt16();
            mReadPos = mData.Position;
            return data;
        }
        //==========================================================================================
        public uint readINT()
        {
            mData.Position = mReadPos;
            uint data = mBinRead.ReadUInt32();
            mReadPos = mData.Position;
            return data;
        }
        //==========================================================================================
        public ulong readLONG()
        {
            mData.Position = mReadPos;
            ulong data = mBinRead.ReadUInt64();
            mReadPos = mData.Position;
            return data;
        }
        //==========================================================================================
        public float readFLOAT()
        {
            mData.Position = mReadPos;
            float data = mBinRead.ReadSingle();
            mReadPos = mData.Position;
            return data;
        }
        //==========================================================================================
        public virtual string readASTRING()
        {
            mData.Position = mReadPos;
            StringBuilder retStr = new StringBuilder();
            byte strLen = (byte)mData.ReadByte();
            strLen -= 128; //the stringlengths are offset by 80
            for (byte i = 0; i < strLen; ++i)
            {
                retStr.Append((char)mData.ReadByte());
            }
            mReadPos = mData.Position;
            return retStr.ToString();
        }
        //==========================================================================================
        public virtual string readUSTRING()
        {
            mData.Position = mReadPos;
            StringBuilder retStr = new StringBuilder();
            byte strLen = (byte)mData.ReadByte();
            strLen -= 128; //the stringlengths are offset by 80
            for (byte i = 0; i < strLen; ++i)
            {
                retStr.Append((char)mBinRead.ReadUInt16());
            }
            mReadPos = mData.Position;
            return retStr.ToString();
        }
        //==========================================================================================
        public byte[] readDATA(int length)
        {
            mData.Position = mReadPos;
            byte[] returnArray = new byte[length];
            mData.Read(returnArray, 0, length);
            mReadPos = mData.Position;
            return returnArray;
        }
        #endregion
        //==========================================================================================
        #region Write Functions
        public void writeBYTE(byte data)
        {
            mData.Position = mWritePos;
            mBinWrite.Write(data);
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public void writeSHORT(ushort data)
        {
            mData.Position = mWritePos;
            mBinWrite.Write(data);
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public void writeINT(uint data)
        {
            mData.Position = mWritePos;
            mBinWrite.Write(data);
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public void writeLONG(ulong data)
        {
            mData.Position = mWritePos;
            mBinWrite.Write(data);
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public void writeFLOAT(float data)
        {
            mData.Position = mWritePos;
            mBinWrite.Write(data);
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public virtual void writeASTRING(string data)
        {
            mData.Position = mWritePos;
            byte strLen = (byte)data.Length;
            byte character;
            mData.WriteByte((byte)(strLen+128)); //string length offset used
            for (int i = 0; i < strLen; ++i)
            {
                character = (byte)data[i];
                mData.WriteByte(character);
            }
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public virtual void writeUSTRING(string data)
        {
            mData.Position = mWritePos;
            byte strLen = (byte)data.Length;
            char character;
            mData.WriteByte((byte)(strLen + 128)); //string length offset used
            for (int i = 0; i < strLen; ++i)
            {
                character = data[i];
                mBinWrite.Write((ushort)character);
            }
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public void writeDATA(byte[] data)
        {
            mData.Position = mWritePos;
            mData.Write(data, 0, (int)data.Length);
            mWritePos = mData.Position;
        }
        //==========================================================================================
        public void writeBUFFER(Bytebuffer data)
        {
            mData.Position = mWritePos;
            byte[] dataArray = data.getBuffer();
            mData.Write(dataArray, (int)mData.Length, data.GetSize());
            mWritePos = mData.Position;
        }
        #endregion
        //==========================================================================================
        #region Peek Functions
        public byte peekBYTE()
        {
            long myPos = mReadPos;
            byte data = readBYTE();
            mReadPos = myPos;
            return data;
        }
        //==========================================================================================
        public ushort peekSHORT()
        {
            long myPos = mReadPos;
            ushort data = readSHORT();
            mReadPos = myPos;
            return data;
        }
        //==========================================================================================
        public uint peekINT()
        {
            long myPos = mReadPos;
            uint data = readINT();
            mReadPos = myPos;
            return data;
        }
        //==========================================================================================
        public ulong peekLONG()
        {
            long myPos = mReadPos;
            ulong data = readLONG();
            mReadPos = myPos;
            return data;
        }
        //==========================================================================================
        public float peekFLOAT()
        {
            long myPos = mReadPos;
            float data = readFLOAT();
            mReadPos = myPos;
            return data;
        }
        #endregion
        //==========================================================================================
        #region Indexers and Overloaded Operators
        public byte this[int index]
        {
          get
          {
              long pos = mData.Position;
              mData.Seek(index, SeekOrigin.Begin);
              byte data = (byte)mData.ReadByte();
              mData.Position = pos;
              return data;
          }
          set
          {
              byte[] data = mData.GetBuffer();
              data[index] = value;
          }
        }
        #endregion
        //==========================================================================================
    }
}
