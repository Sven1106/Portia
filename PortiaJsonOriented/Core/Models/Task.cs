using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Models
{
    public class Task
    {
        public string TaskName { get; set; }
        public List<NodeAttribute> Nodes { get; set; }
    }
}
