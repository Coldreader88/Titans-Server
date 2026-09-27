using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartEngine.Network.Utils.FactoryDataTypes;

namespace SmartEngine.Network.Utils
{
    public abstract class ModularFactory<K, T> : DuplicateFactory<K, T, uint, uint>
        where K : new()
        where T : TTItem<uint, uint>, new()
    {
        public ModularFactory()
            : base()
        {

        }

    }
}
