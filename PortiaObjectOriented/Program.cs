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
        private static BlockingCollection<Uri> UriQueue = new BlockingCollection<Uri>();
        static async Task Main(string[] args)
        {
            await new Program().Run();

        }
        public async Task Run()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

            var launchOptions = new LaunchOptions { Headless = false };

            using (var browser = await Puppeteer.LaunchAsync(launchOptions))
            {

                UriQueue.Add(new Uri("https://www.arla.dk/"));
                //UriQueue.Add(new Uri("https://stackoverflow.com/"));
                //UriQueue.Add(new Uri("https://www.youtube.com/"));
                //UriQueue.Add(new Uri("https://devblogs.microsoft.com/"));

                int threadCount = 2; //Environment.ProcessorCount;
                IList<Task> taskList = new List<Task>();
                for (int i = 0; i < threadCount; i++)
                {
                    int workerId = i;
                    Task task = Task.Run(async () =>
                    {
                        await Worker(workerId, browser);
                    });
                    taskList.Add(task);
                }
                await Task.WhenAll(taskList);
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
        public static async Task Worker(int workerId, Browser browser)
        {
            await Task.Run(async () =>
                {

                    Console.WriteLine("Worker {0} is starting.", workerId);
                    foreach (var workItem in UriQueue.GetConsumingEnumerable())
                    {
                        Console.WriteLine("Worker {0} is processing uri: {1}", workerId, workItem);
                        string content = "";
                        using (var page = await browser.NewPageAsync())
                        {
                            await page.GoToAsync(workItem.ToString());
                            content = await page.GetContentAsync();


                            if (UriQueue.Count <= 0)
                            {
                                UriQueue.CompleteAdding(); // Add this to the HtmlWorker
                            }
                        }
                    }
                    Console.WriteLine("Worker {0} is stopping.", workerId);
                });

        }
    }
}