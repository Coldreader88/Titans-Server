using System;
using System.Collections.Generic;
using System.Text;
using Common.Network.Encryption;
using Common.Network.Packets;
using SmartEngine.Core;
using SmartEngine.Network;

namespace Common.Network
{
    /// <summary>
    /// Sequence numbers for one connection.
    ///
    /// The official servers answered each request with the sequence number of that request: in the
    /// captured logins (UCGO Packet Logs.zip, dummylogin.txt) the status server replies to sequence 0
    /// with 0, and the login server replies to 1, 2, 3 with 1, 2, 3.
    ///
    /// The official CMS (chat) server instead numbered its own packets 1, 2, 3, ... whatever the client
    /// sent, since it also pushes packets nobody asked for (chat from other players). Servers like that
    /// turn on <see cref="Counting"/> (see <see cref="UCNetwork{T}.CountServerSequence"/>).
    /// </summary>
    public class ServerSequence
    {
        private uint sent;

        /// <summary>
        /// The sequence number of the last packet received from the client.
        /// </summary>
        public uint CLIENT { get; set; }

        /// <summary>
        /// Number outgoing packets 1, 2, 3, ... instead of repeating the client's sequence number.
        /// </summary>
        public bool Counting { get; set; }

        /// <summary>
        /// The sequence number of the last outgoing packet, or of the next one when not counting.
        /// </summary>
        public uint SERVER
        {
            get { return Counting ? sent : CLIENT; }
        }

        /// <summary>
        /// The sequence number for the next outgoing packet. Call once per packet.
        /// </summary>
        public uint Next()
        {
            if (!Counting)
            {
                return CLIENT;
            }
            return ++sent;
        }
    }

    /// <summary>
    /// Network layer for UCGO connections: splits the incoming byte stream into packets,
    /// decrypts them and dispatches them by opcode, and wraps, encrypts and sends outgoing packets.
    /// </summary>
    public class UCNetwork<T> : Network<T>
    {
        /// <summary>
        /// Largest body we accept. Packet offsets are 16 bit, so nothing larger can be parsed.
        /// </summary>
        public const int MaxBodySize = ushort.MaxValue - UCHeader.Size;

        private readonly object sendLock = new object();

        public ServerSequence Sequence { get; set; }

        /// <summary>
        /// Number this server's outgoing packets 1, 2, 3, ... per connection (see <see cref="ServerSequence"/>).
        /// Set once at startup; the CMS server turns it on.
        /// </summary>
        public static bool CountServerSequence { get; set; }

        protected UCEncryption UCCrypt
        {
            get { return (UCEncryption)Crypt; }
        }

        public override Network<T> CreateNewInstance(System.Net.Sockets.Socket sock, Dictionary<T, Packet<T>> commandTable, Session<T> client)
        {
            var instance = new UCNetwork<T>();

            instance.Sequence = new ServerSequence { Counting = CountServerSequence };

            CreateNewInstance(instance, sock, commandTable, client);

            return instance;
        }

        /// <summary>
        /// Called with everything received so far (unprocessed bytes from the last call come first).
        /// Several packets can arrive in one read and a packet can be split across reads, so this
        /// decrypts each header to learn the packet length and keeps any incomplete tail in lastContent.
        /// </summary>
        protected override void OnReceivePacket(byte[] buf)
        {
            lastContent = null;

            if (buf == null || buf.Length == 0)
            {
                return;
            }

            int offset = 0;

            try
            {
                while (!Disconnected && buf.Length - offset >= UCHeader.Size)
                {
                    var headerBytes = new byte[UCHeader.Size];
                    Array.Copy(buf, offset, headerBytes, 0, UCHeader.Size);

                    uint key = UCCrypt.DecryptHeader(headerBytes, 0);
                    var header = UCHeader.Read(headerBytes);

                    if (!header.IsValid || header.BlowfishSize > MaxBodySize)
                    {
                        Logger.ShowError(string.Format("Received a malformed packet header ({0}), disconnecting.", header));
                        Disconnect();
                        return;
                    }

                    int total = UCHeader.Size + (int)header.BlowfishSize;
                    if (buf.Length - offset < total)
                    {
                        // The rest of this packet has not arrived yet.
                        break;
                    }

                    var packet = new byte[total];
                    Array.Copy(headerBytes, packet, UCHeader.Size);
                    Array.Copy(buf, offset + UCHeader.Size, packet, UCHeader.Size, total - UCHeader.Size);
                    offset += total;

                    UCCrypt.DecryptBody(packet, 0, total, key);

                    Sequence.CLIENT = header.Sequence;

                    var body = new byte[header.XORSize];
                    Array.Copy(packet, UCHeader.Size, body, 0, body.Length);

                    var p = new UCPacket<T>(body, header)
                    {
                        ID = ToID(header.Opcode),
                        Type = PacketType.CLIENT,
                        OriginalPacket = packet,
                    };

                    ProcessPacket(p);
                }
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }

            if (!Disconnected && offset < buf.Length)
            {
                var rest = new byte[buf.Length - offset];
                Array.Copy(buf, offset, rest, 0, rest.Length);
                lastContent = rest;
            }
        }

