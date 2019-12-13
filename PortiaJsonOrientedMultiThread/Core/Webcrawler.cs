using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PortiaJsonOrientedMultiThread.Core.Models;
using PuppeteerSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml.XPath;

namespace PortiaJsonOrientedMultiThread
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

        public UrlHtmlPair()
        {
        }

    }
    public class Webcrawler
    {
        private static string dequeuedUrlsFile = "dequeuedUrls.txt";
        private static string allQueuedUrlsFile = "allQueuedUrls.txt";
        private static string allSuccesfullUrlsFile = "allSuccesfullUrls.txt";
        private static Uri rootUrl;
        private static List<DataForRequest> dataForRequest = new List<DataForRequest>();
        private static IList<string> disallowedStrings = new List<string>() { };
        private static ConcurrentDictionary<string, JArray> tasks = new ConcurrentDictionary<string, JArray>();
        private static BlockingCollection<Uri> legalUrls = new BlockingCollection<Uri>();
        private static BlockingCollection<Uri> visitedUrls = new BlockingCollection<Uri>();

        private Browser browser;
        private TransformBlock<Uri, UrlHtmlPair> htmlDownloader;
        private TransformManyBlock<UrlHtmlPair, Uri> urlParser;
        private ActionBlock<UrlHtmlPair> objParser;

        private BroadcastBlock<UrlHtmlPair> htmlContentBroadcaster;
        private BroadcastBlock<Uri> legalUrlBroadcaster;
        private BroadcastBlock<Uri> urlBroadcaster;
        private ITargetBlock<Uri> urlGarbage;
        private ITargetBlock<UrlHtmlPair> urlHtmlPairGarbage;
        private int timeOutSeconds = 10;
        public Webcrawler()
        {
            #region Debugging
            File.WriteAllText(dequeuedUrlsFile, string.Empty);
            File.WriteAllText(allQueuedUrlsFile, string.Empty);
            File.WriteAllText(allSuccesfullUrlsFile, string.Empty);


            #endregion
        }

        public void KillProcessesBasedOnExecutablePath(string executablePath)
        {
            List<int> chromiumIds = new List<int>();
            string wmiQueryString = @"SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE ExecutablePath LIKE '" + executablePath + "'";
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            {
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        if (item != null)
                        {
                            var processId = Convert.ToInt32(item["ProcessId"]);
                            chromiumIds.Add(processId);
                        }
                    }
                }
            }
            var processes = Process.GetProcesses().Where(p => chromiumIds.Where(x => x == p.Id).Any()).ToList();
            if (processes.Count > 0)
            {
                // Is running
                processes.ForEach((x) =>
                {
                    x.Kill();
                });
            }
        }
        public async Task CreateBlocks()
        {
            var puppeteerChromiumExecutablePath = new BrowserFetcher().GetExecutablePath(BrowserFetcher.DefaultRevision).Replace(@"\", @"\\");
            KillProcessesBasedOnExecutablePath(puppeteerChromiumExecutablePath);

            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var args = new string[] {
                "--no-sandbox",
                "--disable-plugins", "--disable-sync", "--disable-gpu", "--disable-speech-api",
                "--disable-remote-fonts", "--disable-shared-workers", "--disable-webgl", "--no-experiments",
                "--no-first-run", "--no-default-browser-check", "--no-wifi", "--no-pings", "--no-service-autorun",
                "--disable-databases", "--disable-default-apps", "--disable-demo-mode", "--disable-notifications",
                "--disable-permissions-api", "--disable-background-networking", "--disable-3d-apis",
                "--disable-bundled-ppapi-flash"
            };
            var launchOptions = new LaunchOptions { Headless = true, Args = args, IgnoreHTTPSErrors = true };
            browser = await Puppeteer.LaunchAsync(launchOptions);



            var htmlDownloaderOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 5, // the size of the block input buffer
                MaxDegreeOfParallelism = 3, // by default Tpl dataflow assign a single task per block
                MaxMessagesPerTask = 2 //enforce fairness, after handling n messages the block's task will be re-schedule.

            };
            var parserOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 30,
                MaxDegreeOfParallelism = 10,    //Get the maximum number of messages that the block can process simultaneously.
                MaxMessagesPerTask = 2          //Get the maximum number of messages that can be processed per task.
            };

            urlGarbage = DataflowBlock.NullTarget<Uri>();
            urlHtmlPairGarbage = DataflowBlock.NullTarget<UrlHtmlPair>();


            urlBroadcaster = new BroadcastBlock<Uri>(url => url);
            legalUrlBroadcaster = new BroadcastBlock<Uri>(url => url);
            htmlDownloader = new TransformBlock<Uri, UrlHtmlPair>(
                async url =>
                {
                    UrlHtmlPair urlHtmlPair = await GetUrlHtmlPairAsync(url, browser);
                    visitedUrls.TryAdd(url);
                    return urlHtmlPair;
                }
                , htmlDownloaderOptions);

            htmlContentBroadcaster = new BroadcastBlock<UrlHtmlPair>(urlHtml => urlHtml);

            objParser = new ActionBlock<UrlHtmlPair>(
                urlHtmlPair => ObjParser(urlHtmlPair), new ExecutionDataflowBlockOptions());

            urlParser = new TransformManyBlock<UrlHtmlPair, Uri>(
                urlHtmlPair => ParseUrls(urlHtmlPair.Html), new ExecutionDataflowBlockOptions());

        }

        public void ConfigureBlocks()
        {
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            Predicate<Uri> urlFilter = url =>
            {
                if (IsLegalUrl(url) == false)
                {
                    return false;
                }
                legalUrls.TryAdd(url);
                return true;
            };


            urlBroadcaster.LinkTo(legalUrlBroadcaster, linkOptions, urlFilter);
            legalUrlBroadcaster.LinkTo(htmlDownloader, linkOptions);
            htmlDownloader.LinkTo(htmlContentBroadcaster, linkOptions);
            //htmlDownloader.LinkTo(urlHtmlPairGarbage, linkOptions);
            htmlContentBroadcaster.LinkTo(objParser, linkOptions);
            htmlContentBroadcaster.LinkTo(urlParser, linkOptions);
            urlParser.LinkTo(urlBroadcaster, linkOptions);
            //urlParser.LinkTo(urlGarbage, linkOptions);

        }
        public async Task Render()
        {
            while (!htmlDownloader.Completion.IsCompleted)
            {
                string format = $"\nlegal urls:                 {legalUrls.Count}    " +
                                $"\nurls visited:               {visitedUrls.Count}    " +
                                $"\nurlBroadcaster:         is completed = {urlBroadcaster.Completion.IsCompleted}    " +
                                $"\nlegalUrlBroadcaster:    is completed = {legalUrlBroadcaster.Completion.IsCompleted}    " +
                                $"\nhtmlContentBroadcaster: is completed = {htmlContentBroadcaster.Completion.IsCompleted}    " +
                                $"\nhtmlDownloader:         is completed = {htmlDownloader.Completion.IsCompleted}, input={htmlDownloader.InputCount}, output={htmlDownloader.OutputCount}    " +
                                $"\nurlParser:              is completed = {urlParser.Completion.IsCompleted}, input={urlParser.InputCount}, output={urlParser.OutputCount}    " +
                                $"\nobjParser:              is completed = {objParser.Completion.IsCompleted}, input={objParser.InputCount}    ";
                ConsoleColor color = ConsoleColor.Yellow;
                Console.SetCursorPosition(0, 0);
                WriteToConsole(color, format);
                await Task.Delay(33);
            }
        }
        public async Task Monitor()
        {
            while (!htmlDownloader.Completion.IsCompleted)
            {
                await Task.Delay(timeOutSeconds * 1000); // Is in the beginning to insure the dataflow is running.
                if (legalUrls.Count == visitedUrls.Count
                    && urlParser.InputCount == 0 && urlParser.OutputCount == 0
                    && htmlDownloader.InputCount == 0 && htmlDownloader.OutputCount == 0
                    && objParser.InputCount == 0)
                {
                    htmlDownloader.Complete();
                }
            }
        }
        public async Task<Core.Dtos.Response> StartCrawlerAsync(Core.Dtos.Request request)
        {
            rootUrl = new Uri(request.StartUrl);
            dataForRequest = request.Data;
            disallowedStrings = request.DisallowedStrings;

            foreach (var item in dataForRequest)
            {
                tasks.TryAdd(item.TaskName, new JArray());
            }

            await CreateBlocks();
            ConfigureBlocks();


            Console.WriteLine("Starting");
            await urlBroadcaster.SendAsync(rootUrl);
            Task monitor = Monitor();
            Task render = Render();
            await Task.WhenAll(render, monitor, urlBroadcaster.Completion);
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
            string html;
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
        private static void WriteToConsole(ConsoleColor color, string format)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(format);
            Console.ResetColor();
        }

    }
}
