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
using System.Threading.Tasks;
using System.Xml.XPath;
using PuppeteerSharp;
using PortiaJsonOriented.Core.Models;
using Task = System.Threading.Tasks.Task;
using PortiaTask = PortiaJsonOriented.Core.Models.Task;
using PortiaResponse = PortiaJsonOriented.Core.Dtos.Response;
using PortiaRequest = PortiaJsonOriented.Core.Dtos.Request;
using PortiaJsonOriented.Core;
using System.Threading.Tasks.Dataflow;

using System.Reactive.Disposables;

namespace PortiaJsonOriented
{
    public class WebcrawlerTpl
    {
        private static Uri rootUrl;
        private static List<PortiaTask> tasksFromRequest = new List<PortiaTask>();
        private static IList<string> disallowedStrings = new List<string>() { };
        private static ConcurrentDictionary<string, JArray> dataByTask = new ConcurrentDictionary<string, JArray>();
        private static BlockingCollection<Uri> urlsQueued = new BlockingCollection<Uri>();
        private static BlockingCollection<Uri> urlsVisited = new BlockingCollection<Uri>();

        private TransformBlock<Uri, HtmlContent> htmlContentDownloader;
        private TransformManyBlock<HtmlContent, Uri> urlParser;
        private ActionBlock<HtmlContent> objParser;

        private BroadcastBlock<Uri> urlBroadcaster;
        private BroadcastBlock<HtmlContent> htmlContentBroadcaster;

        public void CreateBlocks(PuppeteerWrapper puppeteerWrapper)
        {

            urlBroadcaster = new BroadcastBlock<Uri>(url =>
            {
                urlsQueued.TryAdd(url);
                return url;
            });

            htmlContentDownloader = new TransformBlock<Uri, HtmlContent>(async url =>
            {
                HtmlContent htmlContent = await puppeteerWrapper.GetHtmlContentAsync(url);
                urlsVisited.TryAdd(url);
                return htmlContent;
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = -1, // the size of the block input buffer. Handles memory ?
                MaxDegreeOfParallelism = 3, // by default Tpl dataflow assign a single task per block
                MaxMessagesPerTask = 2, //enforce fairness, after handling n messages the block's task will be re-schedule.
                EnsureOrdered = false
            });

            htmlContentBroadcaster = new BroadcastBlock<HtmlContent>(htmlContent => htmlContent);
            objParser = new ActionBlock<HtmlContent>(htmlContent => ParseObjects(htmlContent, tasksFromRequest));
            urlParser = new TransformManyBlock<HtmlContent, Uri>(htmlContent =>
            {
                var newUrls = GetAllAbsoluteUrlsFromHtml(htmlContent.Html);
                return newUrls;
            });

        }

        public CompositeDisposable ConfigureBlocksForFeedbackLoop()
        {
            CompositeDisposable blocksForFeedBackLoop = new CompositeDisposable(
                urlBroadcaster.LinkTo(htmlContentDownloader, url => IsUrlLegal(url)),
                htmlContentDownloader.LinkTo(htmlContentBroadcaster),
                htmlContentBroadcaster.LinkTo(objParser),
                htmlContentBroadcaster.LinkTo(urlParser),
                urlParser.LinkTo(urlBroadcaster)
            );
            return blocksForFeedBackLoop;
        }

        public void ConfigureBlocksForFixedListOfUrls()
        {
            urlBroadcaster.LinkTo(htmlContentDownloader);
            htmlContentDownloader.LinkTo(htmlContentBroadcaster);
            htmlContentBroadcaster.LinkTo(objParser);
        }

        public async Task RenderCrawling(int fps = 30)
        {
            while (!urlBroadcaster.Completion.IsCompleted)
            {
                string format = $"\nurls queued:                         = {urlsQueued.Count}    " +
                                $"\nurls visited:                        = {urlsVisited.Count}    " +
                                $"\nurlBroadcaster:         is completed = {urlBroadcaster.Completion.IsCompleted}    " +
                                $"\nhtmlContentBroadcaster: is completed = {htmlContentBroadcaster.Completion.IsCompleted}    " +
                                $"\nhtmlDownloader:         is completed = {htmlContentDownloader.Completion.IsCompleted}, input={htmlContentDownloader.InputCount}, output={htmlContentDownloader.OutputCount}    " +
                                $"\nurlParser:              is completed = {urlParser.Completion.IsCompleted}, input={urlParser.InputCount}, output={urlParser.OutputCount}    " +
                                $"\nobjParser:              is completed = {objParser.Completion.IsCompleted}, input={objParser.InputCount}    ";
                ConsoleColor color = ConsoleColor.Yellow;
                Console.SetCursorPosition(0, 0);
                WriteToConsole(color, format);
                await Task.Delay(1000 / fps);
            }
        }

