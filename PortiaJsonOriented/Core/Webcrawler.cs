using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PortiaJsonOriented.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace PortiaJsonOriented
{
    public class Webcrawler
    {
        private static void flattendata(List<Attributes> root, List<Attributes> flattendatalist)
        {
            foreach (Attributes item in root)
            {
                flattendatalist.Add(item);
                if (item.attributes != null)
                    flattendata(item.attributes, flattendatalist);
            }
        }
        public static async Task<string> StartCrawlerAsync(string json)
        {
            var obj = JsonConvert.DeserializeObject<PortiaRequest>(json);
            Uri rootUri = new Uri(obj.Data.Url);
            Queue queue = new Queue();
            List<Uri> visitedUrls = new List<Uri>();
            queue.Enqueue(rootUri);
            int crawledUrlsCount = 0;

            while (queue.Count > 0)
            {
                Uri currentUrl = (Uri)queue.Dequeue();
                visitedUrls.Add(currentUrl);
                crawledUrlsCount++;
                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync(currentUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }
                    Task<string> contentAsString = response.Content.ReadAsStringAsync();
                    if (contentAsString.IsFaulted)
                    {
                        continue;
                    }
                    string html = contentAsString.Result;
                    HtmlDocument htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(html);
                    //Flatten objs
                    List<Attributes> flattendatalist = new List<Attributes>();
                    flattendata(obj.Data.Items, flattendatalist);

                    // Should object have xpath? is isRequired needed?



                    // JSON kommer ind
                    // Vi skal hente data strukturen fra i

                    // get crawling order.

                    // Add a new list for Each Items
                    // Iterate over Items in data
                    // check if isMultiple.
                    //// if isMultiple == true create instance of array.
                    // check type of item.
                    //// if type is object iterate over its properties.
                    foreach (var item in obj.Data.Items)
                    {
                        var bla = item.GetHashCode();
                    }
                    AddNewUrlsToQueue(new List<string>(), rootUri, ref queue, visitedUrls, htmlDoc);
                }
            }
            return "";
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
