using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class HtmlContent
    {
        public Uri SourceUrl { get; private set; }
        public string Html { get; private set; }
        public HtmlContent()
        {
        }
        public HtmlContent(Uri url, string html)
        {
            SourceUrl = url;
            Html = html;
        }
    }
}
