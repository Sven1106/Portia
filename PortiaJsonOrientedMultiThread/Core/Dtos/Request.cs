using System.Collections.Generic;
using PortiaJsonOrientedMultiThread.Core.Models;

namespace PortiaJsonOrientedMultiThread.Core.Dtos
{
    public class Request
    {
        public string ProjectName { get; set; }
        public string StartUrl { get; set; }
        public IList<string> DisallowedStrings { get; set; }
        public List<Task> Tasks { get; set; }
    }
}
