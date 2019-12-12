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
using HtmlAgilityPack;

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
        private static Uri rootUri;
        private static BlockingCollection<Uri> visitedUrls = new BlockingCollection<Uri>();
        static async Task Main(string[] args)
        {
            await RunAsync();
        }
        static async Task RunAsync()
        {
            rootUri = new Uri("https://www.arla.dk/");
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
            });


            DataflowLinkOptions linkOption = new DataflowLinkOptions { PropagateCompletion = true };
            var downloaderOptions = new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 3, //enforce fairness, after handling n messages the block's task will be re-schedule.
                MaxDegreeOfParallelism = 8,// by default Tpl dataflow assign a single task per block
                BoundedCapacity = 4 // the size of the block input buffer
            };

            var linkParserOptions = new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 2
            };
            TransformBlock<Uri, string> htmlDownloader = new TransformBlock<Uri, string>(
                async uri => await GetHtmlAsync(uri, browser), downloaderOptions);

            TransformManyBlock<string, Uri> urlParser = new TransformManyBlock<string, Uri>(
                html => ParseUris(html), linkParserOptions);

            BroadcastBlock<string> htmlBroadcaster = new BroadcastBlock<string>(i => i);
            BroadcastBlock<Uri> urlBroadcaster = new BroadcastBlock<Uri>(u =>
            {
                Console.WriteLine("LinkBroadcaster cloned: {0}", u);
                visitedUrls.Add(u);
                return u;
            });

            urlBroadcaster.LinkTo(htmlDownloader, linkOption);
            //urlBroadcaster.LinkTo(visitedUrls, linkOption);
            htmlDownloader.LinkTo(htmlBroadcaster, linkOption);
            htmlBroadcaster.LinkTo(urlParser, linkOption);
            urlParser.LinkTo(urlBroadcaster, linkOption);

            Console.WriteLine("Starting");
            await urlBroadcaster.SendAsync(rootUri);
            await Task.Delay(2000);
            await Task.WhenAll(htmlDownloader.Completion, urlParser.Completion);
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
        static async Task<string> GetHtmlAsync(Uri uri, Browser browser)
        {
            string html;
            using (var page = await browser.NewPageAsync())
            {
                await page.GoToAsync(uri.ToString());
                html = await page.GetContentAsync();
            }
            return html;
        }

        static List<Uri> ParseUris(string html)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var aTags = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
            List<Uri> newUris = new List<Uri>();
            if (aTags != null)
            {
                foreach (var aTag in aTags)
                {
                    string hrefValue = aTag.Attributes["href"].Value;
                    Uri url = new Uri(hrefValue, UriKind.RelativeOrAbsolute);
                    url = new Uri(rootUri, url);
                    if (url.OriginalString.Contains(rootUri.OriginalString) == true)
                    {
                        if (visitedUrls.Contains(url) == false)
                        {
                            newUris.Add(url);
                        }
                    }
                }
            }

            // Tilføj Action som hele tiden håndterer visitedUrls bagved
            newUris = newUris.Distinct().ToList();
            return newUris;
        }

        private static bool ContainsAnyWords(Uri url, IList<string> words)
        {
            foreach (var word in words)
            {
                if (url.ToString().Contains(word))
                {
                    return true;
                }
            }
            return false;
        }
    }
}