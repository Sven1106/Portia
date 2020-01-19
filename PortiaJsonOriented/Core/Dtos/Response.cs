using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Dtos
{
    public class Response
    {
        public string ProjectName { get; set; }
        public Uri StartUrl { get; set; }
        public dynamic Task { get; set; }
    }
}
