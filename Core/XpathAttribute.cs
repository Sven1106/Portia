using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portia.Core
{
    public class XpathAttribute : Attribute
    {
        public string NodeXpath { get; }
        public XpathAttribute()
        {
           this.NodeXpath = string.Empty;
        }
        public XpathAttribute(string NodeXpath)
        {
            this.NodeXpath = NodeXpath;
        }
    }
}
