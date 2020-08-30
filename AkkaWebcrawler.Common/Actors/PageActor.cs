﻿using Akka.Actor;
using Akka.Dispatch;
using AkkaWebcrawler.Common.Messages;
using PortiaLib;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Actors
{
    public class PageActor : ReceiveActor
    {
        private Page Page { get; set; }
        private string PageActorName { get; set; }
        private string WebSocket { get; set; }
        private XpathConfigurationForPuppeteer Configuration { get; set; }
        public PageActor(string webSocket, XpathConfigurationForPuppeteer configuration)
        {
            PageActorName = Self.Path.Name;
            WebSocket = webSocket;
            Configuration = configuration;
            ActorTaskScheduler.RunTask(async () =>
            {
                await PreparePage();
            });
        }

        private async Task PreparePage()
        {

            Browser browser = await Puppeteer.ConnectAsync(new ConnectOptions() { BrowserWSEndpoint = WebSocket, IgnoreHTTPSErrors = true });
            Page = await browser.NewPageAsync();
            await Page.SetViewportAsync(new ViewPortOptions() { Width = 1920, Height = 1080 });
            await Page.SetRequestInterceptionAsync(true); // Intercepting the page seems to finish it prematurely
            Page.Request += async (sender, e) =>
            {
                try
                {
                    switch (e.Request.ResourceType)
                    {
                        case ResourceType.Font:
                        case ResourceType.EventSource:
                        case ResourceType.Image:
                        case ResourceType.Manifest:
                        case ResourceType.Media:
                        case ResourceType.Other:
                        case ResourceType.Ping:
                        case ResourceType.TextTrack:
                        case ResourceType.Unknown:
                            await e.Request.AbortAsync();
                            break;
                        case ResourceType.StyleSheet:
                        case ResourceType.Document:
                        case ResourceType.Fetch:
                        case ResourceType.Script:
                        case ResourceType.WebSocket:
                        case ResourceType.Xhr:
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
        }

        private void Ready()
        {
            ColorConsole.WriteLine($"{PageActorName} has become Ready", ConsoleColor.Magenta);
            ReceiveAsync<UrlAndObjectParsing>(async url =>
            {
                ColorConsole.WriteLine($"{PageActorName} processing: {url.Url}", ConsoleColor.Magenta);
                HtmlContent htmlContent = await GetHtmlContent(url.Url);
                ColorConsole.WriteLine($"{PageActorName} completed: {url.Url}", ConsoleColor.Magenta);
                Context.ActorSelection(ActorPaths.UrlParserActor.Path).Tell(htmlContent);
                Context.ActorSelection(ActorPaths.ObjectParserActor.Path).Tell(htmlContent);
                Context.ActorSelection(ActorPaths.ProjectActor.Path).Tell(new VisitedUrl(htmlContent.SourceUrl));
            });
            ReceiveAsync<ObjectParsing>(async url =>
            {
                ColorConsole.WriteLine($"{PageActorName} processing: {url.Url}", ConsoleColor.Magenta);
                HtmlContent htmlContent = await GetHtmlContent(url.Url);
                ColorConsole.WriteLine($"{PageActorName}  completed: {url.Url}", ConsoleColor.Magenta);
                Context.ActorSelection(ActorPaths.ObjectParserActor.Path).Tell(htmlContent);
                Context.ActorSelection(ActorPaths.ProjectActor.Path).Tell(new VisitedUrl(htmlContent.SourceUrl));
            });

        }

        private async Task<HtmlContent> GetHtmlContent(Uri url)
        {
            await Page.GoToAsync(url.ToString(), WaitUntilNavigation.Networkidle0);
            await InvokeXpathConfiguration();
            string html = await Page.GetContentAsync();
            return new HtmlContent(url, html);
        }

        private async Task InvokeXpathConfiguration() // TODO REFACTOR
        {
            bool isAcceptCookiesButtonVisible = await ScrollPageUntilHtmlElementIsVisible(Configuration.XpathForAcceptCookiesButton);
            if (isAcceptCookiesButtonVisible)
            {
                try
                {
                    var acceptCookiesButtonVisible = await Page.WaitForXPathAsync(Configuration.XpathForAcceptCookiesButton,
                        new WaitForSelectorOptions { Visible = true, Timeout = 30000 }
                    );
                    await acceptCookiesButtonVisible.ClickAsync(new ClickOptions() { Delay = 1000 });
                }
                catch (PuppeteerException ex) when (ex is WaitTaskTimeoutException)
                {

                }
            }
            bool isLoadMoreButtonVisible = await ScrollPageUntilHtmlElementIsVisible(Configuration.XpathForLoadMoreButton);
            if (isLoadMoreButtonVisible)
            {
                bool hasMoreContent = true;
                while (hasMoreContent)
                {
                    try
                    {
                        var loadMoreButton = await Page.WaitForXPathAsync(Configuration.XpathForLoadMoreButton,
                            new WaitForSelectorOptions { Visible = true, Timeout = 30000 }
                        );
                        await loadMoreButton.ClickAsync(new ClickOptions() { Delay = 1000 });
                    }
                    catch (PuppeteerException ex) when (ex is WaitTaskTimeoutException)
                    {
                        hasMoreContent = false;
                    }
                }
            }
            foreach (var xpath in Configuration.AbsoluteXpathsForElementsToEnsureExist)
            {
                bool isElementVisible = await ScrollPageUntilHtmlElementIsVisible(xpath);
                if (isElementVisible == false) // xpathsForDesiredHtmlElements has a treestructure, so we want to break out of the loop as soon as possible when not found.
                {
                    break;
                }
            }
        }

        private async Task<bool> ScrollPageUntilHtmlElementIsVisible(string xpath) // TODO REFACTOR
        {
            if (xpath != null)
            {
                int attempt = 1;
                int attemptLimit = 3; // TODO Should be configurable
                int currentHeight = (int)await Page.EvaluateExpressionAsync("document.body.scrollHeight");
                while (attempt <= attemptLimit)
                {
                    try
                    {
                        await Page.WaitForXPathAsync(xpath, new WaitForSelectorOptions { Visible = true, Timeout = 2500 });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        int newHeight = (int)await Page.EvaluateExpressionAsync("document.body.scrollHeight");
                        await Page.EvaluateExpressionAsync("window.scrollBy({top:" + newHeight + ",behavior:'smooth'})"); // To trigger autoload of new content.
                        if (currentHeight == newHeight)
                        {
                            break;
                        }
                        currentHeight = newHeight;
                    }
                    attempt++;
                }
            }
            return false;
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


        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{PageActorName} PreStart", ConsoleColor.Magenta);
            Become(Ready);
        }
        protected override void PreRestart(Exception reason, object message)
        {
            Self.Tell(message); // TODO Find a better way to handle messages from exceptions. Send to ProjectActor?
            base.PreRestart(reason, message);
        }

        protected override void PostStop()
        {
            ActorTaskScheduler.RunTask(async () =>
            {
                if (Page != null)
                {
                    await Page.DisposeAsync();
                }
            });
            ColorConsole.WriteLine($"{PageActorName} PostStop", ConsoleColor.Magenta);
        }

        #endregion
    }
}
