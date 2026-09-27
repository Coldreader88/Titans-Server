using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Common.Network;
using Common.Network.Encryption;
using Common.Network.Packets;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using SmartEngine.Core;

namespace UCPacketViewer.PacketHandler
{
    public enum ServerType
    {
        INFO,
        LOGIN,
        CMS,
        GAME,
    }

    public class UCPacketParser
    {

        public class Server
        {
            public string Filter { get; set; }
            public ushort Port { get; set; }
            public SortedList<uint, UCViewerPacket> Packets { get; set; }
            protected UCEncryption Encryption { get; set; }
            public ServerType Type { get; set; }

            public bool Any { get { return Packets.Values.Any(); } }

            public Server(ServerType server, ushort port, string filter = "tcp port {0}")
            {
                this.Port = port;
                this.Filter = string.Format(filter, Port);
                this.Packets = new SortedList<uint, UCViewerPacket>();
                this.Type = server;
                this.Encryption = new UCEncryption();
            }

            protected byte[] Decrypt(Packet packet)
            {
                try
                {
                    byte[] decr_data = new byte[packet.Buffer.Length - 54];

                    Array.Copy(packet.Buffer, 54, decr_data, 0, decr_data.Length);

                    if (decr_data.Length >= 64)
                    {
                        Encryption.Decrypt(decr_data, 0, decr_data.Length);

                    }
                    else if (decr_data == null)
                    {
                        throw new Exception("Unable to decrypt packet.");
                    }else if (decr_data.Length < 64)
                    {
                        throw new Exception("Decrypted packet length is smaller than required header size.");
                    }

                    return decr_data;
                }
                catch (Exception ex)
                {
                    Msg(ex.Message);
                    return new byte[0];
                }
            }

            public void AddPacket(Packet packet, bool bDecrypt = true)
            {
                try
                {

                    if (packet != null)
                    {
                        Msg(string.Format("Source: {0}, Dest: {1}", packet.IpV4.Tcp.SourcePort, packet.IpV4.Tcp.DestinationPort));
                        if (true)
                        {
                            var data = Decrypt(packet);

                            var buf = data.ToArray();

                            if (buf != null && buf.Length >= 64)
                            {

                                var header = new byte[64];
                                var payload = new byte[buf.Length - 64];

                                Array.Copy(buf, header, 64);
                                Array.Copy(buf, 64, payload, 0, payload.Length);

                                var headerData = UCHeader.Read(header);

                                var ucPacket = new UCViewerPacket(packet, this.Type);

                                ucPacket.ID = headerData.Opcode;//(uint) this.Packets.Count() + 1;//headerData.Opcode;

                                ucPacket.Write(data, 0, 64 + (int)headerData.XORSize);

                                ucPacket.Header = headerData;

                                this.Packets.Add((uint)this.Packets.Count() + 1, ucPacket);
                            }
                            else
                            {
                                Msg("Packet is null or length isnt the required minimum.");
                            }
                        }

                    }
                    else
                    {
                        throw new Exception("Packet is null");
                    }

                }
                catch (Exception ex)
                {
                    Util.ShowError(ex);
                }
            }

        }

        public class ServerList
        {
            public Server Info, Login, Cms, Game;

            public ServerList(ushort infoPort = 24012, ushort loginPort = 24018, ushort cmsPort = 24016, ushort gamePort = 24010)
            {
                Info = new Server(ServerType.INFO, infoPort);
                Login = new Server(ServerType.LOGIN, loginPort);
                Cms = new Server(ServerType.CMS, cmsPort);
                Game = new Server(ServerType.GAME, gamePort);
            }

            public bool Any()
            {
                if (Info.Any || Login.Any || Cms.Any || Game.Any)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

        }

        public ServerList Servers { get; set; }
        private OfflinePacketDevice device = null;


        public UCPacketParser()
        {

        }

        public void HandlePackets(string path = "")
        {
            try
            {

                if (path.Any())
                {
                    this.Servers = new ServerList();
                    this.device = new OfflinePacketDevice(path);

                    using (
                    PacketCommunicator communicator = device.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
                    {
                        communicator.SetFilter(Servers.Info.Filter);//("tcp port 6600");
                        var res = communicator.ReceivePackets(0, InfoPacketCallBack);

                        Msg("Received Info Server Packets.\n");
                    }

                    using (
                    PacketCommunicator communicator = device.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
                    {
                        communicator.SetFilter(Servers.Login.Filter);//("tcp port 6600");
                        var res = communicator.ReceivePackets(0, LoginPacketCallBack);

                        Msg("Received Login Server Packets.\n");
                    }

                    using (
                    PacketCommunicator communicator = device.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
                    {
                        communicator.SetFilter(Servers.Cms.Filter);//("tcp port 6600");
                        var res = communicator.ReceivePackets(0, CmsPacketCallBack);

                        Msg("Received Cms Server Packets.\n");
                    }
                    
                    /*
                    using (
                    PacketCommunicator communicator = device.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
                    {
                        communicator.SetFilter(Servers.Game.Filter);//("tcp port 6600");
                        var res = communicator.ReceivePackets(0, GamePacketCallBack);

                        Msg("Received Game Server Packets.\n");
                    }
                    */

                }
                else
                {
                    throw new Exception("no file specified.");
                }
            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void InfoPacketCallBack(Packet packet)
        {
            try
            {

                if (packet.Any() && packet.Length > 54)
                {
                    this.Servers.Info.AddPacket(packet);
                }


            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
                //System.Windows.Forms.MessageBox.Show("What> " + ex.Message + "\nWhere> " + ex.StackTrace,
                //                                     "PacketParserV2 Callback Error");
            }
        }

        private void LoginPacketCallBack(Packet packet)
        {
            try
            {

                if (packet.Any()&& packet.Length > 54)
                {
                    this.Servers.Login.AddPacket(packet);
                }


            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void CmsPacketCallBack(Packet packet)
        {
            try
            {

                if (packet.Any() && packet.Length > 54)
                {
                    this.Servers.Cms.AddPacket(packet);
                }


            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void GamePacketCallBack(Packet packet)
        {
            try
            {

                if (packet.Any() && packet.Length > 54)
                {
                    this.Servers.Game.AddPacket(packet);
                }


            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        public void DisplayPackets(RichTextBox tb, Server server)
        {
            try
            {
                if (server.Packets.Any())
                {
                    foreach (var packet in server.Packets)
                    {
                        tb.AppendText(string.Format("{0}\n", packet.Value.GetDisplayData()));
                    }
                }
                else
                {
                    throw new Exception(string.Format("No packets for {0}.", server.Type));
                }
            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private static void Msg(string msg = "")
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("UCPacketParser::> {0}\n", msg));
            }
        }
    }
}
