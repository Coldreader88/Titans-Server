using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartEngine.Network.IO
{
    public interface Readable
    {
        void Read(ByteBuffer buffer);
    }
}
