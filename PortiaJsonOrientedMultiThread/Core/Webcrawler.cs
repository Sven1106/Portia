using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml.XPath;
using PuppeteerSharp;
using PortiaJsonOrientedMultiThread.Core.Models;
using Task = System.Threading.Tasks.Task;
using PortiaTask = PortiaJsonOrientedMultiThread.Core.Models.Task;
using PortiaResponse = PortiaJsonOrientedMultiThread.Core.Dtos.Response;
using PortiaRequest = PortiaJsonOrientedMultiThread.Core.Dtos.Request;
using System.Text;
using PortiaJsonOrientedMultiThread.Core;

namespace PortiaJsonOrientedMultiThread
{




    public class Webcrawler
    {
        private static Uri rootUrl;
        private static List<PortiaTask> tasksForRequest = new List<PortiaTask>();
        private static IList<string> disallowedStrings = new List<string>() { };
        private static ConcurrentDictionary<string, JArray> dataByTask = new ConcurrentDictionary<string, JArray>();
        private static BlockingCollection<Uri> legalUrlsQueue = new BlockingCollection<Uri>();
        private static BlockingCollection<Uri> visitedUrlsQueue = new BlockingCollection<Uri>();

        private CrashDump crashDump;
        private Browser browser;
        private TransformBlock<Uri, UrlHtmlPair> htmlDownloader;
        private TransformManyBlock<UrlHtmlPair, Uri> urlParser;
        private TransformManyBlock<UrlHtmlPair, Uri> siteMapParser;
        private ActionBlock<UrlHtmlPair> objParser;

        private BroadcastBlock<Uri> urlBroadcaster;
        private BroadcastBlock<Uri> legalUrlBroadcaster;
        private BroadcastBlock<UrlHtmlPair> htmlContentBroadcaster;

        public void KillPuppeteerChromiumProcesesses()
        {
            var puppeteerExecutablePath = new BrowserFetcher().GetExecutablePath(BrowserFetcher.DefaultRevision).Replace(@"\", @"\\");
            List<int> processIds = new List<int>();
            string wmiQueryString = @"SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE ExecutablePath LIKE '" + puppeteerExecutablePath + "'";
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            {
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        if (item != null)
                        {
                            var processId = Convert.ToInt32(item["ProcessId"]);
                            processIds.Add(processId);
                        }
                    }
                }
            }
            List<Process> processes = Process.GetProcesses().Where(p => processIds.Where(x => x == p.Id).Any()).ToList();
            if (processes.Count > 0)// Is running
            {

                processes.ForEach((x) =>
                {
                    x.Kill();
                });
            }
        }
        public async Task CreateBlocks()
        {
            #region init Puppeteer
            KillPuppeteerChromiumProcesesses();
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
            #endregion
            var htmlDownloaderOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = -1, // the size of the block input buffer
                MaxDegreeOfParallelism = 3, // by default Tpl dataflow assign a single task per block
                MaxMessagesPerTask = 2, //enforce fairness, after handling n messages the block's task will be re-schedule.
                EnsureOrdered = false
            };

            urlBroadcaster = new BroadcastBlock<Uri>(url =>
            {
                return url;
            });
            legalUrlBroadcaster = new BroadcastBlock<Uri>(url =>
            {
                legalUrlsQueue.TryAdd(url);
                return url;
            });
            htmlDownloader = new TransformBlock<Uri, UrlHtmlPair>(
                async url =>
                {
                    UrlHtmlPair urlHtmlPair = await GetUrlHtmlPairAsync(url, browser);
                    visitedUrlsQueue.TryAdd(url);
                    return urlHtmlPair;
                },
                htmlDownloaderOptions);

