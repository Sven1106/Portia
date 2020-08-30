using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Models
{
    public class NodeAttribute
    {
        public string Name { get; set; }
        public NodeType Type { get; set; }
        public bool GetMultipleFromPage { get; set; }
        public bool IsRequired { get; set; } // Can an object be required when its' properties aren't?
        public string Xpath { get; set; }
        public List<NodeAttribute> Attributes { get; set; }
    }
}
