using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PortiaJsonOrientedMultiThread.Core.Models;

namespace PortiaJsonOrientedMultiThread.Core.Dtos
{
    public class Request
    {
        public string ProjectName { get; set; }
        public string StartUrl { get; set; }
        public IList<string> DisallowedStrings { get; set; }
        public List<DataForRequest> Data { get; set; }
    }
}
