using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using PortiaJsonOriented.Core.Models;
using PuppeteerSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Xml.XPath;
using Request = PortiaJsonOriented.Core.Dtos.Request;
using Response = PortiaJsonOriented.Core.Dtos.Response;
using Task = System.Threading.Tasks.Task;

namespace PortiaJsonOriented.Core
{
    public class Webcrawler
    {
        public async Task<Response> StartCrawlerAsync(Request request)
        {
            ConcurrentQueue<Uri> queuedUrls = new ConcurrentQueue<Uri>();
            ConcurrentQueue<Uri> dequeuedUrls = new ConcurrentQueue<Uri>();
            PuppeteerWrapper puppeteerWrapper = await PuppeteerWrapper.CreateAsync();
            List<Task> tasks = new List<Task>();
            List<HtmlContent> content = new List<HtmlContent>();
            for (int u = 0; u < 2; u++)
            {
                for (int i = 0; i < 5; i++)
                {
                    Task task = Task.Run(async () =>
                    {
                        HtmlContent htmlContent = await puppeteerWrapper.GetHtmlContentAsync(request.StartUrl);
                        content.Add(htmlContent);
                    });
                    tasks.Add(task);
                }
            }
            await Task.WhenAll(tasks);
            return new Response();
        }
    }
    public static class PortiaHelpers
    {
        public static List<Uri> GetAbsoluteUrlsFromHtml(string html, Uri rootUrl)
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
        public static JToken GetValueForJTokenRecursively(NodeAttribute node, HtmlNode htmlNode)
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

        public static bool IsUrlLegal(Uri url, Uri rootUrl, IList<Uri> currentQueue, List<string> disallowedStrings)
        {
            bool isUrlFromSameDomainAsRootUrl = url.OriginalString.Contains(rootUrl.OriginalString);
            bool doesUrlAlreadyExistInQueue = currentQueue.Contains(url);
            bool doesUrlContainAnyDisallowedStrings = Helper.ContainsAnyWords(url, disallowedStrings);
            if (isUrlFromSameDomainAsRootUrl == false ||
                 doesUrlAlreadyExistInQueue == true ||
                 doesUrlContainAnyDisallowedStrings == true)
            {
                return false;
            }
            return true;
        }
    }


}
