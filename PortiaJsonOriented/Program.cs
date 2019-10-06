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

namespace PortiaJsonOriented
{
    class Program
    {
        static void Main(string[] args)
        {
            //MainAsync().Wait();
            Start();
        }


        //static async Task MainAsync()
        //{
        //    var request = File.ReadAllText("Request.json");
        //    var requestSchema = File.ReadAllText(Path.Combine("Core","RequestSchema.json"));
        //    JSchema schema = JSchema.Parse(requestSchema);
        //    JObject requestAsJson = JObject.Parse(request);
        //    //bool isValid = requestAsJson.IsValid(schema);
        //    //var response = await Webcrawler.StartCrawlerAsync(requestAsJson.ToString());
        //}

        //#RULES#
        // properties can be basic types(string, number, boolean), complex types(object)
        // and maybe later custom types(date, rawHtml)?
        // properties of any basic type will return a value.
        // properties of the type object cant return a value, but they have attributes which can be of basic types.
        // all properties are lists if isMultiple == true

        // create mapping template.
        static void Start()
        {
            var example1 = File.ReadAllText("example1.json");
            string response = Webcrawler.StartCrawlerAsync(example1).Result;
        }

    }

}
