using HtmlAgilityPack;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using PortiaObjectOriented.Core;

namespace PortiaObjectOriented
{
    public static class Webcrawler
    {
        private static ConcurrentDictionary<object, XpathAttribute[]> properties = new ConcurrentDictionary<object, XpathAttribute[]>();
        private static XpathAttribute[] xpathAttributes;
        private static string dequeuedUrlsFile = "dequeuedUrls.txt";
        private static string allQueuedUrlsFile = "allQueuedUrls.txt";
        private static string allSuccesfullUrlsFile = "allSuccesfullUrls.txt";
        public static async Task<List<object>> StartCrawlerAsync(string rootUrl, List<string> blacklistedWords, Type type)
        {
            object rootObject = Activator.CreateInstance(type);
            Type rootObjectType = rootObject.GetType();
            if (rootObjectType.GetInterfaces().Contains(typeof(IWebcrawler)))
            {
                Uri rootUri = new Uri(rootUrl);
                Queue queue = new Queue();
                List<Uri> visitedUrls = new List<Uri>();
                List<object> items = new List<object>();
                queue.Enqueue(rootUri); // Add root page to queue

                #region Debugging
                File.WriteAllText(dequeuedUrlsFile, String.Empty);
                File.WriteAllText(allQueuedUrlsFile, String.Empty);
                File.WriteAllText(allSuccesfullUrlsFile, String.Empty);
                int crawledUrlsCount = 0;
                #endregion

                while (queue.Count > 0)
                {
                    Uri currentUrl = (Uri)queue.Dequeue();
                    visitedUrls.Add(currentUrl);
                    File.AppendAllText(dequeuedUrlsFile, currentUrl.ToString() + Environment.NewLine);
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

                        #region Elements to look for
                        xpathAttributes = properties.GetOrAdd(rootObjectType, x => (XpathAttribute[])rootObjectType.GetCustomAttributes(typeof(XpathAttribute), false));
                        HtmlNodeCollection elements = htmlDoc.DocumentNode.SelectNodes(xpathAttributes[0].NodeXpath);
                        if (elements != null)
                        {
                            foreach (var element in elements)
                            {
                                object item = CreateInstanceAndMapHtmlNode(type, element, currentUrl.ToString());
                                if (IsAnyValueAssigned(item))
                                {
                                    items.Add(item);
                                    File.AppendAllText(allSuccesfullUrlsFile, currentUrl.ToString() + Environment.NewLine);
                                }
                            }
                        }
                        #endregion

                        #region Breadth-first traversing
                        TraverseUrlsInHtmlDocument(blacklistedWords, rootUri, queue, visitedUrls, htmlDoc);
                        #endregion
                        Console.Write("\rUrls in queue: {0} - Urls visited: {1} - Items successfully crawled: {2}", queue.Count, crawledUrlsCount, items.Count);
                    }
                }
                return items;
            }
            else
            {
                Console.WriteLine("Root type " + type.Name + " has to derive from the interface " + typeof(IWebcrawler).Name);
                return null;
            }

        }

        private static void TraverseUrlsInHtmlDocument(List<string> blacklistedWords, Uri rootUri, Queue queue, List<Uri> visitedUrls, HtmlDocument htmlDoc)
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

                                File.AppendAllText(allQueuedUrlsFile, url.ToString() + Environment.NewLine);
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

        private static object CreateInstanceAndMapHtmlNode(Type type, HtmlNode htmlNode, string url)
        {
            object objectInstance = Activator.CreateInstance(type); // WHY?
            Type objectType = objectInstance.GetType(); // WHY?
            foreach (PropertyInfo property in objectType.GetProperties())
            {
                Type propertyType = property.PropertyType;

                xpathAttributes = properties.GetOrAdd(property, x => (XpathAttribute[])property.GetCustomAttributes(typeof(XpathAttribute), false));
                if (propertyType == typeof(Metadata))
                {
                    continue;
                }
                else if (xpathAttributes.Count() > 0)
                { // returns the xpath expression from the HtmlNodeMapping object.
                    HtmlNodeNavigator navigator = (HtmlNodeNavigator)htmlNode.CreateNavigator(); // HtmlNodeNavigator makes it possible to directly grab the attribute from a HtmlNode with xpath. DocumentNode DOESN'T support this.

                    if (xpathAttributes[0].NodeXpath == string.Empty)
                    {
                        object value = navigator.ValueAs(property.PropertyType);
                        if(property.PropertyType == typeof(string))
                        {
                            value = value.ToString().Trim();
                        }
                        property.SetValue(objectInstance, value);
                    }
                    else
                    {
                        var nodeFound = navigator.SelectSingleNode(xpathAttributes[0].NodeXpath);
                        if (nodeFound == null)
                        {
                            continue;
                        }
                        object value = nodeFound.ValueAs(property.PropertyType); // returns the value as the object property type.
                        property.SetValue(objectInstance, value);
                    }

                }
                else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    propertyType = propertyType.GetGenericArguments()[0];

                    xpathAttributes = properties.GetOrAdd(propertyType, x => (XpathAttribute[])propertyType.GetCustomAttributes(typeof(XpathAttribute), false));
                    string typeRootXpath = xpathAttributes[0].NodeXpath;

                    HtmlNodeCollection elements = htmlNode.SelectNodes(typeRootXpath);
                    if (elements == null)
                    {
                        continue;
                    }
                    IList list = (IList)Activator.CreateInstance(property.PropertyType);
                    foreach (var element in elements)
                    {
                        object item = CreateInstanceAndMapHtmlNode(propertyType, element, url);
                        list.Add(item);
                    }
                    property.SetValue(objectInstance, list);
                }
                else if (propertyType.Module.ScopeName == "CommonLanguageRuntimeLibrary") // Checks if the propertytype is a built-in type?!
                {
                    continue;
                }
                else
                {
                    xpathAttributes = properties.GetOrAdd(propertyType, x => (XpathAttribute[])propertyType.GetCustomAttributes(typeof(XpathAttribute), false));
                    string typeRootXpath = xpathAttributes[0].NodeXpath;
                    HtmlNode element = htmlNode.SelectSingleNode(typeRootXpath);
                    if (element != null)
                    {
                        object item = CreateInstanceAndMapHtmlNode(propertyType, element, url);
                        property.SetValue(objectInstance, item);
                    }
                }

            }
            if (objectType.GetInterfaces().Contains(typeof(IWebcrawler)) && IsAnyValueAssigned(objectInstance)) // only adds metadata if the objectType is derived from the IClassDef interface.
            {
                objectType.GetProperty(typeof(Metadata).Name).SetValue(objectInstance, new Metadata(url, DateTime.UtcNow), null);
            }
            return objectInstance;
        }

        private static bool IsAnyValueAssigned(object myObject)
        {
            foreach (PropertyInfo pi in myObject.GetType().GetProperties())
            {
                var value = pi.GetValue(myObject);
                // Add value type handling
                if (value == null)
                {
                    continue;
                }
                if (pi.PropertyType.IsValueType && Activator.CreateInstance(pi.PropertyType).Equals(value) == true) // Check if property has default value
                {
                    continue;
                }
                return true;
            }
            return false;
        }
    }
}
