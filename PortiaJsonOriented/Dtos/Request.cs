using System;
using System.Collections.Generic;
using PortiaJsonOriented.Models;

namespace PortiaJsonOriented.DTO
{
    public class PortiaRequest // Should requests be immutable?
    {
        public Guid Id { get; set; } // Id is created on Client. Creates a new puppeteer instance pr Id.
        public string ProjectName { get; set; } 
        public Uri Domain { get; set; }
        public List<Uri> StartUrls { get; set; }
        public bool IsFixedListOfUrls { get; set; }
        public string XpathForLoadMoreButton { get; set; }
        public List<Job> Jobs { get; set; }
    }
}
