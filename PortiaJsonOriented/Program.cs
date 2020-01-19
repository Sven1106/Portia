using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;
using PortiaJsonOriented.Core;
using PortiaJsonOriented.Core.Dtos;
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
        static async Task Main(string[] args)
        {
            string solutionRootPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\"));
            var json = File.ReadAllText(Path.Combine(solutionRootPath, "request.json"));

            JSchemaValidatingReader jSchemaReader = new JSchemaValidatingReader(new JsonTextReader(new StringReader(json)));
            jSchemaReader.Schema = JSchema.Parse(File.ReadAllText(Path.Combine(solutionRootPath, "requestSchema.json")));

            IList<string> errorMessages = new List<string>();
            jSchemaReader.ValidationEventHandler += (o, a) => errorMessages.Add(a.Message);
            JsonSerializer serializer = new JsonSerializer();
            Request request = serializer.Deserialize<Request>(jSchemaReader);
            if (errorMessages.Count > 0)
            {
                foreach (var eventMessage in errorMessages)
                {
                    Console.WriteLine(eventMessage);
                }
                Console.ReadKey();
                return;
            }
            WebcrawlerTpl webcrawler = new WebcrawlerTpl();
            Response response = await webcrawler.StartCrawlerAsync(request);
            var settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };
            File.WriteAllText("response.json", JsonConvert.SerializeObject(response, settings));
        }
    }

}
