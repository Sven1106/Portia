using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented.Core.DTOS
{
    public class RecipeDto
    {
        public string heading { get; set; }
        public List<string> ingredients { get; set; }
        public List<Image> images { get; set; }
    }

    public class Image
    {
        public string src { get; set; }
        public string alt { get; set; }
    }
}
