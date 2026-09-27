using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using Common.Network.Encryption.UCGO;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;
using Common.IO;
using Common.Network.Encryption;
using SmartEngine.Network.IO;
using SmartEngine.Network.IO;

namespace Common.Network
{
    public class UCHeader : Writable
    {
        public uint XORKey { get; set; }

        public uint Sequence { get; set; }

        public uint Opcode { get; set; }

        public uint XORSize { get; set; }

        public uint BlowfishSize
        {
            get
            {
                var finalsize = 8 - XORSize % 8;

                
                if (finalsize == 8)
                {
                    finalsize = XORSize;
                }
                else if (finalsize != 8)
                {
                    finalsize += XORSize;
                }
                

                return finalsize;
            }
        }

        public UCHeader()
        {

        }

        public UCHeader(uint packetLen, uint xorKey, uint opcode, uint sequence)
        {
            this.XORKey = xorKey;
            this.Sequence = sequence;
            this.XORSize = packetLen;
            this.Opcode = opcode;
        }

        public byte[] Create(byte[] packet)
        {

            try
            {

                var buffer = new SmartEngine.Network.IO.ByteBuffer();

                buffer.WriteUInt(0x64616568);
                buffer.WriteUShort((ushort)XORKey);
                buffer.WriteUShort(0);
                buffer.WriteUInt();
                buffer.WriteUInt(Sequence);
                buffer.WriteUInt(XORSize);

                buffer.WriteUInt(BlowfishSize);

                buffer.WriteUInt(Opcode);
                buffer.Padd(new Random().Next(53, 255), 32);
                buffer.WriteUInt(0x6C696174);

                return buffer.Buffer;

            }
            catch (Exception ex)
            {

                Logger.ShowError(ex);

                return new byte[64];
            }
        }

        public byte[] Create()
        {

            try
            {

                var buffer = new SmartEngine.Network.IO.ByteBuffer();

                buffer.WriteUInt(0x64616568);
                buffer.WriteUInt(XORKey);
                buffer.WriteUInt();
                buffer.WriteUInt(Sequence);
                buffer.WriteUInt(XORSize);

                buffer.WriteUInt(BlowfishSize);

                buffer.WriteUInt(Opcode);
                buffer.Padd(new Random().Next(53, 255), 32);
                buffer.WriteUInt(0x6C696174);

                return buffer.Buffer;

            }
            catch (Exception ex)
            {

                Logger.ShowError(ex);

                return new byte[64];
            }
        }

        public static UCHeader Read(byte[] packet)
        {
            try
            {

                var header = new UCHeader();

                var buffer = new Bytebuffer();

                buffer.writeDATA(packet);

                buffer.readINT();

                header.XORKey = buffer.readINT();

                buffer.readINT();

                header.Sequence = buffer.readINT();
                header.XORSize = buffer.readINT();

                buffer.readINT();

                header.Opcode = buffer.readINT();


                return header;
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);

                return null;
            }
        }

