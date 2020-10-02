using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class UnprocessedUrlsMessage
    {
        public List<Uri> Urls { get; private set; }
        public UnprocessedUrlsMessage(List<Uri> urls)
        {
            Urls = urls;
        }
    }
}
