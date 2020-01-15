using System.Collections.Generic;
using PortiaJsonOriented.Core.Models;

namespace PortiaJsonOriented.Core.Dtos
{
    public class Request
    {
        public string ProjectName { get; set; }
        public string StartUrl { get; set; }
        public IList<string> DisallowedStrings { get; set; }
        public List<Task> Tasks { get; set; }
    }
}
