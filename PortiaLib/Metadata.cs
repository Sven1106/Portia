using System;

namespace PortiaLib
{
    public class Metadata
    {
        public string FoundAtUrl { get; private set; }
        public DateTime DateTimeFound { get; private set; }
        public Metadata(string FoundAtUrl, DateTime DateTimeFound)
        {
            this.FoundAtUrl = FoundAtUrl;
            this.DateTimeFound = DateTimeFound;
        }
    }
}
