using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class ProcessedUrlMessage
    {
        public Uri Url { get; set; }
        public ProcessedUrlMessage(Uri url)
        {
            Url = url;
        }
    }
}
