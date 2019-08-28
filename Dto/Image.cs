using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portia.Core;

namespace Portia.Dto
{
    [Xpath(".//img")]
    public class Image
    {

        [Xpath(".//@src")]
        public string Url { get; set; }
        [Xpath(".//@alt")]
        public string Alt { get; set; }
    }
}
