using System;
using System.Collections.Generic;
using PortiaJsonOriented.Core.Models;

namespace PortiaJsonOriented.Core.Dtos
{
    public class PortiaRequest
    {
        public string ProjectName { get; set; }
        public Uri Domain { get; set; }
        public List<Uri> StartUrls { get; set; }
        public bool IsFixedListOfUrls { get; set; }
        public List<Task> Tasks { get; set; }
    }
}
