using System;
using System.Collections.Generic;
using PortiaJsonOriented.Core.Models;

namespace PortiaJsonOriented.Core.DTO
{
    public class PortiaRequest // Should requests be immutable?
    {
        public Guid Id { get; set; } // Id is created on Client. Creates a new puppeteer instance pr Id.
        public string ProjectName { get; set; } 
        public Uri Domain { get; set; }
        public List<Uri> StartUrls { get; set; }
        public bool IsFixedListOfUrls { get; set; }

        // TODO CrawlerSettings
        //// return List of objects after every x object found. 0 being unlimited
        public List<Job> Jobs { get; set; }
    }
}
