using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Models
{
    public class Attributes
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsMultiple { get; set; }
        public string Xpath { get; set; }
        public List<Attributes> attributes { get; set; }
    }
}
