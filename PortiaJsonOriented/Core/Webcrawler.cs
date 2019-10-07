using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PortiaJsonOriented.Core;
using PortiaJsonOriented.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace PortiaJsonOriented
{
    public class Webcrawler
    {
        public static async Task<string> StartCrawlerAsync(string json)
        {
            Request request = JsonConvert.DeserializeObject<Request>(json);
            Uri rootUri = new Uri(request.StartUrl);
            Queue queue = new Queue();
            List<Uri> visitedUrls = new List<Uri>();
            queue.Enqueue(rootUri);
            int crawledUrlsCount = 0;
            // Add a new list for all projects in Data
            Dictionary<string, JArray> projects = new Dictionary<string, JArray>();
            foreach (var item in request.Data)
            {
                projects.Add(item.ProjectName, new JArray());
            }

            while (queue.Count > 0)
            {
                Uri currentUrl = (Uri)queue.Dequeue();
                visitedUrls.Add(currentUrl);
                crawledUrlsCount++;
                string html = "";
                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(currentUrl);
                    if (!httpResponseMessage.IsSuccessStatusCode)
                    {
                        continue;
                    }
                    Task<string> contentAsString = httpResponseMessage.Content.ReadAsStringAsync();
                    if (contentAsString.IsFaulted)
                    {
                        continue;
                    }
                    html = contentAsString.Result;
                }
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                HtmlNode documentNode = htmlDoc.DocumentNode;
                foreach (Data project in request.Data)
                {

                    JObject projectObject = new JObject();
                    foreach (NodeAttribute item in project.Items)
                    {
                        JToken value = GetValueForJTokenRecursive(item, documentNode);
                        if (value.ToString() == "")
                        {
                            continue;
                        }
                        projectObject.Add(item.Name, value);
                        Metadata metadata = new Metadata(currentUrl.ToString(), DateTime.UtcNow);
                        projectObject.Add("metadata", JObject.FromObject(metadata, new JsonSerializer()
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        }));
                    }
                    if (projectObject.HasValues == false)
                    {
                        continue;
                    }
                    projects[project.ProjectName].Add(projectObject);
                }
                AddNewUrlsToQueue(new List<string>(), rootUri, ref queue, visitedUrls, htmlDoc);
            }

            return "";
        }
        private static JToken GetValueForJTokenRecursive(NodeAttribute node, HtmlNode htmlNode)
        {
            JToken jToken = "";

            if (node.MultipleFromPage) // TODO
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
                            jArray.Add(navigator.Value);
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
                        jToken = nodeFound.Value;
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
        private static void AddNewUrlsToQueue(List<string> blacklistedWords, Uri rootUri, ref Queue queue, List<Uri> visitedUrls, HtmlDocument htmlDoc)
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
                            if (!ContainsBlacklistedWord(url, blacklistedWords)) //BLACKLIST CHECK
                            {
                                queue.Enqueue(url);
                            }
                        }
                    }
                }
            }
        }
        private static bool ContainsBlacklistedWord(Uri url, List<string> blacklistedWords)
        {
            foreach (var word in blacklistedWords)
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
