using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class UrlForObjectParsingMessage
    {
        public Uri Url { get; private set; }
        public UrlForObjectParsingMessage(Uri url)
        {
            Url = url;
        }
    }
}
