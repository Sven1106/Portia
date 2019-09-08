using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Models
{
    public class PortiaRequest
    {
        public string ApiVersion { get; set; }
        public Data Data { get; set; }
    }
}
