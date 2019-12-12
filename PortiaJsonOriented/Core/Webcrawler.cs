using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PortiaJsonOriented.Core.Models;
using PuppeteerSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml.XPath;

namespace PortiaJsonOriented
{
    class UrlHtmlPair
    {
        public Uri Url { get; set; }
        public string Html { get; set; }
        public UrlHtmlPair(Uri url, string html)
        {
            Url = url;
            Html = html;
        }

    }
    public class Webcrawler
    {
        CancellationTokenSource cts;
        CancellationToken ct;

        private static string dequeuedUrlsFile = "dequeuedUrls.txt";
        private static string allQueuedUrlsFile = "allQueuedUrls.txt";
        private static string allSuccesfullUrlsFile = "allSuccesfullUrls.txt";
        private static Uri rootUrl;
        private static List<DataForRequest> dataForRequest = new List<DataForRequest>();
        private static IList<string> disallowedStrings = new List<string>() { };
        private static ConcurrentDictionary<string, JArray> tasks = new ConcurrentDictionary<string, JArray>();
        private static BlockingCollection<Uri> legalUrls = new BlockingCollection<Uri>();

        public Webcrawler()
        {
            cts = new CancellationTokenSource();
            ct = cts.Token;
            #region Debugging
            File.WriteAllText(dequeuedUrlsFile, string.Empty);
            File.WriteAllText(allQueuedUrlsFile, string.Empty);
            File.WriteAllText(allSuccesfullUrlsFile, string.Empty);


            #endregion
        }

