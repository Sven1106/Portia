using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class UrlForUrlAndObjectParsing
    {

        public Uri Url { get; private set; }
        public UrlForUrlAndObjectParsing(Uri url)
        {
            Url = url;
        }
    }
}
