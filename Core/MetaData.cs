using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Portia.Core
{
    public class Metadata
    {
        public string FoundAtUrl { get; set; }
        public DateTime LastModified { get; set; }
        public Metadata(string FoundAtUrl, DateTime LastModified)
        {
            this.FoundAtUrl = FoundAtUrl;
            this.LastModified = LastModified;
        }
    }
}
