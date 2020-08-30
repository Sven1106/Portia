﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;
using PortiaJsonOriented;
using PortiaJsonOriented.DTO;
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
            var json = File.ReadAllText(Path.Combine(solutionRootPath, "CoopRequest.json"));

            JSchemaValidatingReader jSchemaReader = new JSchemaValidatingReader(new JsonTextReader(new StringReader(json)));
            jSchemaReader.Schema = JSchema.Parse(File.ReadAllText(Path.Combine(solutionRootPath, "requestSchema.json")));

            IList<string> errorMessages = new List<string>();
            jSchemaReader.ValidationEventHandler += (o, a) => errorMessages.Add(a.Message);
            JsonSerializer serializer = new JsonSerializer();
            PortiaRequest request = serializer.Deserialize<PortiaRequest>(jSchemaReader);
            if (errorMessages.Count > 0)
            {
                foreach (var eventMessage in errorMessages)
                {
                    Console.WriteLine(eventMessage);
                }
                Console.ReadKey();
                return;
            }
            WebcrawlerSimple webcrawler = new WebcrawlerSimple();
            PortiaResponse response = await webcrawler.StartAsync(request);
            var settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };
            File.WriteAllText(response.ProjectName + ".json", JsonConvert.SerializeObject(response, settings));
        }
    }

}
