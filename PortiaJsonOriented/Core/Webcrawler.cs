using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PortiaJsonOriented.Core.Models;
using PuppeteerSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace PortiaJsonOriented
{
    public class Webcrawler
    {
        private static string dequeuedUrlsFile = "dequeuedUrls.txt";
        private static string allQueuedUrlsFile = "allQueuedUrls.txt";
        private static string allSuccesfullUrlsFile = "allSuccesfullUrls.txt";
        private static readonly HttpClient httpClient = new HttpClient();

        public Webcrawler()
        {
            #region Debugging
            File.WriteAllText(dequeuedUrlsFile, string.Empty);
            File.WriteAllText(allQueuedUrlsFile, string.Empty);
            File.WriteAllText(allSuccesfullUrlsFile, string.Empty);


            #endregion
        }
        public async Task<Core.Dtos.Response> StartCrawlerAsync(Core.Dtos.Request request)
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            Uri rootUri = new Uri(request.StartUrl);
            ConcurrentQueue<Uri> queue = new ConcurrentQueue<Uri>();
            IList<Uri> visitedUrls = new List<Uri>();
            IList<string> blackListedWords = new List<string>() { };
            queue.Enqueue(rootUri);
            int itemSuccessfullyCrawledCount = 0;

            int crawledUrlsCount = 0;
            // Add a new list for every task in Data
            Dictionary<string, JArray> tasks = new Dictionary<string, JArray>();
            foreach (var item in request.Data)
            {
                tasks.Add(item.TaskName, new JArray());
            }

            //Enabled headless option
            var launchOptions = new LaunchOptions { Headless = fa };
            var browser = await Puppeteer.LaunchAsync(launchOptions);

            while (itemSuccessfullyCrawledCount < 1000 && queue.Count > 0)
            {
                queue.TryDequeue(out Uri currentUrl);
                File.AppendAllText(dequeuedUrlsFile, currentUrl.ToString() + Environment.NewLine);
                visitedUrls.Add(currentUrl);
                crawledUrlsCount++;

                string html = await GetWithHttpClient(currentUrl);
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                AddNewUrlsToQueue(blackListedWords, rootUri, ref queue, visitedUrls, htmlDoc);

                bool containsAnyStartElements = ContainsAnyRootItems(html, request);

                if (containsAnyStartElements)
                {
                    html = await GetWithPuppeteer(browser, currentUrl);
                    htmlDoc.LoadHtml(html);
                    HtmlNode documentNode = htmlDoc.DocumentNode;
                    foreach (DataForRequest task in request.Data)
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
                            Metadata metadata = new Metadata(currentUrl.ToString(), DateTime.UtcNow);
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
                        File.AppendAllText(allSuccesfullUrlsFile, currentUrl.ToString() + Environment.NewLine);
                        itemSuccessfullyCrawledCount++;
                    }
                }
                Console.Write("\rUrls in queue: {0} - Urls visited: {1} - Items successfully crawled: {2}", queue.Count, crawledUrlsCount, itemSuccessfullyCrawledCount);
            }
            Core.Dtos.Response response = new Core.Dtos.Response
            {
                ProjectName = request.ProjectName,
                StartUrl = request.StartUrl,
                Data = tasks
            };
            return response;
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
        private static void AddNewUrlsToQueue(IList<string> blacklistedWords, Uri rootUri, ref ConcurrentQueue<Uri> queue, IList<Uri> visitedUrls, HtmlDocument htmlDoc)
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
                            if (ContainsAnyWords(url, blacklistedWords)) //BLACKLIST CHECK
                            {
                                continue;
                            }
                            queue.Enqueue(url);
                            File.AppendAllText(allQueuedUrlsFile, url.ToString() + Environment.NewLine);
                        }
                    }
                }
            }
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
        private static bool ContainsAnyRootItems(string html, Core.Dtos.Request request)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            HtmlNode documentNode = htmlDoc.DocumentNode;
            foreach (var datum in request.Data)
            {
                foreach (var item in datum.Items)
                {
                    if (documentNode.SelectSingleNode(item.Xpath) != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static async Task<string> GetWithHttpClient(Uri uri)
        {
            using (HttpResponseMessage response = await httpClient.GetAsync(uri))
            {
                string content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }

        private static async Task<string> GetWithPuppeteer(Browser browser, Uri uri)
        {
            string content = "";
            using (var page = await browser.NewPageAsync())
            {
                await page.GoToAsync(uri.ToString());
                content = await page.GetContentAsync();
            }
            return content;
        }
    }
}
