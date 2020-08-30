using Akka.Actor;
using Akka.Dispatch;
using AkkaWebcrawler.Common.Messages;
using Newtonsoft.Json;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Actors
{
    public class BrowserActor : ReceiveActor
    {
        private int AmountOfPages { get; set; } = 1;
        private Browser Browser { get; set; }
        private string BrowserActorName { get; set; }
        private Queue<IActorRef> PageActors { get; set; }
        private XpathConfigurationForPuppeteer XpathConfiguration { get; set; }
        public BrowserActor(XpathConfigurationForPuppeteer xpathConfiguration)
        {
            XpathConfiguration = xpathConfiguration;
            BrowserActorName = Self.Path.Name;
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
            //Browser = await Puppeteer.ConnectAsync(new ConnectOptions() { BrowserWSEndpoint = "wss://chrome.browserless.io", IgnoreHTTPSErrors = true });
            string webSocket = Browser.WebSocketEndpoint;
            for (int i = 0; i < AmountOfPages; i++)
            {
                IActorRef page = Context.ActorOf(Props.Create<PageActor>(webSocket, XpathConfiguration), $"Page_{i}");
                PageActors.Enqueue(page);
            }
        }

        private void Ready()
        {
            ColorConsole.WriteLine($"{BrowserActorName} has become Ready", ConsoleColor.Green);
            Receive<UrlAndObjectParsing>(validUrl =>
            {
                IActorRef page = PageActors.Dequeue();
                page.Tell(validUrl);
                PageActors.Enqueue(page);
            });
            Receive<ObjectParsing>(validUrl =>
            {
                IActorRef page = PageActors.Dequeue();
                page.Tell(validUrl);
                PageActors.Enqueue(page);
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

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                exception =>
                {
                    if (exception is PuppeteerException)
                    {
                        if (exception is ProcessException) // B
                        {

                        }
                        if (exception is NavigationException) // Page was closed
                        {
                            return Directive.Restart;
                        }
                    }
                    if (exception is NullReferenceException)
                    {
                        return Directive.Escalate;
                    }
                    return Directive.Resume;
                });
        }
        #endregion
    }
}
