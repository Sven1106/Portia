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
using PortiaResponse = PortiaJsonOriented.Core.Dtos.PortiaResponse;
using PortiaRequest = PortiaJsonOriented.Core.Dtos.PortiaRequest;
using PortiaJsonOriented.Core;
using System.Threading.Tasks.Dataflow;
using System.Net;

namespace PortiaJsonOriented
{
    public class WebcrawlerTpl
    {
        private static Uri startUrl;
        private static Uri hostUrl;
        private static List<string> xpathsToWaitFor = new List<string>();
        private static List<PortiaTask> tasksFromRequest = new List<PortiaTask>();
        private static IList<string> disallowedStrings = new List<string>() { };
        private static ConcurrentDictionary<string, JArray> dataByTask = new ConcurrentDictionary<string, JArray>();
        private static BlockingCollection<Uri> urlsQueued = new BlockingCollection<Uri>();
        private static BlockingCollection<Uri> urlsVisited = new BlockingCollection<Uri>();

        private TransformBlock<Uri, HtmlContent> htmlContentDownloader;
        private TransformManyBlock<HtmlContent, Uri> urlParser;
        private TransformManyBlock<Uri, Uri> urlFilter;
        private ActionBlock<HtmlContent> objParser;
        private ActionBlock<Uri> urlsQueueLogger;
        private ActionBlock<HtmlContent> urlsVisitedLogger;

        private BroadcastBlock<Uri> urlBroadcaster;
        private BroadcastBlock<HtmlContent> htmlContentBroadcaster;

        public TransformManyBlock<System.Uri, System.Uri> CreateUrlFilterBlock<Uri>()
        {
            var hs = new HashSet<System.Uri>();
            return new TransformManyBlock<System.Uri, System.Uri>(url =>
            {
                bool isUrlFromSameDomainAsRootUrl = url.OriginalString.Contains(hostUrl.OriginalString);
                bool doesUrlContainAnyDisallowedStrings = Helper.ContainsAnyWords(url, disallowedStrings);
                if (isUrlFromSameDomainAsRootUrl == false ||
                     doesUrlContainAnyDisallowedStrings == true)
                {
                    return Enumerable.Empty<System.Uri>();
                }

                return Enumerable.Repeat(url, hs.Add(url) ? 1 : 0);
            });
        }
        public void CreateBlocks(PuppeteerWrapper puppeteerWrapper)
        {

            urlBroadcaster = new BroadcastBlock<Uri>(url => url);

            urlsQueueLogger = new ActionBlock<Uri>(url => urlsQueued.TryAdd(url));
            urlsVisitedLogger = new ActionBlock<HtmlContent>(htmlContent => urlsVisited.TryAdd(htmlContent.Url));

            htmlContentDownloader = new TransformBlock<Uri, HtmlContent>(async url => await puppeteerWrapper.GetHtmlContentAsync(url, xpathsToWaitFor),
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = -1, // the size of the block input buffer. Handles memory ?
                    MaxDegreeOfParallelism = 3, // by default Tpl dataflow assign a single task per block
                    MaxMessagesPerTask = 2, //enforce fairness, after handling n messages the block's task will be re-schedule.
                    EnsureOrdered = false
                });

