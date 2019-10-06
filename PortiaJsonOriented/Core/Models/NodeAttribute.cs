using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Models
{
    public class NodeAttribute
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool MultipleFromPage { get; set; }
        public string Xpath { get; set; }
        public List<NodeAttribute> Attributes { get; set; }
    }
}