        public async Task<Core.Dtos.Response> StartCrawlerAsync(Core.Dtos.Request request)
        {
            rootUrl = new Uri(request.StartUrl);
            dataForRequest = request.Data;
            disallowedStrings = request.DisallowedStrings;
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
            });
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            var htmlDownloaderOptions = new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 3, //enforce fairness, after handling n messages the block's task will be re-schedule.
                MaxDegreeOfParallelism = 2,// by default Tpl dataflow assign a single task per block
                BoundedCapacity = 8, // the size of the block input buffer
                CancellationToken = ct
            };
            var parserOptions = new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 3,
                MaxDegreeOfParallelism = 8,
                CancellationToken = ct
            };

            Predicate<Uri> urlFilter = url =>
            {
                if (IsLegalUrl(url) == false)
                {
                    return false;
                }
                legalUrls.TryAdd(url);
                return true;
            };

            TransformBlock<Uri, UrlHtmlPair> htmlDownloader = new TransformBlock<Uri, UrlHtmlPair>(
                async url => await GetUrlHtmlPairAsync(url, browser), htmlDownloaderOptions);
            TransformManyBlock<UrlHtmlPair, Uri> urlParser = new TransformManyBlock<UrlHtmlPair, Uri>(
                urlHtmlPair => ParseUrls(urlHtmlPair.Html), parserOptions);
            ActionBlock<UrlHtmlPair> objParser = new ActionBlock<UrlHtmlPair>(
                urlHtmlPair =>
                {
                    ObjParser(urlHtmlPair);
                }, parserOptions);




            BroadcastBlock<UrlHtmlPair> htmlContentBroadcaster = new BroadcastBlock<UrlHtmlPair>(urlHtml => urlHtml);
            BroadcastBlock<Uri> legalUrlBroadcaster = new BroadcastBlock<Uri>(url =>
            {
                Console.WriteLine("legalUrlBroadcaster cloned: {0}", url);
                return url;
            });
            BroadcastBlock<Uri> urlBroadcaster = new BroadcastBlock<Uri>(url =>
            {
                Console.WriteLine("urlBroadcaster cloned: {0}", url);
                return url;
            });
            urlBroadcaster.LinkTo(legalUrlBroadcaster, linkOptions, urlFilter);
            legalUrlBroadcaster.LinkTo(htmlDownloader, linkOptions);
            htmlDownloader.LinkTo(htmlContentBroadcaster, linkOptions);
            htmlContentBroadcaster.LinkTo(objParser, linkOptions);
            htmlContentBroadcaster.LinkTo(urlParser, linkOptions);
            urlParser.LinkTo(urlBroadcaster, linkOptions);



            foreach (var item in dataForRequest)
            {
                tasks.TryAdd(item.TaskName, new JArray());
            }


            Console.WriteLine("Starting");
            await urlBroadcaster.SendAsync(rootUrl);
            //await Task.Delay(5000);
            await Task.WhenAll(legalUrlBroadcaster.Completion);
            await browser.CloseAsync();
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();


            Core.Dtos.Response response = new Core.Dtos.Response
            {
                ProjectName = request.ProjectName,
                StartUrl = request.StartUrl,
                Data = tasks
            };
            return response;
        }

        static async Task<UrlHtmlPair> GetUrlHtmlPairAsync(Uri url, Browser browser)
        {
            string html = "";
            using (var page = await browser.NewPageAsync())
            {
                await page.SetRequestInterceptionAsync(true);
                page.Request += Page_Request;
                await page.GoToAsync(url.ToString());
                html = await page.GetContentAsync();
            }
            return new UrlHtmlPair(url, html);
        }

        static List<Uri> ParseUrls(string html)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            var aTags = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
            List<Uri> newUrls = new List<Uri>();
            if (aTags != null)
            {
                foreach (var aTag in aTags)
                {
                    string hrefValue = aTag.Attributes["href"].Value;
                    Uri url = new Uri(hrefValue, UriKind.RelativeOrAbsolute);
                    url = new Uri(rootUrl, url);
                    newUrls.Add(url);
                }
            }
            return newUrls;
        }

        static void ObjParser(UrlHtmlPair urlHtmlPair)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(urlHtmlPair.Html);
            HtmlNode documentNode = htmlDoc.DocumentNode;
            foreach (DataForRequest task in dataForRequest)
            {
                JObject taskObject = new JObject();
                foreach (NodeAttribute item in task.Items)
                {
                    JToken value = GetValueForJTokenRecursive(item, documentNode);
                    if (value.ToString() == "")
                    {
                        continue;
                    }
                    taskObject.Add(item.Name, value);
                    Metadata metadata = new Metadata(urlHtmlPair.Url.ToString(), DateTime.UtcNow);
                    taskObject.Add("metadata", JObject.FromObject(metadata, new JsonSerializer()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }));

                }
                if (taskObject.HasValues == false)
                {
                    continue;
                }
                tasks[task.TaskName].Add(taskObject);
                // File.AppendAllText(allSuccesfullUrlsFile, currentUrl.ToString() + Environment.NewLine);
                //Interlocked.Increment(ref itemSuccessfullyCrawledCount);
            }
        }

        private static JToken GetValueForJTokenRecursive(NodeAttribute node, HtmlNode htmlNode)
        {
            JToken jToken = "";
            if (node.GetMultipleFromPage) // TODO
            {
                JArray jArray = new JArray();
                if (node.Type.ToLower() == "string" || node.Type.ToLower() == "number" || node.Type.ToLower() == "boolean") // basic types
                {
                    HtmlNodeCollection elements = htmlNode.SelectNodes(node.Xpath);
                    if (elements != null)
                    {
                        foreach (var element in elements)
                        {
                            HtmlNodeNavigator navigator = (HtmlNodeNavigator)element.CreateNavigator();
                            jArray.Add(navigator.Value.Trim());
                        }
                        jToken = jArray;
                    }
                }
                else if (node.Type.ToLower() == "object" && node.Attributes.Count > 0) // complex types
                {
                    JObject jObject = new JObject();
                    HtmlNodeCollection elements = htmlNode.SelectNodes(node.Xpath);
                    if (elements != null)
                    {
                        foreach (var element in elements)
                        {
                            foreach (var attribute in node.Attributes)
                            {
                                JToken value = GetValueForJTokenRecursive(attribute, element);
                                if (value.ToString() == "" && attribute.IsRequired)
                                {
                                    return jToken;
                                }
                                jObject.Add(attribute.Name, value);
                            }
                            jArray.Add(jObject);
                        }
                        jToken = jArray;
                    }
                }
            }
            else
            {
                HtmlNodeNavigator navigator = (HtmlNodeNavigator)htmlNode.CreateNavigator();
                if (node.Type.ToLower() == "string" || node.Type.ToLower() == "number" || node.Type.ToLower() == "boolean") // basic types
                {
                    XPathNavigator nodeFound = navigator.SelectSingleNode(node.Xpath);
                    // Get as Type
                    if (nodeFound != null)
                    {
                        jToken = nodeFound.Value.Trim();
                    }
                }
                else if (node.Type.ToLower() == "object" && node.Attributes.Count > 0) // complex types
                {
                    HtmlNode element = htmlNode.SelectSingleNode(node.Xpath);
                    if (element != null)
                    {
                        JObject jObject = new JObject();
                        foreach (var attribute in node.Attributes)
                        {
                            JToken value = GetValueForJTokenRecursive(attribute, element);
                            if (value.ToString() == "" && attribute.IsRequired)
                            {
                                return jToken;
                            }
                            jObject.Add(attribute.Name, value);
                        }
                        jToken = jObject;
                    }
                }
            }
            return jToken;
        }

        private static bool IsLegalUrl(Uri url)
        {
            if (url.OriginalString.Contains(rootUrl.OriginalString) == false ||
                legalUrls.Contains(url) == true ||
                ContainsAnyWords(url, disallowedStrings) == true)
            {
                return false;
            }
            return true;
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
        private static async void Page_Request(object sender, RequestEventArgs e)
        {
            try
            {
                switch (e.Request.ResourceType)
                {

                    case ResourceType.Media:
                    case ResourceType.StyleSheet:
                    case ResourceType.Image:
                    case ResourceType.Unknown:
                    case ResourceType.Font:
                    case ResourceType.TextTrack:
                    case ResourceType.Xhr:
                    case ResourceType.Fetch:
                    case ResourceType.EventSource:
                    case ResourceType.WebSocket:
                    case ResourceType.Manifest:
                    case ResourceType.Ping:
                    case ResourceType.Other:
                        await e.Request.AbortAsync();
                        break;
                    case ResourceType.Document:
                    case ResourceType.Script:
                    default:
                        await e.Request.ContinueAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error => {ex.Message}");
                await e.Request.ContinueAsync();
            }
        }


    }
}
