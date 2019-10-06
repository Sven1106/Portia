using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
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

                //// Iterate over Attributes in an item
                ////// Create property name for attribute
                ////// If MultipleFromPage create JArray
                //////// Check type of attribute
                //////// If basic type add directly to propertyValue
                //////// If object type iterate over attribute.properties.

                // Iterate over projects in request.data
                foreach (Data project in request.Data)
                {
                    JObject rootObject = new JObject();
                    //// Iterate over items in a project.Items
                    foreach (NodeAttribute item in project.Items)
                    {
                        JToken value = GetValueForJToken(item, documentNode);
                        rootObject.Add(item.Name, value);
                        //DoSomethingFromAttribute(item, documentNode); // returns product
                    }
                    projects[project.ProjectName].Add(rootObject);
                }





                // check if isMultiple.
                //// if isMultiple == true create instance of array.
                // check type of item.
                //// if type is object iterate over its properties.
                var bla = request.Data[0].Items;

                AddNewUrlsToQueue(new List<string>(), rootUri, ref queue, visitedUrls, htmlDoc);

            }
            return "";
        }

        // PropertyName, IsMultiple, PropertyType, PropertyValue

        private static JToken GetValueForJToken(NodeAttribute node, HtmlNode htmlNode)
        {
            JToken jToken;
            if (node.MultipleFromPage) // TODO
            {
                jToken = GetMultipleNodeValue(node, htmlNode);
            }
            else
            {
                jToken = GetSingleNodeValue(node, htmlNode);
            }

            return jToken;
        }

        private static JArray GetMultipleNodeValue(NodeAttribute node, HtmlNode htmlNode)
        {
            JArray jArray = new JArray();
            if (node.Type.ToLower() == "string" || node.Type.ToLower() == "number" || node.Type.ToLower() == "boolean") // basic types
            {
                HtmlNodeCollection elements = htmlNode.SelectNodes(node.Xpath);
                foreach (var element in elements)
                {
                    jArray.Add(GetSingleNodeValue(node, htmlNode));
                }
            }
            else if (node.Type.ToLower() == "object" && node.Attributes.Count > 0) // complex types
            {
                HtmlNodeCollection elements = htmlNode.SelectNodes(node.Xpath);
                //foreach (var element in elements)
                //{
                //    JObject jObject = new JObject();
                //    foreach (var attribute in node.Attributes)
                //    {
                //        JToken value = GetSingleNodeValue(attribute, element);
                //        jObject.Add(attribute.Name, value);
                //    }
                //    jArray.Add(jObject);
                //}

            }
            else
            { // TODO add prober error handling. return list of errors that occurred.

            }
            return jArray;
        }

        private static JToken GetSingleNodeValue(NodeAttribute node, HtmlNode htmlNode)
        {
            JToken jToken;
            HtmlNodeNavigator navigator = (HtmlNodeNavigator)htmlNode.CreateNavigator();
            if (node.Type.ToLower() == "string" || node.Type.ToLower() == "number" || node.Type.ToLower() == "boolean") // basic types
            {
                XPathNavigator nodeFound = navigator.SelectSingleNode(node.Xpath);
                // Get as Type
                jToken = nodeFound.Value;
            }
            else if (node.Type.ToLower() == "object" && node.Attributes.Count > 0) // complex types
            {
                JObject jObject = new JObject();
                HtmlNode element = htmlNode.SelectSingleNode(node.Xpath);
                foreach (var attribute in node.Attributes)
                {
                    JToken value = GetValueForJToken(attribute, element);
                    jObject.Add(attribute.Name, value);
                }
                jToken = jObject;
            }
            else
            { // TODO add prober error handling. return list of errors that occurred.
                jToken = "ERROR OCCURED";
            }
            return jToken;
        }




        private static void DoSomethingFromAttribute(NodeAttribute node, HtmlNode htmlNode)
        {
            HtmlNodeNavigator navigator = (HtmlNodeNavigator)htmlNode.CreateNavigator();
            if (node.MultipleFromPage)
            {
                JArray jArray = new JArray();
            }
            else
            {
                JObject jObject = new JObject();
                if (node.Type.ToLower() == "string" || node.Type.ToLower() == "number" || node.Type.ToLower() == "boolean") // basic types
                {
                    var nodeFound = navigator.SelectSingleNode(node.Xpath);
                    // Get as Type
                    var value = nodeFound.Value;
                    jObject.Add(node.Name, value);
                }
                else if (node.Type.ToLower() == "object" && node.Attributes.Count > 0) // complex types
                {
                    HtmlNode element = htmlNode.SelectSingleNode(node.Xpath);
                    foreach (var attribute in node.Attributes)
                    {
                        DoSomethingFromAttribute(attribute, element);
                    }
                }
                else
                { // TODO add prober error handling. return list of errors that occurred.

                }
            }
            //return new ClassDefinition(node.Name, type, node.Xpath);
        }




        private static string GetXpath(List<Core.Models.NodeAttribute> attributes)
        {
            foreach (Core.Models.NodeAttribute item in attributes)
            {
                if (item.Type == "object")
                {
                    return GetXpath(item.Attributes);
                }
                else
                {
                    return item.Xpath;
                }
            }
            return GetXpath(attributes);
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
