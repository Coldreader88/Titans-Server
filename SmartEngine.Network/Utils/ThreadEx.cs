// /*
//  * @author Vampirecat
// * @Year: 2013 @Month: 11 @Day: 12
// */

#region usings

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SmartEngine.Network;
using System.Collections.Generic;

#endregion

namespace SmartEngine.Network.Utils
{
    /// <summary>
    ///     Noobish Executor for Threading
    /// </summary>
    public class ThreadEx : Singleton<ThreadEx>, IDisposable
    {
        private readonly RandomF r = new RandomF();
        private ConcurrentDictionary<int, Timer> _delayedTimer = new ConcurrentDictionary<int, Timer>();
        private ConcurrentDictionary<int, Timer> _periodicTimer = new ConcurrentDictionary<int, Timer>();

        public int DelayedTimerCount
        {
            get { return _delayedTimer.Count; }
        }

        public int PeriodicTimerCount
        {
            get { return _periodicTimer.Count; }
        }

        /// <summary>
        ///     Called when Object is getting deleted
        /// </summary>
        public void Dispose()
        {
            CancelAll();
            _periodicTimer = null;
            _delayedTimer = null;
        }

        /// <summary>
        ///     Schedule a Delayed Task with Delegate
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="action"></param>
        /// <returns>Timer</returns>
        public Timer Schedule(int delay, Action action)
        {
            var threadId = r.Next();
            var timer = new Timer(delegate
            {
                Timer t;
                _delayedTimer.TryRemove(threadId, out t);
                action.Invoke();
            }, null, delay, Timeout.Infinite);

            _delayedTimer.TryAdd(threadId, timer);
            return timer;
        }

        /// <summary>
        ///     Perform a Timer Periodic
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="period"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public Timer SchedulePeriodic(int delay, int period, Action action)
        {
            var threadId = r.Next();
            var timer = new Timer(delegate
            {
                Timer t;
                _periodicTimer.TryRemove(threadId, out t);
                action.Invoke();
            }, null, delay, period);

            _periodicTimer.TryAdd(threadId, timer);
            return timer;
        }

        /// <summary>
        ///     Execute stuff where you dont have to wait to finish - Pool
        /// </summary>
        /// <param name="c"></param>
        public void ExecuteNowOnPool(WaitCallback c)
        {
            ThreadPool.QueueUserWorkItem(c);
        }

        /// <summary>
        ///     Execute stuff where you dont have to wait to finish - Task
        /// </summary>
        /// <param name="c"></param>
        public Task ExecuteNowVTask(Action a)
        {
            var task = Task.Factory.StartNew(a);
            return task;
        }

        /// <summary>
        ///     Best Usages: For large Tasks
        /// </summary>
        /// <param name="a"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public Task ExecuteNowVTask(Action a, CancellationToken token)
        {
            var task = Task.Factory.StartNew(a, token);
            return task;
        }


        /// <summary>
        ///     Very Slow compared to other Methods, please only use if you need full Controll of a Thread
        /// </summary>
        /// <param name="start"></param>
        /// <param name="p"></param>
        /// <param name="ob"></param>
        /// <returns></returns>
        public Thread ExecuteWithControl(ParameterizedThreadStart start, ThreadPriority p, object ob)
        {
            var t = new Thread(start) { Priority = p };
            t.Start(ob);
            return t;
        }

        /// <summary>
        /// Execute every method with a own Thread from Pool
        /// </summary>
        /// <param name="methods"></param>
        /// <returns>List size matchs executed counter</returns>
        public bool BatchExecute(List<Action> methods)
        {
            var doneCounter = methods.Count;
            var executed = 0;

            foreach (Action a in methods)
            {
                ExecuteNowOnPool(new WaitCallback(obj => a.Invoke()));
                executed++;
            }

            return executed == doneCounter;
        }

        /// <summary>
        ///     Cancel all stored Tasks/Timers
        /// </summary>
        public void CancelAll()
        {
            foreach (var timer in _periodicTimer)
            {
                timer.Value.Dispose();
            }
            _periodicTimer.Clear();

            foreach (var timer in _delayedTimer)
            {
                timer.Value.Dispose();
            }
            _delayedTimer.Clear();
        }
    }
}