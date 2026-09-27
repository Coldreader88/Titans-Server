using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UCPacketViewer.PacketHandler;

namespace UCPacketViewer
{
    public partial class UCPacketViewer : Form
    {
        public UCPacketParser PacketParser;
        public PacketHandler.PacketHandler Handler;

        public UCPacketViewer()
        {
            InitializeComponent();
        }

        private void UCPacketViewer_Load(object sender, EventArgs e)
        {
            this.FormClosed += UCPacketViewer_FormClosed;
            this.serverList.DropDownStyle = ComboBoxStyle.DropDownList;
            //this.packetOutput.Text

            this.PacketParser = new UCPacketParser();
            this.Handler = new PacketHandler.PacketHandler();
        }

        void UCPacketViewer_FormClosed(object sender, FormClosedEventArgs e)
        {
            exitToolStripMenuItem_Click(sender, e);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Util.ShowError(new NotImplementedException());
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Util.MsgBox("About", "UCPacketViewer Created by Dante 'Brian Burnett'");
        }

        private void packetList_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {

            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                switch (oFileDialog.ShowDialog())
                {
                    case DialogResult.OK:
                    case DialogResult.Yes:
                        {
                            PacketParser.HandlePackets(oFileDialog.FileName);
                            serverList_SelectedIndexChanged(null, null);
                            //PacketParser.DisplayPackets(this.packetOutput, PacketParser.Servers.Login);
                            break;
                        }

                    case DialogResult.Cancel:
                    default:
                        {
                            return;
                        }
                }

                if (PacketParser.Servers.Any())
                {
                    this.serverList.Enabled = true;
                    this.packetList.Enabled = true;
                }
                else
                {
                    this.serverList.Enabled = false;
                    this.packetList.Enabled = false;
                }

                this.setPacketCount();

            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void serverList_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                packetList.Items.Clear();

                List<UCViewerPacket> packetSublist = new List<UCViewerPacket>();

                switch (serverList.Text.ToLower())
                {
                    case "info":
                        {
                            packetSublist = PacketParser.Servers.Info.Packets.Values.ToList();
                            break;
                        }

                    case "login":
                        {
                            packetSublist = PacketParser.Servers.Login.Packets.Values.ToList();
                            break;
                        }

                    case "cms":
                        {
                            packetSublist = PacketParser.Servers.Cms.Packets.Values.ToList();
                            break;
                        }

                    case "game":
                        {
                            packetSublist = PacketParser.Servers.Game.Packets.Values.ToList();
                            break;
                        }

                    default:
                        {
                            packetSublist = PacketParser.Servers.Login.Packets.Values.ToList();
                            break;
                        }
                }

                foreach (var ucViewerPacket in packetSublist)
                {
                    packetList.Items.Add(new UCPacketListItem(ucViewerPacket));
                }

                this.setPacketCount();
            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void packetList_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                this.packetOutput.Clear();

                var item = (UCPacketListItem)packetList.SelectedItem;

                if (item != null)
                {
                    //this.packetOutput.AppendText(item.Packet.GetDisplayData());
                    Handler.HandlePacket(item.Packet, packetOutput, chkIncHeader.Checked);
                }


            }
            catch (Exception ex)
            {
                Util.ShowError(ex);
            }
        }

        private void setPacketCount()
        {
            this.lblPacketCount.Text = string.Format("Packet Count: {0}", this.packetList.Items.Count);
        }

    }
}
