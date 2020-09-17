using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class UrlForObjectParsing
    {
        public Uri Url { get; private set; }
        public UrlForObjectParsing(Uri url)
        {
            Url = url;
        }
    }
}
