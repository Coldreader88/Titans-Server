using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace UCPacketViewer.PacketHandler
{
    public partial class PacketHandler
    {

        public void HandlePacket(UCViewerPacket packet, TextBox tb, bool includeHeader = true)
        {
            
            try
            {

                var p = packet;

                if (!includeHeader)
                {
                    p = packet.Payload();
                }

                tb.Clear();

                string temp = "";

                switch (p.Server)
                {

                    case ServerType.INFO:
                        {
                            temp = HandleInfoPackets(p);
                            break;
                        }

                        case ServerType.LOGIN:
                        {
                            temp = HandleLoginPackets(p);
                            break;
                        }

                        case ServerType.CMS:
                        {
                            temp = HandleCmsPackets(p);
                            break;
                        }

                        case ServerType.GAME:
                        {
                            temp = HandleGamePackets(p);
                            break;
                        }
                }

                DisplayPacket(p, temp + "\n", tb);
            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void DisplayPacket(UCViewerPacket p, string extra, TextBox tb)
        {
            try
            {

                var captureTimeInfo = string.Format("Time Captured: [{0:G}]", p.CaptureTime.ToLocalTime());
                string dataHeader = "";
                dataHeader += "=================================================\r\n";
                dataHeader += " 1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 |\r\n";
                dataHeader += "=================================================\r\n";
                //string tmp = "Sender:{0}\r\nOpcode:0x{1:X4}\r\nName:{2}\r\n\r\n{5}\r\n\r\nLength:{3}\r\nData:" + tmp3 + "\r\n{4}";
                string tmp = "Server:{7}\r\nSender:{0}\r\n{6}\r\nOpcode:0x{1:X4}\r\nName:{2}\r\n\r\n{5}\r\n\r\nLength:{3}\r\nData:\r\n\r\n" + dataHeader + "{4}";
                string tmp2 = p.DumpDetailedData();
                tmp = string.Format(tmp, "Unknown", p.ID, "UCPacket", p.Length, tmp2, extra, captureTimeInfo, p.Server.ToString());
                
                tb.AppendText(tmp);
            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

    }
}
