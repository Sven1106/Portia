using System;

namespace AkkaWebcrawler.Common.Models.Deserialization
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
