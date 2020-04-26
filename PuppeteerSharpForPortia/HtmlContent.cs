using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PuppeteerSharpForPortia
{
    public class HtmlContent
    {
        public Uri Url { get; set; }
        public string Html { get; set; }
        public HtmlContent(Uri url, string html)
        {
            Url = url;
            Html = html;
        }
        public HtmlContent()
        {
        }
    }
}
