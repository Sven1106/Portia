using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PortiaObjectOriented.Core;

namespace PortiaObjectOriented.Dto
{
    [Xpath(".//*[@class='versions-item']")]
    public class Car : IWebcrawler
    {
        public Metadata Metadata { get; set; }
        [Xpath(".//h2")]
        public string Model { get; set; }
        [Xpath(".//div[@class='price']/span")]
        public string Price { get; set; }
        public Image Image { get; set; }
    }

}
