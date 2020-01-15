using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOrientedTPL.Core.Models
{
    public class UrlHtmlPair
    {
        public Uri Url { get; set; }
        public string Html { get; set; }
        public UrlHtmlPair(Uri url, string html)
        {
            Url = url;
            Html = html;
        }
        public UrlHtmlPair()
        {
        }
    }
}
