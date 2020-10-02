using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class HtmlContentMessage
    {
        public Uri SourceUrl { get; private set; }
        public string Html { get; private set; }
        public HtmlContentMessage()
        {
        }
        public HtmlContentMessage(Uri url, string html)
        {
            SourceUrl = url;
            Html = html;
        }
    }
}
