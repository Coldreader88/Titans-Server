using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using SmartEngine.Core;

namespace UCPacketViewer
{
    public static class Util
    {

        public static DialogResult MsgBox(string title = "", string text = "",
                                          MessageBoxIcon mbIcon = MessageBoxIcon.Exclamation,
                                          MessageBoxButtons mbButtons = MessageBoxButtons.OK)
        {
            #if DEBUG
            Logger.ShowWarning(string.Format("[{0}]::>{1}", title.Any() ? "UCPacketViewer::" + title : "UCPacketViewer", text));
            #endif
            return MessageBox.Show(text, title.Any() ? "UCPacketViewer::" + title : "UCPacketViewer", mbButtons, mbIcon);
        }

        public static DialogResult MsgBox(string title, string fmt, object[] args, MessageBoxIcon mbIcon,
                                          MessageBoxButtons mbButtons)
        {
            #if DEBUG
            Logger.ShowWarning(fmt, args);
            #endif
            return MessageBox.Show(string.Format(fmt, args), title.Any() ? "UCPacketViewer::" + title : "UCPacketViewer", mbButtons, mbIcon);
        }

        public static DialogResult ShowError(Exception ex)
        {
            return MsgBox(string.Format("UCPacketViewer::ERROR [{0}]", ex.GetType().Name),
                          string.Format("{0}.\n{1}.\n{2}.\n", ex.Message, ex.Source, ex.StackTrace),
                          MessageBoxIcon.Error, MessageBoxButtons.AbortRetryIgnore);
        }

    }
}
