using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class ParsedUrls
    {
        public List<Uri> Urls { get; private set; }
        public ParsedUrls(List<Uri> urls)
        {
            Urls = urls;
        }
    }
}
