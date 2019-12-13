using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOrientedMultiThread.Core.Dtos
{
    public class Response
    {
        public string ProjectName { get; set; }
        public string StartUrl { get; set; }
        public dynamic Data { get; set; }
    }
}
