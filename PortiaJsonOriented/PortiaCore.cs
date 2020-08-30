using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PortiaJsonOriented.DTO;
using PortiaJsonOriented.Models;
using PuppeteerSharpForPortia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace PortiaJsonOriented
{
    public class PortiaCore
    {
        public ConcurrentDictionary<string, JArray> dataByJobName = new ConcurrentDictionary<string, JArray>();
        public BlockingCollection<Uri> urlsToFixedList = new BlockingCollection<Uri>();
        public BlockingCollection<Uri> currentQueuedUrls = new BlockingCollection<Uri>();
        public BlockingCollection<Uri> logOfAllVisitedUrls = new BlockingCollection<Uri>();
        public BlockingCollection<Uri> logOfAllQueuedUrls = new BlockingCollection<Uri>();
        public IList<string> disallowedStrings = new List<string>() { };
        public PortiaRequest portiaRequest = new PortiaRequest();
        public PuppeteerWrapper puppeteerWrapper = null;


        public async Task ParseUrlsAndObjectsFromUrl(Uri url)
        {
            var htmlContent = await puppeteerWrapper.GetHtmlContentAsync(url);
            logOfAllVisitedUrls.TryAdd(url);
            ParseObjectsFromHtmlBasedOnJob(htmlContent);
            var parsedUrls = GetAllAbsoluteUrlsFromHtml(htmlContent.Html);
            FilterAndAddUrls(parsedUrls, ref currentQueuedUrls);
        }
        public async Task ParseObjectsFromUrl(Uri url)
        {
            var htmlContent = await puppeteerWrapper.GetHtmlContentAsync(url);
            logOfAllVisitedUrls.TryAdd(url);
            ParseObjectsFromHtmlBasedOnJob(htmlContent);
        }

        public void FilterAndAddUrls(List<Uri> urls, ref BlockingCollection<Uri> targetQueue)
        {
            foreach (Uri url in urls)
            {
                bool isUrlFromSameDomainAsRootUrl = url.OriginalString.Contains(portiaRequest.Domain.OriginalString);
                bool doesUrlContainAnyDisallowedStrings = ContainsAnyWords(url, disallowedStrings);
                if (isUrlFromSameDomainAsRootUrl == false ||
                    doesUrlContainAnyDisallowedStrings == true ||
                    logOfAllQueuedUrls.Contains(url) == true)
                {
                    continue;
                }
                logOfAllQueuedUrls.TryAdd(url);
                targetQueue.TryAdd(url);
            }

        }
        public List<string> GetAllXpath(NodeAttribute node)
        {
            List<string> xpaths = new List<string>();
            CreateAbsoluteXpathsRecursivelyOnRequiredNodes(ref xpaths, node);
            return xpaths;

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
                KeyValuePair<string, JObject> keyValuePair = new KeyValuePair<string, JObject>(job.Name, jobObject);
                dataByJobName[job.Name].Add(jobObject); //TODO Reference dataByJobName instead of static variable
            }
        }
        private void CreateAbsoluteXpathsRecursivelyOnRequiredNodes(ref List<string> xpaths, NodeAttribute node, string currentXpath = "")
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
                foreach (var attribute in node.Attributes.Where(x => x.IsRequired == true))
                {
                    CreateAbsoluteXpathsRecursivelyOnRequiredNodes(ref xpaths, attribute, currentXpath);
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
