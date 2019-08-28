using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portia.Core;

namespace Portia
{
    interface IWebcrawler
    {
        Metadata Metadata { get; set; }
    }
}
