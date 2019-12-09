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
using System.Xml.XPath;

namespace PortiaJsonOriented
{
    public class Webcrawler
    {
        private static string dequeuedUrlsFile = "dequeuedUrls.txt";
        private static string allQueuedUrlsFile = "allQueuedUrls.txt";
        private static string allSuccesfullUrlsFile = "allSuccesfullUrls.txt";
        private static BlockingCollection<Uri> queue = new BlockingCollection<Uri>();
        private static BlockingCollection<Uri> visitedUrls = new BlockingCollection<Uri>();
        private static ConcurrentDictionary<string, JArray> tasks = new ConcurrentDictionary<string, JArray>();
        private static ConcurrentDictionary<int, string> IdMessagePairs = new ConcurrentDictionary<int, string>();
        private static IList<string> disallowedStrings = new List<string>() { };
        private static Uri rootUri;
        private static int itemSuccessfullyCrawledCount = 0;
        private static int crawledUrlsCount = 0;
        private static List<DataForRequest> dataForRequest = new List<DataForRequest>();

        public Webcrawler()
        {
            #region Debugging
            File.WriteAllText(dequeuedUrlsFile, string.Empty);
            File.WriteAllText(allQueuedUrlsFile, string.Empty);
            File.WriteAllText(allSuccesfullUrlsFile, string.Empty);


            #endregion
        }
        public async Task<Core.Dtos.Response> StartCrawlerAsync(Core.Dtos.Request request)
        {
            dataForRequest = request.Data;
            disallowedStrings = request.DisallowedStrings;

            rootUri = new Uri(request.StartUrl);
            queue.TryAdd(rootUri);

            foreach (var item in dataForRequest)
            {
                tasks.TryAdd(item.TaskName, new JArray());
            }

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            //Enabled headless option
            var args = new string[] {
                "--no-sandbox",
                "--disable-plugins", "--disable-sync", "--disable-gpu", "--disable-speech-api",
                "--disable-remote-fonts", "--disable-shared-workers", "--disable-webgl", "--no-experiments",
                "--no-first-run", "--no-default-browser-check", "--no-wifi", "--no-pings", "--no-service-autorun",
                "--disable-databases", "--disable-default-apps", "--disable-demo-mode", "--disable-notifications",
                "--disable-permissions-api", "--disable-background-networking", "--disable-3d-apis",
                "--disable-bundled-ppapi-flash"
            };
            var launchOptions = new LaunchOptions { Headless = false, Args = args, IgnoreHTTPSErrors = true };
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            using (var browser = await Puppeteer.LaunchAsync(launchOptions))
            {
                int threadCount = 4;// Environment.ProcessorCount;// Use max 4 threads to crawl
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
            stopwatch.Stop();
            Console.WriteLine(stopwatch.Elapsed);

            Core.Dtos.Response response = new Core.Dtos.Response
            {
                ProjectName = request.ProjectName,
                StartUrl = request.StartUrl,
                Data = tasks
            };
            return response;
        }

        public static async Task Worker(int workerId, Browser browser)
        {
            await Task.Run(async () =>
            {
                Console.WriteLine("Worker {0} is starting.", workerId);
                foreach (var workItem in queue.GetConsumingEnumerable())
                {
                    Uri currentUrl = workItem;
                    //Console.WriteLine("Worker {0} is processing uri: {1}", workerId, currentUrl);
                    File.AppendAllText(dequeuedUrlsFile, currentUrl.ToString() + Environment.NewLine);
                    visitedUrls.TryAdd(currentUrl);
                    Interlocked.Increment(ref crawledUrlsCount);

                    string html = await GetWithPuppeteer(browser, currentUrl);
                    HtmlDocument htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(html);
                    AddNewUrlsToQueue(disallowedStrings, rootUri, ref queue, visitedUrls, htmlDoc);
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
                            Metadata metadata = new Metadata(currentUrl.ToString(), DateTime.UtcNow);
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
                        File.AppendAllText(allSuccesfullUrlsFile, currentUrl.ToString() + Environment.NewLine);
                        Interlocked.Increment(ref itemSuccessfullyCrawledCount);
                    }
                    Console.Write("\rUrls in queue: {0} - Urls visited: {1} - Items successfully crawled: {2}", queue.Count, crawledUrlsCount, itemSuccessfullyCrawledCount);

                    if (queue.Count <= 0)
                    {
                        queue.CompleteAdding(); // Add this to the HtmlWorker
                    }
                }
                Console.WriteLine("\rWorker {0} is stopping.", workerId);
            });

        }

        public static async Task WorkerMessage()
        {
            while (true)
            {
                foreach (var item in IdMessagePairs)
                {
                    Console.WriteLine(item.Value);
                }
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
        private static void AddNewUrlsToQueue(IList<string> disallowedStrings, Uri rootUri, ref BlockingCollection<Uri> queue, BlockingCollection<Uri> visitedUrls, HtmlDocument htmlDoc)
        {
            var aTags = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
            if (aTags != null)
            {
                foreach (var aTag in aTags)
                {
                    string hrefValue = aTag.Attributes["href"].Value;
                    Uri url = new Uri(hrefValue, UriKind.RelativeOrAbsolute);
                    url = new Uri(rootUri, url);
                    if (url.OriginalString.Contains(rootUri.OriginalString) == true)
                    {
                        if (queue.Contains(url) == false && visitedUrls.Contains(url) == false)
                        {
                            if (ContainsAnyWords(url, disallowedStrings))
                            {
                                continue;
                            }
                            queue.TryAdd(url);
                            File.AppendAllText(allQueuedUrlsFile, url.ToString() + Environment.NewLine);
                        }
                    }
                }
            }
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

        private static async Task<string> GetWithPuppeteer(Browser browser, Uri uri)
        {
            string content = "";
            using (var page = await browser.NewPageAsync())
            {
                await page.SetRequestInterceptionAsync(true);

                page.Request += Page_Request;
                await page.GoToAsync(uri.ToString());
                content = await page.GetContentAsync();
            }
            return content;
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
                    case ResourceType.Script:
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
