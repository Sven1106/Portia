using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.DTO
{
    public class PortiaResponse
    {
        public string ProjectName { get; set; }
        public Uri Domain { get; set; }
        public dynamic Jobs { get; set; }
    }
}
