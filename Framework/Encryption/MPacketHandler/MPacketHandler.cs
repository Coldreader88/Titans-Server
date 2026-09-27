using System;
using System.Runtime.InteropServices;
using System.Text;


namespace Common.Network.Encryption
{
    public class MPacketHandler
    {
        private static PacketHandler.PacketHandler PKH = null;
        //==========================================================================================
        public MPacketHandler()
        {
            PKH = new PacketHandler.PacketHandler();
        }

        //==========================================================================================
        public MPacketHandler(string Password = "chrTCPPassword")
        {
            unsafe
            {
                fixed (char* strPtr = Password.ToCharArray())
                    PKH = new PacketHandler.PacketHandler(0x1000, 0x800, (byte*)strPtr);
            }
        }

        //==========================================================================================
        public MPacketHandler(int BufferSize, int PacketSize, string Password)
        {
            unsafe
            {
                fixed(char* strPtr = Password.ToCharArray())
                PKH = new PacketHandler.PacketHandler((uint)BufferSize, (uint)PacketSize, (byte*)strPtr);
            }
        }
        //==========================================================================================
        public bool IsRecvEmpty()
        {
            return PKH.IsRecvEmpty();
        }
        //==========================================================================================
        public bool IsSendEmpty()
        {
            return PKH.IsSendEmpty();
        }
        //==========================================================================================
        public int AddRecvBuffer(byte[] data)
        {
            int error = 0;
            unsafe
            {
                fixed(byte* dataPtr = data)
                error =PKH.AddRecvBuffer(dataPtr, (uint)data.Length);
            }
            return error;
        }
        //==========================================================================================
        public byte[] GetSendBuffer()
        {
            unsafe
            {
                byte* data;
                int length;
                int error;
                error = PKH.GetSendBuffer((byte**)&data, (uint*)&length);
                byte[] retData = new byte[length];
                if(length > 0)
                    Marshal.Copy(new IntPtr(data), retData, 0, length);
                return retData;
            }
        }
        //==========================================================================================
        public int SendPacket(byte[] data)
        {
            int error = 0;
            unsafe
            {
                fixed (byte* dataPtr = data)
                    error = PKH.SendPacket(dataPtr, (uint)data.Length);
            }
            return error;
        }
        //==========================================================================================
        public byte[] RecvPacket()
        {
            unsafe
            {
                byte* data;
                int length;
                int error;
                error = PKH.RecvPacket((byte**)&data, (uint*)&length);
                byte[] retData = new byte[length];
                if (length > 0)
                    Marshal.Copy(new IntPtr(data), retData, 0, length);
                return retData;
            }
        }
        //==========================================================================================
        public void SetPassword(string Password)
        {
            unsafe
            {
                fixed (char* strPtr = Password.ToCharArray())
                    PKH.SetPassword((byte*)strPtr);
            }
        }
        //==========================================================================================
    }
}
