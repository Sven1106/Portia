using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PortiaObjectOriented;

namespace PortiaObjectOriented.Dto
{
    [Xpath(".//div/section[1]/section/div[2]/div/div[1]/div[2]/div/div/ul/li")]
    public class Ingredient
    {
        [Xpath]
        public string Name { get; set; }
    }
}
