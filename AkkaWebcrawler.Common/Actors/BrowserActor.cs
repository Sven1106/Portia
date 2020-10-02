using Akka.Actor;
using Akka.Dispatch;
using AkkaWebcrawler.Common.Messages;
using AkkaWebcrawler.Common.Models;
using Newtonsoft.Json;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Actors
{
    public class BrowserActor : ReceiveActor
    {
        private int AmountOfPages { get; set; }
        private Browser Browser { get; set; }
        private string BrowserActorName { get; set; }
        private Queue<IActorRef> PageActors { get; set; }
        private XpathConfigurationForPuppeteer XpathConfiguration { get; set; }
        public BrowserActor(XpathConfigurationForPuppeteer xpathConfiguration)
        {
            XpathConfiguration = xpathConfiguration;
            BrowserActorName = Self.Path.Name;
            AmountOfPages = 3;
            // TODO Add Chromium instance handling
            // TODO Add Chromium instance handling
            // TODO Add Chromium instance handling
            // TODO Add Chromium instance handling
            // TODO Add Chromium instance handling
        }

        private async Task PrepareBrowserInstance()
        {
            PageActors = new Queue<IActorRef>();
            // TODO Add integration to browserless
            // TODO Add integration to docker
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var args = new string[] {
                    "--no-sandbox",
                    "--disable-plugins",
                    "--disable-sync",
                    "--disable-gpu",
                    "--disable-speech-api",
                    "--disable-remote-fonts",
                    "--disable-shared-workers",
                    "--disable-webgl",
                    "--no-experiments",
                    "--no-first-run",
                    "--no-default-browser-check",
                    "--no-wifi",
                    "--no-pings",
                    "--no-service-autorun",
                    "--disable-databases",
                    "--disable-default-apps",
                    "--disable-demo-mode",
                    "--disable-notifications",
                    "--disable-permissions-api",
                    "--disable-background-networking",
                    "--disable-3d-apis",
                    "--disable-bundled-ppapi-flash"
                };
            var launchOptions = new LaunchOptions { Headless = false, Args = args, IgnoreHTTPSErrors = true };
            Browser = await Puppeteer.LaunchAsync(launchOptions);
            // Browser = await Puppeteer.ConnectAsync(new ConnectOptions() { BrowserWSEndpoint = "wss://chrome.browserless.io", IgnoreHTTPSErrors = true });
            string webSocket = Browser.WebSocketEndpoint;
            for (int i = 0; i < AmountOfPages; i++)
            {
                IActorRef page = Context.ActorOf(Props.Create<PageActor>(webSocket, XpathConfiguration), ActorPaths.Page.Name);
                PageActors.Enqueue(page);
            }
        }

        private void Ready()
        {
            ColorConsole.WriteLine($"{BrowserActorName} has become Ready", ConsoleColor.Green);
            Receive<UrlForUrlAndObjectParsingMessage>(urlForUrlAndObjectParsing =>
            {
                IActorRef page = PageActors.Dequeue();
                page.Forward(urlForUrlAndObjectParsing);
                PageActors.Enqueue(page);
            });
            Receive<UrlForObjectParsingMessage>(urlForObjectParsing =>
            {
                IActorRef page = PageActors.Dequeue();
                page.Forward(urlForObjectParsing);
                PageActors.Enqueue(page);
            });
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {

            return new OneForOneStrategy(
                exception =>
                {
                    if (exception is PuppeteerException)
                    {
                        if (exception is ProcessException)
                        {

                        }
                        else if (exception is EvaluationFailedException)
                        {

                        }
                        else if (exception is MessageException)
                        {

                        }
                        else if (exception is NavigationException) // Couldn't Navigate to url. Or Browser was disconnected //Target.detachedFromTarget
                        {
                            return Directive.Restart;
                        }
                        else if (exception is SelectorException)
                        {

                        }
                        else if (exception is TargetClosedException) // Page was closed
                        {
                            return Directive.Restart;
                        }
                    }
                    else if (exception is NullReferenceException)
                    {
                        return Directive.Escalate;
                    }
                    return Directive.Resume;
                });
        }

        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{BrowserActorName} PreStart", ConsoleColor.Green);
            ActorTaskScheduler.RunTask(async () =>
            {
                await PrepareBrowserInstance();
                Become(Ready);
            });
        }

        protected override void PostStop()
        {
            ActorTaskScheduler.RunTask(async () =>
            {
                if (Browser != null)
                {
                    await Browser.DisposeAsync();
                }
            });
            ColorConsole.WriteLine($"{BrowserActorName} PostStop", ConsoleColor.Green);
        }
        #endregion
    }
}