        public async Task<bool> MonitorCrawling()
        {
            int intervalToCheckInSeconds = 1;
            int maxTimeoutCount = 5;
            int currentTimeOutCount = 0;
            int urlsQueuedCount = 0;
            int currentUrlsVisitedCount = 0;

            while (!urlBroadcaster.Completion.IsCompleted)
            {
                bool nothingInTransformBlocks = htmlContentDownloader.InputCount == 0 && htmlContentDownloader.OutputCount == 0
                    && urlParser.InputCount == 0 && urlParser.OutputCount == 0
                    && objParser.InputCount == 0;

                bool noChangesInQueue = urlsQueuedCount == urlsQueued.Count && currentUrlsVisitedCount == urlsVisited.Count;
                bool isTimedOut = nothingInTransformBlocks && noChangesInQueue;
                if (isTimedOut)
                {
                    currentTimeOutCount++;
                    //Console.WriteLine("Timeout in: {0}", intervalToCheckInSeconds * (maxTimeoutCount + 1) - intervalToCheckInSeconds * currentTimeOutCount);
                }
                else
                {
                    currentTimeOutCount = 0;
                }
                bool maxTimeOutHit = currentTimeOutCount >= maxTimeoutCount;
                if (maxTimeOutHit) //TIMED OUT
                {
                    urlBroadcaster.Complete();
                }

                urlsQueuedCount = urlsQueued.Count;
                currentUrlsVisitedCount = urlsVisited.Count;
                await Task.Delay(intervalToCheckInSeconds * 1000);
            }
            bool succesfullyCrawled = urlsQueued.Count == urlsVisited.Count;
            return succesfullyCrawled;
        }

