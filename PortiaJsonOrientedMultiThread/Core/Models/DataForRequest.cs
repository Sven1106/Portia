using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOrientedMultiThread.Core.Models
{
    public class DataForRequest
    {
        public string TaskName { get; set; }
        public List<NodeAttribute> Items { get; set; }
    }
}
