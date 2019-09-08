using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PortiaObjectOriented.Core;

namespace PortiaObjectOriented.Dto
{
    [Xpath(".//*[@id=\"main-content\"]")]
    public class Recipe : IWebcrawler
    {
        public Metadata Metadata { get; set; }

        [Xpath(".//div/section[1]/section/header/div[1]/h1")]
        public string Heading { get; set; }
        public List<Ingredient> Ingredients { get; set; }
        public Image image { get; set; }
    }
}