        public async Task<PortiaResponse> StartCrawlerAsync(PortiaRequest request)
        {
            Console.WriteLine("Starting");
            rootUrl = new Uri(request.StartUrl);
            tasksFromRequest = request.Tasks;
            disallowedStrings = request.DisallowedStrings;
            //TODO Add /robots.txt handling eg. Sitemap, Disallow

            foreach (PortiaTask task in tasksFromRequest)
            {
                dataByTask.TryAdd(task.TaskName, new JArray());
            }

            PuppeteerWrapper puppeteerWrapper = await PuppeteerWrapper.CreateAsync();
            CreateBlocks(puppeteerWrapper);

            #region unfinishedWork
            CrashDump crashDump = new CrashDump("GUID");
            var remainingWork = await crashDump.AnyCrashDump();
            if (remainingWork.Count() != 0)
            {
                dataByTask = JsonConvert.DeserializeObject<ConcurrentDictionary<string, JArray>>(remainingWork[nameof(dataByTask)]);

                List<Uri> _urlsQueued = JsonConvert.DeserializeObject<ICollection<Uri>>(remainingWork[nameof(urlsQueued)]).ToList();
                _urlsQueued.ForEach(lu => urlsQueued.TryAdd(lu));

                List<Uri> _urlsVisited = JsonConvert.DeserializeObject<ICollection<Uri>>(remainingWork[nameof(urlsVisited)]).ToList();
                _urlsVisited.ForEach(vu => urlsVisited.TryAdd(vu));

                List<Uri> _urlsRemaining = _urlsQueued.Except(_urlsVisited).ToList();
                _urlsRemaining.ForEach(async (uu) => await htmlContentDownloader.SendAsync(uu));
            }
            else
            {
                await urlBroadcaster.SendAsync(rootUrl);
            }
            #endregion

            #region Configuring Blocks
            List<Task> tasks = new List<Task>();
            Task render = RenderCrawling();
            bool isFixedListOfUrls = false; //Add bool to Request.JSON
            if (isFixedListOfUrls)
            {

                BufferBlock<Uri> tempUrlBroadcaster = new BufferBlock<Uri>();

                //ConfigureBlocksForFixedListOfUrls();

                urlBroadcaster.LinkTo(htmlContentDownloader, url => IsUrlLegal(url));
                htmlContentDownloader.LinkTo(htmlContentBroadcaster);

                //Task 1
                //Get all Urls from rootUrl and add to a broadcastBlock.
                CompositeDisposable part1 = new CompositeDisposable(
                    htmlContentBroadcaster.LinkTo(urlParser),
                    urlParser.LinkTo(tempUrlBroadcaster)
                );

                Task<bool> monitorTask = MonitorCrawling();
                await Task.WhenAll(monitorTask);


                //Task 2. 
                //// https://stackoverflow.com/questions/24631767/tpl-dataflow-how-to-remove-the-link-between-the-blocks
                //Dispose htmlContentBroadcaster
                //Add ObjParser
                //tempUrlBroadcaster.LinkTo(urlBroadcaster, linkOptions);
                //htmlContentBroadcaster.LinkTo(objParser, linkOptions);
                CreateBlocks(puppeteerWrapper);
                urlBroadcaster.LinkTo(htmlContentDownloader);
                htmlContentDownloader.LinkTo(htmlContentBroadcaster);
                CompositeDisposable part2 = new CompositeDisposable(
                    htmlContentBroadcaster.LinkTo(objParser)
                );
                while (tempUrlBroadcaster.TryReceive(out Uri item))
                {
                    await urlBroadcaster.SendAsync(item);
                }
                Task<bool> monitorTask2 = MonitorCrawling();
                await Task.WhenAll(monitorTask2);



            }
            else
            {
                var linksForFeedBackLoop = ConfigureBlocksForFeedbackLoop();
                Task<bool> monitorTask = MonitorCrawling();
                await Task.WhenAll(monitorTask);
                #region Logging
                bool successfullyCrawled = monitorTask.Result;
                if (successfullyCrawled)
                {

                }
                else
                {
                    Dictionary<string, string> jsonByVariableName = new Dictionary<string, string>();
                    jsonByVariableName.Add(nameof(urlsQueued), JsonConvert.SerializeObject(urlsQueued));
                    jsonByVariableName.Add(nameof(dataByTask), JsonConvert.SerializeObject(dataByTask));
                    jsonByVariableName.Add(nameof(urlsVisited), JsonConvert.SerializeObject(urlsVisited));
                    await crashDump.CreateDumpFiles(jsonByVariableName);
                }
                #endregion
            }
            #endregion

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
        private List<Uri> GetAllAbsoluteUrlsFromHtml(string html)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            List<Uri> urlsFound = new List<Uri>();
            if (htmlDoc.DocumentNode.SelectSingleNode("//urlset[starts-with(@xmlns, 'http://www.sitemaps.org')]") != null) // if sitemap)
            {
                var locs = htmlDoc.DocumentNode.SelectNodes("//loc");
                if (locs != null)
                {
                    foreach (var loc in locs)
                    {
                        string value = loc.InnerText;
                        Uri url = new Uri(value, UriKind.RelativeOrAbsolute);
                        urlsFound.Add(url);
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
                        urlsFound.Add(url);
                    }
                }
            }
            return urlsFound;
        }
        private void ParseObjects(HtmlContent htmlContent, List<PortiaTask> tasks)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent.Html);
            HtmlNode documentNode = htmlDoc.DocumentNode;
            foreach (PortiaTask task in tasks)
            {
                JObject taskObject = new JObject();
                foreach (NodeAttribute item in task.Items)
                {
                    JToken value = GetValueForJTokenRecursively(item, documentNode);
                    if (value.ToString() == "")
                    {
                        continue;
                    }
                    taskObject.Add(item.Name, value);
                    Metadata metadata = new Metadata(htmlContent.Url.ToString(), DateTime.UtcNow);
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
        private JToken GetValueForJTokenRecursively(NodeAttribute node, HtmlNode htmlNode)
        {
            JToken jToken = "";
            if (node.GetMultipleFromPage)
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
                                JToken value = GetValueForJTokenRecursively(attribute, element);
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
                            JToken value = GetValueForJTokenRecursively(attribute, element);
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
        private bool IsUrlLegal(Uri url)
        {
            bool isUrlFromSameDomainAsRootUrl = url.OriginalString.Contains(rootUrl.OriginalString);
            bool doesUrlAlreadyExistInLegalUrls = urlsQueued.Contains(url);
            bool doesUrlContainAnyDisallowedStrings = Helper.ContainsAnyWords(url, disallowedStrings);
            if (isUrlFromSameDomainAsRootUrl == false ||
                 doesUrlAlreadyExistInLegalUrls == true ||
                 doesUrlContainAnyDisallowedStrings == true)
            {
                return false;
            }
            return true;
        }
        private void WriteToConsole(ConsoleColor color, string format)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(format);
            Console.ResetColor();
        }

    }
}
