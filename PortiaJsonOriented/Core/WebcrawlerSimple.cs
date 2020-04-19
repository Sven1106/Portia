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
using PortiaJsonOriented.Core.Dtos;
using Task = System.Threading.Tasks.Task;
using PortiaTask = PortiaJsonOriented.Core.Models.Task;
using PortiaJsonOriented.Core;
using System.Threading.Tasks.Dataflow;
using System.Net;

namespace PortiaJsonOriented
{
    public class WebcrawlerSimple
    {
        private static Uri domain;
        private static Dictionary<string, JArray> dataByTaskName = new Dictionary<string, JArray>();
        private static BlockingCollection<Uri> urlsQueued = new BlockingCollection<Uri>();
        private PuppeteerWrapper puppeteerWrapper;

        public async Task<PortiaResponse> StartCrawlerAsync(PortiaRequest request)
        {
            Console.WriteLine("Starting");
            domain = request.Domain;
            //TODO Add /robots.txt handling eg. Sitemap, Disallow
            #region preparation
            List<string> xpathsToWaitFor = new List<string>();
            request.Tasks.ForEach((task) =>
            {
                dataByTaskName.Add(task.TaskName, new JArray());// Initialize a new Key-value Pair for each Task.
                task.Nodes.ForEach(TaskNode => xpathsToWaitFor.AddRange(GetAllXpath(TaskNode))); // Creates the list of xpathsToWaitFor.
            });
            #endregion
            request.StartUrls.ForEach((url) => {
                urlsQueued.Add(url);
            });
            puppeteerWrapper = await PuppeteerWrapper.CreateAsync();

            var runningTasks = new List<Task<Uri>>();
            runningTasks.Add(ProcessUrl(request.StartUrls.FirstOrDefault()));
            while (runningTasks.Any())
            {
                var firstCompletedTask = await Task.WhenAny(runningTasks);
                runningTasks.Remove(firstCompletedTask);
                var urlsFound = await firstCompletedTask;
                Console.WriteLine(urlsFound);

                //await puppeteerWrapper.GetHtmlContentAsync(bla);
                //Console.WriteLine(urlsQueued.IsCompleted);
            }
            Console.WriteLine("Adding was completed!");
            bool isFixedListOfUrls = request.IsFixedListOfUrls;

            PortiaResponse response = new PortiaResponse
            {
                ProjectName = request.ProjectName,
                Domain = request.Domain,
                Tasks = dataByTaskName
            };
            return response;
        }

        private async Task<Uri> ProcessUrl(Uri url)
        {
            await puppeteerWrapper.GetHtmlContentAsync(url);
            return url;
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
                dataByTaskName[task.TaskName].Add(taskObject); //TODO Reference dataByTask instead of static variable
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
                        url = new Uri(domain, url);
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
    }
}