        /// <summary>
        /// Hands a received packet to the handler registered for its opcode. The handler gets a
        /// fresh packet instance holding the body and header.
        /// </summary>
        protected override void ProcessPacket(Packet<T> p)
        {
            var packet = p as UCPacket<T>;
            var session = NetSession;

            if (packet == null || session == null)
            {
                base.ProcessPacket(p);
                return;
            }

            Packet<T> command;
            commandTable.TryGetValue(packet.ID, out command);

            if (command == null)
            {
                if (!SuppressUnknownPackets)
                {
                    Logger.ShowWarning(string.Format("Unknown packet 0x{0:X5} ({1} bytes)\r\n{2}",
                        packet.Opcode, packet.Length, packet.DumpData2()));
                }
                return;
            }

            var handler = command.New();

            var ucHandler = handler as UCPacket<T>;
            if (ucHandler != null)
            {
                ucHandler.Header = packet.Header;
                ucHandler.Type = packet.Type;
                ucHandler.OriginalPacket = packet.OriginalPacket;
                ucHandler.ID = packet.ID;
            }

            var body = packet.ToArray();
            if (body.Length > 0)
            {
                handler.PutBytes(body, 0);
            }
            handler.Position = 0;

            PrintPacketData(handler);

            if (autoLock)
                ClientManager.EnterCriticalArea();
            try
            {
                handler.OnProcess(session);
            }
            catch (Exception ex)
            {
                Logger.ShowError(ex);
            }
            finally
            {
                if (autoLock)
                    ClientManager.LeaveCriticalArea();
            }
        }

        /// <summary>
        /// Sends a packet. With <paramref name="noWarper"/> the body is sent as is, without a header
        /// and without encryption.
        /// </summary>
        public override void SendPacket(Packet<T> p, bool noWarper)
        {
            if (!noWarper)
            {
                SendPacket(p);
                return;
            }

            if (Disconnected)
            {
                return;
            }

            var raw = p.ToArray();
            SendPacketRaw(raw, 0, raw.Length);
        }

        /// <summary>
        /// Wraps the packet body in a 64 byte header, pads it to 8 bytes, encrypts it and sends it.
        /// </summary>
        public override void SendPacket(Packet<T> p)
        {
            if (Disconnected || p == null)
            {
                return;
            }

            uint opcode = Convert.ToUInt32(p.ID);
            if (opcode == 0xFFFF)
            {
                return;
            }

            var packet = p as UCPacket<T> ?? new UCPacket<T>(p.ToArray()) { ID = p.ID };
            packet.Type = PacketType.SERVER;

            if (packet.Length > MaxBodySize)
            {
                Logger.ShowError(string.Format("Packet 0x{0:X5} is too large to send ({1} bytes).", opcode, packet.Length));
                return;
            }

            lock (sendLock)
            {
                var wire = packet.ToWire(Sequence.Next());

                PrintPacketData(packet, false);

                Crypt.Encrypt(wire, 0, wire.Length);

                SendPacketRaw(wire, 0, wire.Length);
            }
        }

        private static T ToID(uint opcode)
        {
            if (typeof(T).IsEnum)
            {
                return (T)Enum.ToObject(typeof(T), opcode);
            }
            return (T)Convert.ChangeType(opcode, typeof(T));
        }

        /// <summary>
        /// Formats a byte array as a hex dump with an ASCII column.
        /// </summary>
        public static string HexDump(byte[] data, int bytesPerRow = 16)
        {
            var sb = new StringBuilder();

            if (data == null) return string.Empty;

            for (int row = 0; row < data.Length; row += bytesPerRow)
            {
                sb.AppendFormat("{0:X4}:   ", row);

                for (int i = 0; i < bytesPerRow; i++)
                {
                    int idx = row + i;
                    sb.Append(idx < data.Length ? string.Format("{0:X2} ", data[idx]) : "   ");
                }

                sb.Append("  ");
                for (int i = 0; i < bytesPerRow && row + i < data.Length; i++)
                {
                    byte b = data[row + i];
                    sb.Append(b >= 0x20 && b <= 0x7E ? (char)b : '.');
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
