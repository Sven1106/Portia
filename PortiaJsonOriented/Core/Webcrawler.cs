using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using PortiaJsonOriented.Core.Models;
using Task = System.Threading.Tasks.Task;
using Response = PortiaJsonOriented.Core.Dtos.Response;
using Request = PortiaJsonOriented.Core.Dtos.Request;

namespace PortiaJsonOriented.Core
{
    public class Webcrawler
    {
        public async Task<Response> StartCrawlerAsync(Request request)
        {
            PuppeteerWrapper puppeteerWrapper = await PuppeteerWrapper.CreateAsync();
            List<Task> tasks = new List<Task>();
            List<HtmlContent> content = new List<HtmlContent>();
            for (int u = 0; u < 2; u++)
            {
                for (int i = 0; i < 5; i++)
                {
                    Task task = Task.Run(async () =>
                    {
                        HtmlContent htmlContent = await puppeteerWrapper.GetHtmlContentAsync(new Uri(request.StartUrl));
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
        public static JToken GetValueForJTokenRecursive(NodeAttribute node, HtmlNode htmlNode)
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

        public static bool IsUrlLegal(Uri url, Uri rootUrl, IList<Uri> legalUrlsQueue, List<string> disallowedStrings)
        {
            bool isUrlFromSameDomainAsRootUrl = url.OriginalString.Contains(rootUrl.OriginalString);
            bool doesUrlAlreadyExistInLegalUrls = legalUrlsQueue.Contains(url);
            bool doesUrlContainAnyDisallowedStrings = Helper.ContainsAnyWords(url, disallowedStrings);
            if (isUrlFromSameDomainAsRootUrl == false ||
                 doesUrlAlreadyExistInLegalUrls == true ||
                 doesUrlContainAnyDisallowedStrings == true)
            {
                return false;
            }
            return true;
        }
    }

    public class PuppeteerWrapper
    {
        private Browser browser;

        private async Task<PuppeteerWrapper> InitializeAsync()
        {
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            KillPuppeteerIfRunning();
            var args = new string[] {
                "--no-sandbox",
                "--disable-plugins", "--disable-sync", "--disable-gpu", "--disable-speech-api",
                "--disable-remote-fonts", "--disable-shared-workers", "--disable-webgl", "--no-experiments",
                "--no-first-run", "--no-default-browser-check", "--no-wifi", "--no-pings", "--no-service-autorun",
                "--disable-databases", "--disable-default-apps", "--disable-demo-mode", "--disable-notifications",
                "--disable-permissions-api", "--disable-background-networking", "--disable-3d-apis",
                "--disable-bundled-ppapi-flash"
            };
            var launchOptions = new LaunchOptions { Headless = false, Args = args, IgnoreHTTPSErrors = true };
            browser = await Puppeteer.LaunchAsync(launchOptions);
            return this;
        }

        public static Task<PuppeteerWrapper> CreateAsync()
        {
            var ret = new PuppeteerWrapper();
            return ret.InitializeAsync();
        }

        public async Task<HtmlContent> GetHtmlContentAsync(Uri url)
        {
            string html;
            using (Page page = await browser.NewPageAsync())
            {
                await page.SetRequestInterceptionAsync(true);
                page.Request += async (sender, e) =>
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
                };
                await page.GoToAsync(url.ToString());
                html = await page.GetContentAsync();
            }
            return new HtmlContent(url, html);
        }
        private void KillPuppeteerIfRunning()
        {
            var puppeteerExecutablePath = new BrowserFetcher().GetExecutablePath(BrowserFetcher.DefaultRevision).Replace(@"\", @"\\");
            List<int> processIdsToKill = new List<int>();
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
                            processIdsToKill.Add(processId);
                        }
                    }
                }
            }
            List<Process> processesToKill = Process.GetProcesses().Where(p => processIdsToKill.Where(x => x == p.Id).Any()).ToList();
            if (processesToKill.Count > 0)// Is running
            {
                processesToKill.ForEach((x) =>
                {
                    x.Kill();
                });
            }
        }
    }
}
