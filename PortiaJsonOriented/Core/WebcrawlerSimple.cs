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
using PortiaJsonOriented.Core;
using PortiaJsonOriented.Core.Models;
using PortiaJsonOriented.Core.DTO;
using System.Net;
using PuppeteerSharpForPortia;

namespace PortiaJsonOriented
{
    public class WebcrawlerSimple
    {
        private ConcurrentDictionary<string, JArray> dataByJobName = new ConcurrentDictionary<string, JArray>();
        private BlockingCollection<Uri> currentQueuedUrls = new BlockingCollection<Uri>();
        private BlockingCollection<Uri> logOfAllVisitedUrls = new BlockingCollection<Uri>();
        private BlockingCollection<Uri> logOfAllQueuedUrls = new BlockingCollection<Uri>();
        private BlockingCollection<Uri> urlsToFixedList = new BlockingCollection<Uri>();
        private IList<string> disallowedStrings = new List<string>() { };
        private PuppeteerWrapper puppeteerWrapper;
        private readonly int maxConcurrentDownload = 3;
        private PortiaRequest portiaRequest;

        public async Task<PortiaResponse> StartAsync(PortiaRequest request)
        {
            Console.WriteLine("Starting");
            portiaRequest = request;
            // TODO Add /robots.txt handling eg. Sitemap, Disallow
            #region preparation
            List<string> xpathsToWaitFor = new List<string>();
            portiaRequest.Jobs.ForEach((job) =>
            {
                dataByJobName.TryAdd(job.Name, new JArray());// Initialize a new Key-value Pair for each Job.
                job.Nodes.ForEach(jobNode => xpathsToWaitFor.AddRange(GetAllXpath(jobNode))); // Creates the list of xpathsToWaitFor.
            });
            // TODO create SignalR connection and return it to client.
            #endregion
            puppeteerWrapper = await PuppeteerWrapper.CreateAsync();
            Task render = RenderCrawling();

            var runningTasks = new List<Task>();
            bool isFixedListOfUrls = portiaRequest.IsFixedListOfUrls;
            if (isFixedListOfUrls)
            {
                FilterAndAddUrls(portiaRequest.StartUrls, ref urlsToFixedList);
                while (urlsToFixedList.Any() || runningTasks.Any())
                {
                    while (urlsToFixedList.Any() && runningTasks.Count < maxConcurrentDownload)
                    {
                        if (urlsToFixedList.TryTake(out Uri uri))
                        {
                            runningTasks.Add(ParseUrlsAndObjectsFromUrl(uri));
                        }
                    }
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
                while (currentQueuedUrls.Any() || runningTasks.Any())
                {
                    while (currentQueuedUrls.Any() && runningTasks.Count < maxConcurrentDownload)
                    {
                        if (currentQueuedUrls.TryTake(out Uri uri))
                        {
                            runningTasks.Add(ParseObjectsFromUrl(uri));
                        }
                    }
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
            }
            else
            {

                FilterAndAddUrls(portiaRequest.StartUrls, ref currentQueuedUrls);
                while (currentQueuedUrls.Any() || runningTasks.Any())
                {
                    while (currentQueuedUrls.Any() && runningTasks.Count < maxConcurrentDownload)
                    {
                        if (currentQueuedUrls.TryTake(out Uri uri))
                        {
                            runningTasks.Add(ParseUrlsAndObjectsFromUrl(uri));
                        }
                    }
                    var firstCompletedTask = await Task.WhenAny(runningTasks);
                    runningTasks.Remove(firstCompletedTask);
                }
            }
            Console.WriteLine("Adding was completed!");
            Console.ReadKey();
            PortiaResponse response = new PortiaResponse
            {
                ProjectName = request.ProjectName,
                Domain = request.Domain,
                Jobs = dataByJobName
            };
            return response;
        }

        private async Task ParseUrlsAndObjectsFromUrl(Uri url)
        {
            var htmlContent = await puppeteerWrapper.GetHtmlContentAsync(url);
            logOfAllVisitedUrls.TryAdd(url);
            ParseObjectsFromHtmlBasedOnJob(htmlContent);
            var parsedUrls = GetAllAbsoluteUrlsFromHtml(htmlContent.Html);
            FilterAndAddUrls(parsedUrls, ref currentQueuedUrls);
        }
        private async Task ParseObjectsFromUrl(Uri url)
        {
            var htmlContent = await puppeteerWrapper.GetHtmlContentAsync(url);
            logOfAllVisitedUrls.TryAdd(url);
            ParseObjectsFromHtmlBasedOnJob(htmlContent);
        }

        private void FilterAndAddUrls(List<Uri> parsedUrls, ref BlockingCollection<Uri> targetQueue)
        {
            foreach (var parsedUrl in parsedUrls)
            {
                bool isUrlFromSameDomainAsRootUrl = parsedUrl.OriginalString.Contains(portiaRequest.Domain.OriginalString);
                bool doesUrlContainAnyDisallowedStrings = Helper.ContainsAnyWords(parsedUrl, disallowedStrings);
                if (isUrlFromSameDomainAsRootUrl == false ||
                    doesUrlContainAnyDisallowedStrings == true ||
                    logOfAllQueuedUrls.Contains(parsedUrl) == true)
                {
                    continue;
                }
                logOfAllQueuedUrls.TryAdd(parsedUrl);
                targetQueue.TryAdd(parsedUrl);
            }

        }

        private void ParseObjectsFromHtmlBasedOnJob(HtmlContent htmlContent) // TODO Better name
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent.Html);
            HtmlNode documentNode = htmlDoc.DocumentNode;
            foreach (Job job in portiaRequest.Jobs) // TODO Find a better way to reference the job schemas
            {
                JObject jobObject = new JObject();
                foreach (NodeAttribute item in job.Nodes)
                {
                    JToken value = GetValueForJTokenRecursively(item, documentNode);
                    if (value.ToString() == "")
                    {
                        continue;
                    }
                    jobObject.Add(item.Name, value);
                    Metadata metadata = new Metadata(htmlContent.Url.ToString(), DateTime.UtcNow);
                    jobObject.Add("metadata", JObject.FromObject(metadata, new JsonSerializer()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }));

                }
                if (jobObject.HasValues == false)
                {
                    continue;
                }
                dataByJobName[job.Name].Add(jobObject); //TODO Reference dataByJobName instead of static variable
            }
        }
        private List<string> GetAllXpath(NodeAttribute node)
        {
            List<string> xpaths = new List<string>();
            CreateAbsoluteXpathsRecursively(ref xpaths, node);
            return xpaths;

        }
        private void CreateAbsoluteXpathsRecursively(ref List<string> xpaths, NodeAttribute node, string currentXpath = "")
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
                    CreateAbsoluteXpathsRecursively(ref xpaths, attribute, currentXpath);
                }
            }
        }
        private List<Uri> GetAllAbsoluteUrlsFromHtml(string html)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            List<Uri> urlsFound = new List<Uri>();
            if (htmlDoc.DocumentNode.SelectSingleNode("//urlset[starts-with(@xmlns, 'http://www.sitemaps.org')]") != null) // if sitemap
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
                        url = new Uri(portiaRequest.Domain, url);
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
                if (node.Type == NodeType.String || node.Type == NodeType.Number || node.Type == NodeType.Boolean) // basic types
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
                else if (node.Type == NodeType.Object && node.Attributes.Count > 0) // complex types
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
                if (node.Type == NodeType.String || node.Type == NodeType.Number || node.Type == NodeType.Boolean) // basic types
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
                else if (node.Type == NodeType.Object && node.Attributes.Count > 0) // complex types
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

        private async Task RenderCrawling(int fps = 30)
        {
            while (true)
            {
                string format = $"\ncurrent urls in queue              = {currentQueuedUrls.Count}    " +
                                $"\nurls visited                       = {logOfAllVisitedUrls.Count}    " +
                                $"\nlog of all queued urls             = {logOfAllQueuedUrls.Count}    " +
                                $"\n" +
                                $"\nobjects parsed for:";
                foreach (var item in dataByJobName)
                {
                    format +=   $"\n    '{item.Key}' = {item.Value.Count}    ";
                }
                ConsoleColor color = ConsoleColor.Yellow;
                Console.SetCursorPosition(0, 0);
                Console.ForegroundColor = color;
                Console.WriteLine(format);
                Console.ResetColor();
                await Task.Delay((int)TimeSpan.FromSeconds(1).TotalMilliseconds / fps);
            }
        }
    }
}
