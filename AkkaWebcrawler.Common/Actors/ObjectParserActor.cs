using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PortiaLib;
using System;
using System.Collections.Generic;
using System.Xml.XPath;

namespace AkkaWebcrawler.Common.Actors
{
    public class ObjectParserActor : ReceiveActor
    {
        private string ObjectParserActorName { get; set; }
        private List<CrawlerSchema> CrawlerSchemas { get; set; }

        public ObjectParserActor(List<CrawlerSchema> crawlerSchemas)
        {
            CrawlerSchemas = crawlerSchemas;
            ObjectParserActorName = Self.Path.Name;
        }
        private void Ready()
        {
            ColorConsole.WriteLine($"{ObjectParserActorName} has become Ready", ConsoleColor.White);
            Receive<HtmlContent>(htmlContent =>
            {
                ColorConsole.WriteLine($"{ObjectParserActorName} started parsing objects from: {htmlContent.SourceUrl}", ConsoleColor.White);

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent.Html);
                HtmlNode documentNode = htmlDoc.DocumentNode;
                foreach (CrawlerSchema crawlerSchema in CrawlerSchemas) // TODO Find a better way to reference the job schemas
                {
                    JObject objectFound = new JObject();
                    foreach (NodeAttribute node in crawlerSchema.Nodes)
                    {
                        JToken value = GetValueForJTokenRecursively(node, documentNode);
                        if (value.ToString() == "")
                        {
                            continue;
                        }
                        objectFound.Add(node.Name, value);
                        Metadata metadata = new Metadata(htmlContent.SourceUrl.ToString(), DateTime.UtcNow);
                        objectFound.Add("metadata", JObject.FromObject(metadata, new JsonSerializer()
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        }));
                    }
                    if (objectFound.HasValues == false)
                    {
                        continue;
                    }
                    CrawledObjectContent crawledObjectContent = new CrawledObjectContent(crawlerSchema.Name, objectFound);
                    Context.ActorSelection(ActorPaths.ObjectTrackerActor.Path).Tell(crawledObjectContent);
                }
                ColorConsole.WriteLine($"{ObjectParserActorName} finished parsing objects from: {htmlContent.SourceUrl}", ConsoleColor.White);
            });
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

        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{ObjectParserActorName} Prestart", ConsoleColor.White);
            Become(Ready);
        }

        protected override void PostStop()
        {
            ColorConsole.WriteLine($"{ObjectParserActorName} PostStop", ConsoleColor.White);
        }
        #endregion
    }
}
