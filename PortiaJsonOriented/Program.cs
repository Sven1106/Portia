using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using PortiaJsonOriented.Core;
using PortiaJsonOriented.Core.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortiaJsonOriented
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().Wait();
        }
        static async Task MainAsync()
        {
            var example1 = File.ReadAllText("request.json");
            string response = await Webcrawler.StartCrawlerAsync(example1);
        }

    }

}
