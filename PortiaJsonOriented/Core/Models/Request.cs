using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Models
{
    public class Request
    {
        public string StartUrl { get; set; }
        public List<Data> Data { get; set; }
    }
}
