using System.Collections.Generic;
using PortiaJsonOrientedTPL.Core.Models;

namespace PortiaJsonOrientedTPL.Core.Dtos
{
    public class Request
    {
        public string ProjectName { get; set; }
        public string StartUrl { get; set; }
        public IList<string> DisallowedStrings { get; set; }
        public List<Task> Tasks { get; set; }
    }
}
