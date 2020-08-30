using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class ObjectParsing
    {
        public Uri Url { get; private set; }
        public ObjectParsing(Uri url)
        {
            Url = url;
        }
    }
}
