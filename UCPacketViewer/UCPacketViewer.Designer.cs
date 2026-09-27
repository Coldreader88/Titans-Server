using System.Windows.Forms;

namespace UCPacketViewer
{
    partial class UCPacketViewer
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UCPacketViewer));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.oFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.packetList = new System.Windows.Forms.ListBox();
            this.packetOutput = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lblPacketCount = new System.Windows.Forms.Label();
            this.chkIncHeader = new System.Windows.Forms.CheckBox();
            this.serverList = new System.Windows.Forms.ComboBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.menuStrip1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.viewToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(802, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.closeToolStripMenuItem,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // closeToolStripMenuItem
            // 
            this.closeToolStripMenuItem.Name = "closeToolStripMenuItem";
            this.closeToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.closeToolStripMenuItem.Text = "Close";
            this.closeToolStripMenuItem.Click += new System.EventHandler(this.closeToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(97, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(41, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(103, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // oFileDialog
            // 
            this.oFileDialog.FileName = "openFileDialog1";
            this.oFileDialog.Filter = "*.pcap|*.pcap";
            // 
            // packetList
            // 
            this.packetList.Enabled = false;
            this.packetList.FormattingEnabled = true;
            this.packetList.Location = new System.Drawing.Point(6, 54);
            this.packetList.Name = "packetList";
            this.packetList.Size = new System.Drawing.Size(164, 407);
            this.packetList.TabIndex = 0;
            this.packetList.SelectedIndexChanged += new System.EventHandler(this.packetList_SelectedIndexChanged);
            this.packetList.DoubleClick += new System.EventHandler(this.packetList_DoubleClick);
            // 
            // packetOutput
            // 
            this.packetOutput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.packetOutput.Location = new System.Drawing.Point(6, 92);
            this.packetOutput.Multiline = true;
            this.packetOutput.Name = "packetOutput";
            this.packetOutput.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.packetOutput.Size = new System.Drawing.Size(560, 369);
            this.packetOutput.TabIndex = 1;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.lblPacketCount);
            this.groupBox1.Controls.Add(this.chkIncHeader);
            this.groupBox1.Controls.Add(this.serverList);
            this.groupBox1.Controls.Add(this.packetList);
            this.groupBox1.Location = new System.Drawing.Point(12, 34);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(176, 493);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Server Selection";
            // 
            // lblPacketCount
            // 
            this.lblPacketCount.AutoSize = true;
            this.lblPacketCount.Location = new System.Drawing.Point(6, 464);
            this.lblPacketCount.Name = "lblPacketCount";
            this.lblPacketCount.Size = new System.Drawing.Size(84, 13);
            this.lblPacketCount.TabIndex = 3;
            this.lblPacketCount.Text = "Packet Count: 0";
            // 
            // chkIncHeader
            // 
            this.chkIncHeader.AutoSize = true;
            this.chkIncHeader.CheckAlign = System.Drawing.ContentAlignment.BottomRight;
            this.chkIncHeader.Checked = true;
            this.chkIncHeader.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkIncHeader.Location = new System.Drawing.Point(71, 31);
            this.chkIncHeader.Name = "chkIncHeader";
            this.chkIncHeader.Size = new System.Drawing.Size(99, 17);
            this.chkIncHeader.TabIndex = 2;
            this.chkIncHeader.Text = "Include Header";
            this.chkIncHeader.UseVisualStyleBackColor = true;
            // 
            // serverList
            // 
            this.serverList.Enabled = false;
            this.serverList.FormattingEnabled = true;
            this.serverList.Items.AddRange(new object[] {
            "Info",
            "Login",
            "CMS",
            "Game"});
            this.serverList.Location = new System.Drawing.Point(6, 27);
            this.serverList.Name = "serverList";
            this.serverList.Size = new System.Drawing.Size(59, 21);
            this.serverList.TabIndex = 1;
            this.serverList.Text = "Login";
            this.serverList.SelectedIndexChanged += new System.EventHandler(this.serverList_SelectedIndexChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.groupBox3);
            this.groupBox2.Controls.Add(this.packetOutput);
            this.groupBox2.Location = new System.Drawing.Point(194, 34);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(596, 493);
            this.groupBox2.TabIndex = 4;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Packet Output";
            // 
            // groupBox3
            // 
            this.groupBox3.Location = new System.Drawing.Point(6, 19);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(560, 67);
            this.groupBox3.TabIndex = 3;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Conversions";
            // 
            // UCPacketViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(802, 539);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "UCPacketViewer";
            this.Text = "UCPacketViewer";
            this.Load += new System.EventHandler(this.UCPacketViewer_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem closeToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog oFileDialog;
        private System.Windows.Forms.ListBox packetList;
        private TextBox packetOutput;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.ComboBox serverList;
        private System.Windows.Forms.CheckBox chkIncHeader;
        private System.Windows.Forms.Label lblPacketCount;
    }
}

