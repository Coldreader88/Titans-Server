using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Common.Network.Packets;

using PcapDotNet.Packets;
using SmartEngine.Core;

namespace UCPacketViewer.PacketHandler
{

    public class UCViewerPacket : UCPacket<uint>
    {

        private DateTime _capTime;

        public DateTime CaptureTime
        {
            get { return _capTime.ToLocalTime(); }

            set { _capTime = value; }
        }

        public ServerType Server { get; set; }

        public int Size { get { return this.Buffer.Length; } }

        public string SourceAddress { get; set; }
        public string DestinationAddress { get; set; }
        public ushort SourcePort { get; set; }
        public ushort DestinationPort { get; set; }
        public bool HasHeader { get; set; }

        public Packet PCapPacket { get; set; }

        public UCViewerPacket(Packet packet, ServerType server, PacketType packetType = PacketType.UNKNOWN)
        {
            this.PCapPacket = packet;
            this.Server = server;
            this.Type = packetType;
            this.HasHeader = true;

            this.CaptureTime = this.PCapPacket.Timestamp;
        }

        public UCViewerPacket Payload()
        {
            try
            {
                if (this.Size > 64)
                {
                    ushort size = Convert.ToUInt16(this.Header.XORSize);

                    var data = this.GetBytes(size, 64);

                    var p = new UCViewerPacket(this.PCapPacket, this.Server);

                    p.ID = this.ID;

                    p.PutBytes(data, 0);

                    p.HasHeader = false;

                    return p;
                }
                else
                {
                    return this;
                }
            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
                return this;
            }
        }

        public string GetDisplayData()
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine(this.CaptureTime.ToString("yyyy-MM-dd hh:mm:ss.fff") + " length:" + this.Length +
               "\n");
                sb.AppendLine();

                string dataHeader = "";
                dataHeader += "=================================================\r\n";
                dataHeader += " 1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 |\r\n";
                dataHeader += "=================================================\r\n";
                ushort pos = offset;
                sb.Append(string.Format("Packet Data:\n{2}(0x{0:X4})\r\n\n" + dataHeader + "{1}\n", this.ID, this.DumpData(), this.ID));
                offset = pos;

                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                Util.ShowError(ex);
            }

            return "";
        }

    }

    public class UCPacketListItem : ListViewItem
    {
        public UCViewerPacket Packet { get; set; }

        public List<UCViewerPacket> PacketList { get; set; }

        public UCPacketListItem(UCViewerPacket packet)
            : base()
        {
            this.Packet = packet;
            this.Text = string.Format("0x{0:X4}", packet.ID);
            this.Tag = "";
            this.PacketList = new List<UCViewerPacket>();
        }
    }
}
