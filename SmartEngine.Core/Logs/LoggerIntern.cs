using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Text;

namespace SmartEngine.Core
{
    public struct LogData
    {
        public Level LogLevel;
        public string Text;
        public NLog.Logger Logger;
    }

    public enum Level
    {
        Debug,
        Info,
        Warn,
        Error,
        SQL,
    }

    public class LoggerIntern
    {
        static ConcurrentQueue<LogData> queue = new ConcurrentQueue<LogData>();
        static Thread thread;
        static AutoResetEvent waiter = new AutoResetEvent(false);
        public static bool Ready = false;

        /// <summary>
        /// TITANS_CONSOLE_LOG=1 prints every message to the console as "[Level] text" instead of NLog (the scripted
        /// tests read the servers' output; NLog does not run under mono).
        /// </summary>
        static readonly bool console = Environment.GetEnvironmentVariable("TITANS_CONSOLE_LOG") == "1";

        public static void Init()
        {
            if (thread == null)
            {
                thread = new Thread(MainLoop);
                thread.Start();
                Ready = true;
            }
        }

        static void MainLoop()
        {
            while (true)
            {
                LogData data;
                while (queue.TryDequeue(out data))
                {
                    if (console)
                    {
                        Console.WriteLine("[" + data.LogLevel + "] " + data.Text);
                        continue;
                    }
                    if (data.Logger == null)
                    {
                        // Logged before the logger was set up: print it rather than crash the log thread.
                        Console.WriteLine(data.Text);
                        continue;
                    }
                    switch (data.LogLevel)
                    {
                        case Level.Debug:
                            data.Logger.Debug(data.Text);
                            break;
                        case Level.Info :
                            data.Logger.Info(data.Text);
                            break;
                        case Level.Warn:
                            data.Logger.Warn(data.Text);
                            break;
                        case Level.Error:
                            data.Logger.Error(data.Text);
                            break;
                        case Level.SQL:
                            data.Logger.Debug(data.Text);
                            break;
                    }
                }
                waiter.WaitOne();
            }
        }

        public static void EnqueueMsg(Level level, string msg, NLog.Logger logger)
        {
            LogData data = new LogData();
            data.LogLevel = level;
            data.Text = msg;
            data.Logger = logger;
            queue.Enqueue(data);
            waiter.Set();
        }
    }
}
