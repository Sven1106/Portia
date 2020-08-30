using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Actors
{
    public class UrlParserActor : ReceiveActor
    {
        private string UrlParserActorName { get; set; }
        public UrlParserActor()
        {
            UrlParserActorName = Self.Path.Name;
        }

        private void Ready()
        {
            ColorConsole.WriteLine($"{UrlParserActorName} has become Ready", ConsoleColor.Cyan);
            Receive<HtmlContent>(htmlContent =>
            {
                ParsedUrls parsedUrls = ParseUrlsFromHtmlContent(htmlContent);
                Context.ActorSelection(ActorPaths.ProjectActor.Path).Tell(parsedUrls);
            });
        }

        private ParsedUrls ParseUrlsFromHtmlContent(HtmlContent htmlContent)
        {
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent.Html);
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
                        url = new Uri(htmlContent.SourceUrl, url);
                        urlsFound.Add(url);
                    }
                }
            }
            return new ParsedUrls(urlsFound);
        }

        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{UrlParserActorName} Prestart", ConsoleColor.Cyan);
            Become(Ready);
        }

        protected override void PostStop()
        {
            ColorConsole.WriteLine($"{UrlParserActorName} PostStop", ConsoleColor.Cyan);
        }
        #endregion
    }
}