        public ByteBuffer Write()
        {
            try
            {

                var buffer = new SmartEngine.Network.IO.ByteBuffer();

                buffer.WriteUInt(0x68656164);
                buffer.WriteUInt(XORKey);
                buffer.WriteUInt();
                buffer.WriteUInt(Sequence);
                buffer.WriteUInt(XORSize);

                buffer.WriteUInt(BlowfishSize);

                buffer.WriteUInt(Opcode);
                buffer.Padd(new Random().Next(53, 255), 32);
                buffer.WriteUInt(0x6C696174);

                return buffer;

            }
            catch (Exception ex)
            {

                Logger.ShowError(ex);

                return new ByteBuffer();
            }
        }
    }

    public class ServerSequence
    {
        private uint serverSequence;
        private uint clientSequence;

        public uint CLIENT 
        { 
            get { return clientSequence; } 
            set { clientSequence = value; } 
        }

        public uint SERVER
        {
            /*get { return serverSequence++; }*/
            get
            {
                return clientSequence;
            }
        }

    }

    public class UCNetwork<T> : Network<T>
    {

        //protected UCEncryption Crypto { get; set; }

        public ServerSequence Sequence { get; set; }

        public override Network<T> CreateNewInstance(System.Net.Sockets.Socket sock, Dictionary<T, Packet<T>> commandTable, Session<T> client)
        {
            var instance = new UCNetwork<T>();

            instance.Sequence = new ServerSequence();

            instance.Sequence.CLIENT = 0;

            CreateNewInstance(instance, sock, commandTable, client);

            return instance;
        }

        protected override void OnReceivePacket(byte[] buf)
        {
            if (buf != null && buf.Any())
            {

                if (buf.Length > 64 && lastContent != buf)
                {
                    try
                    {

                        var buffer = buf.ToArray();

                        this.Crypt.Decrypt(buffer, 0, buffer.Length);

                        if (buffer != null && buffer.Any())
                        {

                            var _header = new byte[64];
                            var payload = new byte[buffer.Length - 64];

                            Array.Copy(buffer, _header, 64);
                            Array.Copy(buffer, 64, payload, 0, payload.Length);

                            var header = UCHeader.Read(_header);

                            this.Sequence.CLIENT = header.Sequence;

                            var p = new UCPacket<T>(buffer, header);

                            p.PutBytes(payload);

                            p.ID = (T)(object)(int)p.Header.Opcode;

                            p.OriginalPacket = buffer;

                            PrintPacketData(new Packet<T>(buffer), true, true);

                            ProcessPacket(p);

                            

                            lastContent = buf;

                        }
                        else
                        {
                            Logger.ShowError("OnRecieve: Couldn't decrypt packet.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.ShowError(ex);
                    }

                }
                else
                {
                    Logger.ShowError("OnRecieve: buffer {0}", buf != null ? "size is (" + buf.Length + ") must be bigger than 64." : "is null");
                }

            }
            else
            {

                Logger.ShowWarning("OnRecieve: buffer is null or empty.");
            }
        }


        public override void SendPacket(Packet<T> p, bool noWarper)
        {
            throw new NotImplementedException();
        }

        /*
        public override void SendPacket(Packet<T> p)
        {
            Logger.ShowWarning("SendPacket");

            if ((int)(object)p.ID == 0xFFFF || Disconnected)
            {
                return;
            }

            PrintPacketData(p, false);

            var packet = new UCPacket<T>();

            var xorKey = (uint) ((UCEncryption) Crypt).XORKey;

            packet.Write(new UCHeader((uint)p.Length, xorKey, (uint)(int)(object)p.ID,  this.Sequence.SERVER));
            packet.ID = p.ID;
            packet.PutBytes(p.Buffer.ToArray());

            PrintPacketData(SuppressPacketHeaderPrintOut ? p : packet, false);

            if (packet.Header.BlowfishSize > packet.Header.XORSize)
            {
                packet.PutBytes((ushort)(packet.Header.BlowfishSize - packet.Header.XORSize));
            }

            PrintPacketData(SuppressPacketHeaderPrintOut ? p : packet, false);

            var buf = packet.Buffer.ToArray();

            Crypt.Encrypt(buf, 0, buf.Length);

            HexDump(buf);

            SendPacketRaw(buf, 0, buf.Length);
        }*/

        static void HexDump(byte[] data, int bytesPerRow = 16)
        {
            StringBuilder sb = new StringBuilder();

            if (data == null) return;

            for (int row = 0; row < data.Length; row += bytesPerRow)
            {
                // offset column
                sb.Append($"{row:X4}:   ");

                // hex column (pad short final rows so the ascii gutter stays aligned)
                for (int i = 0; i < bytesPerRow; i++)
                {
                    int idx = row + i;
                    sb.Append(idx < data.Length ? $"{data[idx]:X2} " : "   ");
                }

                // ascii gutter
                sb.Append("  ");
                for (int i = 0; i < bytesPerRow && row + i < data.Length; i++)
                {
                    byte b = data[row + i];
                    sb.Append(b >= 0x20 && b <= 0x7E ? (char)b : '.');
                }

                sb.AppendLine();
            }

            Logger.ShowWarning(sb.ToString());
        }

        public override void SendPacket(Packet<T> p)
        {
            Logger.ShowWarning("SendPacket");

            if ((int)(object)p.ID == 0xFFFF || Disconnected)
            {
                return;
            }

            //PrintPacketData(p, false);
            //HexDump(p.ToArray());

            var packet = new UCPacket<T>();

            packet.ID = p.ID;

            packet.SetLength(64 + p.Length);

            var xorKey = (uint)((UCEncryption)Crypt).XORKey;

            var header = new UCHeader((uint)p.Length, xorKey, (uint)(int)(object)p.ID, this.Sequence.SERVER);

            packet.PutBytes(header.Create(), 0);

            var dataBytes = new byte[p.Length];

            Array.Copy(p.Buffer, dataBytes, header.XORSize);

            packet.PutBytes(dataBytes);

            //packet.Write(new UCHeader((uint)p.Length, xorKey, (uint)(int)(object)p.ID, this.Sequence.SERVER));
            //packet.ID = p.ID;


            //HexDump(packet.ToArray());

            //packet.SetLength(64 + header.XORSize); ;

            PrintPacketData(SuppressPacketHeaderPrintOut ? p : packet, false);

            /*if (packet.Header.BlowfishSize > packet.Header.XORSize)
            {
                packet.PutBytes((ushort)(packet.Header.BlowfishSize - packet.Header.XORSize));
            }*/

            //PrintPacketData(SuppressPacketHeaderPrintOut ? p : packet, false);
            //HexDump(packet.ToArray());

            var buf = new byte[64 + header.BlowfishSize];
            
            Array.Copy(packet.Buffer, buf, buf.Length);

            Crypt.Encrypt(buf, 0, buf.Length);


            SendPacketRaw(buf, 0, buf.Length);
        }
    }
}

