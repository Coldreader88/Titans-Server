using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using System.IO;

using SmartEngine.Core;

namespace SmartEngine.Network.IO
{

    public interface Writable
    {
        ByteBuffer Write();
    }
}