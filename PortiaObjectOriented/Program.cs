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
        private static BlockingCollection<Uri> UriQueue = new BlockingCollection<Uri>();
        private static readonly BufferBlock<Uri> _queue = new BufferBlock<Uri>();

        private BufferBlock<double> consumerQueue = new BufferBlock<double>();
        private BufferBlock<double> producerQueue = new BufferBlock<double>();
        static async Task Main(string[] args)
        {
            await new Program().Run();

        }
        async Task Run()
        {
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            string myMessage = "Hello World";
            Task.Factory.StartNew((state) =>
            {
                Thread.Sleep(2000);
                token.ThrowIfCancellationRequested();
                Console.WriteLine("Is Background thread: {0}", Thread.CurrentThread.IsBackground);
                Console.WriteLine("Is threadpool thread: {0}", Thread.CurrentThread.IsThreadPoolThread);
                Console.WriteLine(myMessage);

            }, token).Wait();
            Console.WriteLine("Press any key to exit.");
            
            Console.ReadKey();
        }
        public void DoStuff()
        {
            Thread.Sleep(5000);

        }
        public static async Task Worker(int workerId, Browser browser)
        {
            await Task.Run(async () =>
                {
                    Console.WriteLine("Worker {0} is starting.", workerId);
                    while (await _queue.OutputAvailableAsync())
                    {
                        var item = await _queue.ReceiveAsync();
                        Console.WriteLine("Worker {0} is processing uri: {1}", workerId, item);
                        string content = "";
                        using (var page = await browser.NewPageAsync())
                        {
                            await page.GoToAsync(item.ToString());
                            content = await page.GetContentAsync();
                        }

                    }

                    //foreach (var workItem in UriQueue.GetConsumingEnumerable())
                    //{
                    //    Console.WriteLine("Worker {0} is processing uri: {1}", workerId, workItem);
                    //    string content = "";
                    //    using (var page = await browser.NewPageAsync())
                    //    {
                    //        await page.GoToAsync(workItem.ToString());
                    //        content = await page.GetContentAsync();
                    //        await Task.Delay(1000);// DO STUFF
                    //    }
                    //    if (UriQueue.Count <= 0)
                    //    {
                    //        UriQueue.CompleteAdding(); // Add this to the HtmlWorker
                    //    }
                    //}
                    Console.WriteLine("Worker {0} is stopping.", workerId);
                });

        }
    }

}