            htmlContentBroadcaster = new BroadcastBlock<HtmlContent>(htmlContent => htmlContent);
            objParser = new ActionBlock<HtmlContent>(htmlContent => ParseObjects(htmlContent, tasksFromRequest));
            urlParser = new TransformManyBlock<HtmlContent, Uri>(htmlContent => GetAllAbsoluteUrlsFromHtml(htmlContent.Html));
            urlFilter = CreateUrlFilterBlock<Uri>();
        }
        public async Task RenderCrawling(int fps = 30)
        {
            while (true)
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
                await Task.Delay((int)TimeSpan.FromSeconds(1).TotalMilliseconds / fps);
            }
        }
        public async Task<bool> MonitorCrawling()
        {
            //TODO Remove urlsQueued & urlsVisited and find a way to get the count of values passed through broadcast
            //Find a better way to monitor TPL

            int intervalToCheckInSeconds = 1;
            int maxTimeoutCount = 30;
            int currentTimeOutCount = 0;
            int urlsQueuedCount = 0;
            int currentUrlsVisitedCount = 0;

            while (!urlBroadcaster.Completion.IsCompleted)
            {
                bool isTransformBlocksEmpty = htmlContentDownloader.InputCount == 0 && htmlContentDownloader.OutputCount == 0
                    && urlParser.InputCount == 0 && urlParser.OutputCount == 0
                    && objParser.InputCount == 0;

                bool noChangesInQueue = urlsQueuedCount == urlsQueued.Count && currentUrlsVisitedCount == urlsVisited.Count;
                bool isTimedOut = isTransformBlocksEmpty && noChangesInQueue;
                if (isTimedOut)
                {
                    currentTimeOutCount++;
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
                await Task.Delay((int)TimeSpan.FromSeconds(intervalToCheckInSeconds).TotalMilliseconds);
            }
            bool isSuccesfullyCrawled = urlsQueued.Count == urlsVisited.Count;
            return isSuccesfullyCrawled;
        }


        public async Task<PortiaResponse> StartCrawlerAsync(PortiaRequest request)
        {
            Console.WriteLine("Starting");
            hostUrl = new Uri(request.StartUrl.Scheme + Uri.SchemeDelimiter + request.StartUrl.Host);
            startUrl = request.StartUrl;
            tasksFromRequest = request.Tasks;
            //TODO Add /robots.txt handling eg. Sitemap, Disallow


            foreach (PortiaTask task in tasksFromRequest)
            {
                dataByTask.TryAdd(task.TaskName, new JArray());

                foreach (var item in task.Nodes)
                {
                    var xpaths = GetAllXpath(item);
                    xpathsToWaitFor = xpaths; // TODO Has to handle xpaths foreach item
                }
            }

            PuppeteerWrapper puppeteerWrapper = await PuppeteerWrapper.CreateAsync();
            CreateBlocks(puppeteerWrapper);

            #region unfinishedWork //TODO
            CrashDump crashDump = new CrashDump(request.ProjectName);
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
                await urlBroadcaster.SendAsync(startUrl);
            }
            #endregion


            List<Task<bool>> tasks = new List<Task<bool>>();
            Task render = RenderCrawling();
            bool isFixedListOfUrls = request.IsFixedListOfUrls;
            if (isFixedListOfUrls)
            {
                BufferBlock<Uri> filteredUrls = new BufferBlock<Uri>();

                urlBroadcaster.LinkTo(urlsQueueLogger);
                urlBroadcaster.LinkTo(htmlContentDownloader);
                htmlContentDownloader.LinkTo(htmlContentBroadcaster);
                htmlContentBroadcaster.LinkTo(urlsVisitedLogger);
                htmlContentBroadcaster.LinkTo(urlParser);
                urlParser.LinkTo(urlFilter);
                urlFilter.LinkTo(filteredUrls);

                Task<bool> monitorForUrlParsing = MonitorCrawling();
                tasks.Add(monitorForUrlParsing);
                await Task.WhenAll(tasks);

                CreateBlocks(puppeteerWrapper);
                filteredUrls.LinkTo(urlBroadcaster);
                urlBroadcaster.LinkTo(urlsQueueLogger);
                urlBroadcaster.LinkTo(htmlContentDownloader);
                htmlContentDownloader.LinkTo(htmlContentBroadcaster);
                htmlContentBroadcaster.LinkTo(urlsVisitedLogger);
                htmlContentBroadcaster.LinkTo(objParser);

                Task<bool> monitorForObjParsing = MonitorCrawling();
                tasks.Add(monitorForObjParsing);
            }
            else
            {
                urlBroadcaster.LinkTo(urlsQueueLogger);
                urlBroadcaster.LinkTo(htmlContentDownloader);
                htmlContentDownloader.LinkTo(htmlContentBroadcaster);
                htmlContentBroadcaster.LinkTo(urlsVisitedLogger);
                htmlContentBroadcaster.LinkTo(objParser);
                htmlContentBroadcaster.LinkTo(urlParser);
                urlParser.LinkTo(urlFilter);
                urlFilter.LinkTo(urlBroadcaster);

                Task<bool> monitor = MonitorCrawling();
                tasks.Add(monitor);
            }

            var results = (await Task.WhenAll(tasks)).ToList();
            #region Logging //TODO
            bool successfullyCrawled = results.All(x => x == true);
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

            PortiaResponse response = new PortiaResponse
            {
                ProjectName = request.ProjectName,
                StartUrl = request.StartUrl,
                Tasks = dataByTask
            };
            return response;
        }
        private void ParseObjects(HtmlContent htmlContent, List<PortiaTask> tasks)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent.Html);
            HtmlNode documentNode = htmlDoc.DocumentNode;
            foreach (PortiaTask task in tasks)
            {
                JObject taskObject = new JObject();
                foreach (NodeAttribute item in task.Nodes)
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
                dataByTask[task.TaskName].Add(taskObject); //TODO Reference dataByTask instead of static variable
            }
        }
        private List<string> GetAllXpath(NodeAttribute node)
        {
            List<string> xpaths = new List<string>();
            CreateAbsoluteXpaths(ref xpaths, node);
            return xpaths;

        }
        private void CreateAbsoluteXpaths(ref List<string> xpaths, NodeAttribute node, string currentXpath = "")
        {
            string nodeXpath = node.Xpath;
            if (currentXpath != "")
            {
                nodeXpath = nodeXpath.Replace("./", "/"); //removes relative prefix and prepares xpath for absolute path
            }
            currentXpath += nodeXpath;
            if (node.Attributes == null)
            {
                xpaths.Add(currentXpath);
            }
            else
            {
                foreach (var attribute in node.Attributes)
                {
                    CreateAbsoluteXpaths(ref xpaths, attribute, currentXpath);
                }
            }
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
                        hrefValue = WebUtility.HtmlDecode(hrefValue);
                        Uri url = new Uri(hrefValue, UriKind.RelativeOrAbsolute);
                        url = new Uri(startUrl, url);
                        urlsFound.Add(url);
                    }
                }
            }
            return urlsFound;
        }
        private JToken GetValueForJTokenRecursively(NodeAttribute node, HtmlNode htmlNode) // TODO: see if it is possible to use the same HTMLNode/Htmldocument through out the extractions.
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
                            if (navigator.Value.Trim() == "")
                            {
                                continue;
                            }
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
                        if (nodeFound.Value.Trim() == "")
                        {
                            return jToken;
                        }
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
        private void WriteToConsole(ConsoleColor color, string format)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(format);
            Console.ResetColor();
        }

    }
}
