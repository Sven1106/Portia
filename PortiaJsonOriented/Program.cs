using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PortiaJsonOriented
{
    class Program
    {
        private static readonly ConcurrentDictionary<string, Type> propertyNamesToTypes = new ConcurrentDictionary<string, Type>();
        static void Main(string[] args)
        {
            MainAsync().Wait();
        }


        static async Task MainAsync()
        {
            var request = File.ReadAllText("Request.json");
            var requestSchema = File.ReadAllText(Path.Combine("Core","RequestSchema.json"));
            JSchema schema = JSchema.Parse(requestSchema);
            JObject requestAsJson = JObject.Parse(request);
            bool isValid = requestAsJson.IsValid(schema);
            if (isValid)
            {
                var response = await Webcrawler.StartCrawlerAsync(requestAsJson.ToString());
            }

        }
    }
}
