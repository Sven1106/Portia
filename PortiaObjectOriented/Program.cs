using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using PortiaObjectOriented.Dto;
using System;
using System.IO;

namespace PortiaObjectOriented
{
    class Program
    {
        static void Main(string[] args)
        {
            List<string> blacklistedWords = new List<string>(new string[] {
                "temaer",
                "leksikon"
            });

            Recipe recipe = new Recipe();
            Type type = recipe.GetType();
            var result = Webcrawler.StartCrawlerAsync("https://www.arla.dk/opskrifter/risotto-med-bacon-og-able-rosenkalstopping-/", blacklistedWords, type).Result; //https://www.arla.dk/opskrifter/
            var list = result.ToList();
            var responseJson = JsonConvert.SerializeObject(list, Formatting.Indented);
            System.IO.File.WriteAllText("response.json", responseJson);
        }
    }
}