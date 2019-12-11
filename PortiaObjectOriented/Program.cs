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

            var linkOption = new DataflowLinkOptions { PropagateCompletion = true };

            var downloader = new TransformBlock<string, string>(async x =>
            {
                string html = await GetHtmlAsync(x);
                return html;
            }, new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 3, //enforce fairness, after handling n messages the block's task will be re-schedule.
                MaxDegreeOfParallelism = 8,// by default Tpl dataflow assign a single task per block
                BoundedCapacity = 4 // the size of the block input buffer
            });

            var linkParser = new TransformManyBlock<string, string>(
                (html) =>
                {
                    var urls = new List<string>();
                    if (html != "https://www.arla.dk/opskrifter/risotto-med-bacon-og-able-rosenkalstopping-/")
                    {
                        urls.Add("https://www.arla.dk/opskrifter/risotto-med-bacon-og-able-rosenkalstopping-/");
                    }
                    return urls;
                }, new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = 2
                });

            BroadcastBlock<string> contentBroadcaster = new BroadcastBlock<string>(i =>
            {
                Console.WriteLine(i);
                return i;
            });
            var linkBroadcaster = new BroadcastBlock<string>(u =>
            {
                Console.WriteLine(u);
                return u;
            });

            downloader.LinkTo(contentBroadcaster, linkOption, html => html != null);
            contentBroadcaster.LinkTo(linkParser, linkOption);
            linkParser.LinkTo(linkBroadcaster, linkOption);
            linkBroadcaster.LinkTo(downloader, linkOption);


            await downloader.SendAsync("https://www.arla.dk/");
            Thread.Sleep(10 * 1000);
            downloader.Complete();

            await Task.WhenAll(downloader.Completion, linkParser.Completion, contentBroadcaster.Completion);
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
        static async Task<string> GetHtmlAsync(string uri)
        {
            string html = uri;
            await Task.Delay(400);
            return html;
        }
    }
}