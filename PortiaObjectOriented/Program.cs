using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using PortiaObjectOriented.Dto;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.Http;
using PuppeteerSharp;
using System.Threading.Tasks.Dataflow;

namespace PortiaObjectOriented
{
    //class Program
    //{
    //    static void Main(string[] args)
    //    {
    //        List<string> blacklistedWords = new List<string>(new string[] {});

    //        Recipe recipe = new Recipe();
    //        Type type = recipe.GetType();
    //        var result = Webcrawler.StartCrawlerAsync("https://www.arla.dk/opskrifter/", blacklistedWords, type).Result; //https://www.arla.dk/opskrifter/
    //        var list = result.ToList();
    //        var responseJson = JsonConvert.SerializeObject(list, Formatting.Indented);
    //        System.IO.File.WriteAllText("response.json", responseJson);
    //    }
    //}


    class Program
    {
        static async Task Main(string[] args)
        {
            await Run();
        }
        static async Task Run()
        {
            await Task.Run(() => DoStuffAsync());
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
        static async Task DoStuffAsync()
        {
            int ms = 1000;
            await Task.Delay(ms);
            Thread.Sleep(ms);
            Console.WriteLine("waited :" + ms + "ms");

        }
    }

}