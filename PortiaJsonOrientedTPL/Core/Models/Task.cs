using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOrientedTPL.Core.Models
{
    public class Task
    {
        public string TaskName { get; set; }
        public List<NodeAttribute> Items { get; set; }
    }
}