            htmlContentBroadcaster = new BroadcastBlock<UrlHtmlPair>(urlHtml => urlHtml);
            objParser = new ActionBlock<UrlHtmlPair>(urlHtmlPair => ParseObjs(urlHtmlPair));
            urlParser = new TransformManyBlock<UrlHtmlPair, Uri>(urlHtmlPair =>
            {
                var newUrls = ParseHtmlToUrls(urlHtmlPair.Html);
                return newUrls;
            });
            siteMapParser = new TransformManyBlock<UrlHtmlPair, Uri>(urlHtmlPair =>
            {
                var newUrls = ParseHtmlToUrls(urlHtmlPair.Html);
                return newUrls;
            });

        }
        public void ConfigureBlocksForCircularFlow()
        {
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            Predicate<Uri> legalUrlFilter = url =>
            {
                if (IsUrlLegal(url) == false)
                {
                    return false;
                }
                return true;
            };

            legalUrlBroadcaster.LinkTo(htmlDownloader, linkOptions);
            htmlDownloader.LinkTo(htmlContentBroadcaster, linkOptions);
            htmlContentBroadcaster.LinkTo(objParser, linkOptions);
            htmlContentBroadcaster.LinkTo(urlParser, linkOptions);
            urlParser.LinkTo(urlBroadcaster, linkOptions);
            urlBroadcaster.LinkTo(legalUrlBroadcaster, linkOptions, legalUrlFilter);
        }

        public void ConfigureBlocksForLinearFlow()
        {
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            legalUrlBroadcaster.LinkTo(htmlDownloader, linkOptions);
            htmlDownloader.LinkTo(htmlContentBroadcaster, linkOptions);
            htmlContentBroadcaster.LinkTo(objParser, linkOptions);
        }

        public async Task Render(int fps = 30)
        {
            while (!legalUrlBroadcaster.Completion.IsCompleted)
            {
                string format = $"\nlegal urls:                          = {legalUrlsQueue.Count}    " +
                                $"\nurls visited:                        = {visitedUrlsQueue.Count}    " +
                                $"\nurlBroadcaster:         is completed = {urlBroadcaster.Completion.IsCompleted}    " +
                                $"\nlegalUrlBroadcaster:    is completed = {legalUrlBroadcaster.Completion.IsCompleted}    " +
                                $"\nhtmlContentBroadcaster: is completed = {htmlContentBroadcaster.Completion.IsCompleted}    " +
                                $"\nhtmlDownloader:         is completed = {htmlDownloader.Completion.IsCompleted}, input={htmlDownloader.InputCount}, output={htmlDownloader.OutputCount}    " +
                                $"\nurlParser:              is completed = {urlParser.Completion.IsCompleted}, input={urlParser.InputCount}, output={urlParser.OutputCount}    " +
                                $"\nobjParser:              is completed = {objParser.Completion.IsCompleted}, input={objParser.InputCount}    ";
                ConsoleColor color = ConsoleColor.Yellow;
                Console.SetCursorPosition(0, 0);
                WriteToConsole(color, format);
                await Task.Delay(1000 / fps);
            }
        }


        public async Task Monitor()
        {
            int secondIntervalToCheck = 5;
            int maxTimeoutCount = 6;
            int currentTimeOutCount = 0;
            int currentLegalUrlsCount = 0;
            int currentVisitedUrlsCount = 0;

            while (!legalUrlBroadcaster.Completion.IsCompleted)
            {
                bool nothingInTransformBlocks = htmlDownloader.InputCount == 0 && htmlDownloader.OutputCount == 0
                    && urlParser.InputCount == 0 && urlParser.OutputCount == 0
                    && objParser.InputCount == 0;
                bool noChangesInQueue = currentLegalUrlsCount == legalUrlsQueue.Count && currentVisitedUrlsCount == visitedUrlsQueue.Count;
                bool isTimedOut = nothingInTransformBlocks && noChangesInQueue;
                if (isTimedOut)
                {
                    currentTimeOutCount++;
                }
                else
                {
                    currentTimeOutCount = 0;
                }
                bool maxTimeoutHit = currentTimeOutCount >= maxTimeoutCount;
                if (maxTimeoutHit) //TIMED OUT
                {
                    Dictionary<string, string> jsonByVariableName = new Dictionary<string, string>();
                    jsonByVariableName.Add(nameof(legalUrlsQueue), JsonConvert.SerializeObject(legalUrlsQueue));
                    jsonByVariableName.Add(nameof(dataByTask), JsonConvert.SerializeObject(dataByTask));
                    jsonByVariableName.Add(nameof(visitedUrlsQueue), JsonConvert.SerializeObject(visitedUrlsQueue));
                    await crashDump.CreateDumpFiles(jsonByVariableName);
                    KillPuppeteerChromiumProcesesses();
                    legalUrlBroadcaster.Complete();
                }

                bool isUrlCountEqual = legalUrlsQueue.Count == visitedUrlsQueue.Count;
                if (isUrlCountEqual && nothingInTransformBlocks)
                {
                    legalUrlBroadcaster.Complete();
                }
                currentLegalUrlsCount = legalUrlsQueue.Count;
                currentVisitedUrlsCount = visitedUrlsQueue.Count;
                await Task.Delay(secondIntervalToCheck * 1000);
            }
        }

        public async Task<PortiaResponse> StartCrawlerAsync(PortiaRequest request)
        {
            Console.WriteLine("Starting");
            rootUrl = new Uri(request.StartUrl);
            tasksForRequest = request.Tasks;
            disallowedStrings = request.DisallowedStrings;

            foreach (PortiaTask task in tasksForRequest)
            {
                dataByTask.TryAdd(task.TaskName, new JArray());
            }
            await CreateBlocks();
            bool isRootUrlASiteMap = rootUrl.OriginalString.Contains("sitemap");
            if (isRootUrlASiteMap)
            {
                ConfigureBlocksForLinearFlow();
            }
            else
            {
                ConfigureBlocksForCircularFlow();
            }

            crashDump = new CrashDump("GUID");
            var allUnfinishedWork = await crashDump.AnyCrashDump();
            if (allUnfinishedWork.Count() != 0)
            {
                dataByTask = JsonConvert.DeserializeObject<ConcurrentDictionary<string, JArray>>(allUnfinishedWork[nameof(dataByTask)]);

                var legalUrls = JsonConvert.DeserializeObject<ICollection<Uri>>(allUnfinishedWork[nameof(legalUrlsQueue)]).ToList();
                legalUrls.ForEach(lu => legalUrlsQueue.TryAdd(lu));

                var visitedUrls = JsonConvert.DeserializeObject<ICollection<Uri>>(allUnfinishedWork[nameof(visitedUrlsQueue)]).ToList();
                visitedUrls.ForEach(vu => visitedUrlsQueue.TryAdd(vu));

                var unfinishedUrls = legalUrls.Except(visitedUrls).ToList();
                unfinishedUrls.ForEach(async (uu) => await legalUrlBroadcaster.SendAsync(uu));
            }
            else
            {
                await legalUrlBroadcaster.SendAsync(rootUrl);
            }
            Task render = Render();
            Task monitor = Monitor();
            await Task.WhenAll(monitor);
            await browser.CloseAsync();
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();


            PortiaResponse response = new PortiaResponse
            {
                ProjectName = request.ProjectName,
                StartUrl = request.StartUrl,
                Task = dataByTask
            };
            return response;
        }
        static async Task<UrlHtmlPair> GetUrlHtmlPairAsync(Uri url, Browser browser)
        {
            string html;
            using (Page page = await browser.NewPageAsync())
            {
                await page.SetRequestInterceptionAsync(true);
                page.Request += Page_Request;
                await page.GoToAsync(url.ToString());
                html = await page.GetContentAsync();
            }
            return new UrlHtmlPair(url, html);
        }
        static List<Uri> ParseHtmlToUrls(string html)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            List<Uri> newUrls = new List<Uri>();
            var urlset = htmlDoc.DocumentNode.SelectSingleNode("//urlset[starts-with(@xmlns, 'http://www.sitemaps.org')]"); // if sitemap
            if (urlset != null)
            {
                var locs = htmlDoc.DocumentNode.SelectNodes("//loc");
                if (locs != null)
                {
                    foreach (var loc in locs)
                    {
                        string value = loc.InnerText;
                        Uri url = new Uri(value, UriKind.RelativeOrAbsolute);
                        newUrls.Add(url);
                    }
                }
            }
            else
            {
                var aTags = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
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
            }
            return newUrls;
        }
        private static void ParseObjs(UrlHtmlPair urlHtmlPair)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(urlHtmlPair.Html);
            HtmlNode documentNode = htmlDoc.DocumentNode;
            foreach (PortiaTask task in tasksForRequest)
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
                dataByTask[task.TaskName].Add(taskObject);

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
        private static bool IsUrlLegal(Uri url)
        {
            bool isRootUrlASiteMap = rootUrl.OriginalString.Contains("sitemap");
            bool doesUrlContainRootUrl = url.OriginalString.Contains(rootUrl.OriginalString);
            bool doesUrlAlreadyExistInLegalUrls = legalUrlsQueue.Contains(url);
            bool doesUrlContainAnyDisallowedStrings = Helper.ContainsAnyWords(url, disallowedStrings);
            if (isRootUrlASiteMap == false && doesUrlContainRootUrl == false ||
                 doesUrlAlreadyExistInLegalUrls == true ||
                 doesUrlContainAnyDisallowedStrings == true)
            {
                return false;
            }
            return true;
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
