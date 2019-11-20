﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.Models
{
    public class Metadata
    {
        public string FoundAtUrl { get; set; }
        public DateTime DateFound { get; set; }
        public Metadata(string FoundAtUrl, DateTime DateFound)
        {
            this.FoundAtUrl = FoundAtUrl;
            this.DateFound = DateFound;
        }
    }
}
