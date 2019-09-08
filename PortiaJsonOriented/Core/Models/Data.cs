using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Models
{
    public class Data
    {
        public string Url { get; set; }
        public string ProjectName { get; set; }
        public List<Attributes> Items { get; set; }
    }
}
