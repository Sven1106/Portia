using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using PortiaObjectOriented.Dto;

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

            var result = Webcrawler.StartCrawlerAsync<Recipe>("https://www.arla.dk/opskrifter/", blacklistedWords).Result; //https://www.arla.dk/opskrifter/
            var list = result.Cast<Recipe>().ToList();
            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            System.IO.File.WriteAllText("result.json", json);
        }
    }
}