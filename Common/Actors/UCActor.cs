using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartEngine.Network.Tasks;

namespace Common.Actors
{
    public class UCActor
    {

        ConcurrentDictionary<ulong, ulong> visibleActors = new ConcurrentDictionary<ulong, ulong>();
        ConcurrentDictionary<string, Task> tasks = new ConcurrentDictionary<string, Task>();
        public byte Level { get; set; }
        //Use field instead of property because it'll be used in Interlocked
        public int HP;
        //Use field instead of property because it'll be used in Interlocked
        public int MP;
        public int MaxHP { get; set; }
        public ushort MaxMP { get; set; }
        public UCActor FaceTo { get; set; }
        public bool Combat { get; set; }
        public ConcurrentDictionary<ulong, ulong> VisibleActors { get { return visibleActors; } }
        public ConcurrentDictionary<string, Task> Tasks { get { return tasks; } }

    }
}
