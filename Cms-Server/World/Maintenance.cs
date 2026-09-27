using System;
using System.Threading;
using SmartEngine.Core;
using TitansUC.CmsServer.Manager;

namespace TitansUC.CmsServer.World
{
    /// <summary>
    /// The #shutdown countdown: announces the maintenance to everyone, then tells the game servers to
    /// close. Java reference: event/InformClosingEvent.java, which announced every hour, then every
    /// minute, then every second; here the last minute is announced at 30, 10 and 5 to 1 seconds.
    /// </summary>
    public class Maintenance
    {
        static readonly Maintenance instance = new Maintenance();

        public static Maintenance Instance { get { return instance; } }

        private readonly object sync = new object();
        private Timer timer;
        private int remaining;

        public void Start(int seconds)
        {
            lock (sync)
            {
                Stop();
                remaining = seconds;
                Logger.ShowInfo(string.Format("Maintenance in {0} seconds.", seconds));
                Announce();
                timer = new Timer(Tick, null, 1000, 1000);
            }
        }

        public void Cancel()
        {
            lock (sync)
            {
                if (timer != null)
                {
                    Logger.ShowInfo("Maintenance countdown cancelled.");
                }
                Stop();
            }
        }

        private void Stop()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }

        private void Tick(object state)
        {
            lock (sync)
            {
                if (timer == null)
                {
                    return;
                }

                remaining--;
                if (remaining > 0)
                {
                    if (ShouldAnnounce(remaining))
                    {
                        Announce();
                    }
                    return;
                }

                Stop();
            }

            CmsWorld.Instance.SystemMessage("Thank you for playing UCGO!\nPlease remain calm while TITANS has scheduled maintenance!");
            GameLinkManager.Instance.Closure(0);
            Logger.ShowInfo("Maintenance started; the game servers were told to close.");
        }

        private static bool ShouldAnnounce(int seconds)
        {
            if (seconds >= 3600)
            {
                return seconds % 3600 == 0;
            }
            if (seconds >= 60)
            {
                return seconds % 60 == 0;
            }
            return seconds == 30 || seconds == 10 || seconds <= 5;
        }

        private void Announce()
        {
            string text;
            if (remaining >= 3600)
            {
                text = string.Format("{0} hours", remaining / 3600);
            }
            else if (remaining >= 60)
            {
                text = string.Format("{0} minutes", remaining / 60);
            }
            else
            {
                text = string.Format("{0} seconds", remaining);
            }
            CmsWorld.Instance.SystemMessage("The server will close for maintenance in " + text + ".");
        }
    }
}
