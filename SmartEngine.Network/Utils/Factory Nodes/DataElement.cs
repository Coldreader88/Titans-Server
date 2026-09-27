using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartEngine.Network.Utils.Factory_Nodes
{
    public class DataElement
    {
        public string Name { get; set; }
        public string InnerText { get; set; }

        public DataElement(string Name = "default", string InnerText = "default")
        {
            this.Name = Name;
            this.InnerText = InnerText;
        }
    }
}
