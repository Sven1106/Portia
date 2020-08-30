using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using Newtonsoft.Json.Linq;
using PortiaLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Actors
{
    public class ProjectActor : ReceiveActor
    {
        private string ProjectActorName { get; set; }
        private List<Uri> ValidUrls { get; set; } = new List<Uri>();
        private List<Uri> VisitedUrls { get; set; } = new List<Uri>();
        public ProjectDefinition ProjectDefinition { get; private set; }
        // TODO Add UrlsVisited logging

        private Dictionary<string, JArray> ObjectsByCrawlerSchemaName { get; set; } = new Dictionary<string, JArray>();

        private Queue<IActorRef> BrowserActors { get; set; } = new Queue<IActorRef>();
        public ProjectActor(ProjectDefinition projectDefinition)
        {
            ProjectActorName = Self.Path.Name;
            ProjectDefinition = projectDefinition;
            ProjectDefinition.CrawlerSchemas.ForEach((crawlerSchema) =>
            {
                ObjectsByCrawlerSchemaName.Add(crawlerSchema.Name, new JArray());
            });
            XpathConfigurationForPuppeteer crawlerConfiguration = new XpathConfigurationForPuppeteer(ProjectDefinition);
            Context.ActorOf(Props.Create<UrlParserActor>(), ActorPaths.UrlParserActor.Name); // TODO Add coordinator for scaling
            Context.ActorOf(Props.Create<ObjectParserActor>(ProjectDefinition.CrawlerSchemas), ActorPaths.ObjectParserActor.Name); // TODO Add coordinator for scaling
            IActorRef browserActor = Context.ActorOf(Props.Create<BrowserActor>(crawlerConfiguration), ActorPaths.BrowserParserActor.Name); // TODO Add coordinator for scaling
            BrowserActors.Enqueue(browserActor);
            Self.Tell(new ParsedUrls(ProjectDefinition.StartUrls)); // START URL
        }


        public async Task RenderCrawling(int fps = 30)
        {

            while (true)
            {
                string format = $"\nurls validated:                      = {ValidUrls.Count}    " +
                                $"\nurls visited:                        = {VisitedUrls.Count}    " +
                                $"\nobjs parsed:                         = {ObjectsByCrawlerSchemaName.Values.Sum(x => x.Count)}    ";
                ColorConsole.WriteLine(format, ConsoleColor.DarkYellow);
                await Task.Delay((int)TimeSpan.FromSeconds(1).TotalMilliseconds / fps);
            }
        }
        private void Ready()
        {
            ColorConsole.WriteLine($"{ProjectActorName} has become Ready", ConsoleColor.DarkYellow);


            // Since ParsedUrls, CrawledObjectContent and VisitedUrl are all handled by the ProjectActor, this will result in some overhead. TODO Shouldn't this be moved to seperate actors?!
            Receive<ParsedUrls>(message => // UrlValidator
            {
                IActorRef browserActor = BrowserActors.Dequeue();
                List<Uri> destinctUrls = message.Urls.Distinct().ToList(); // Can ToLower() be implemented? or will it result in false positives when visiting the Urls later?
                foreach (var url in destinctUrls)
                {
                    bool isUrlFromSameDomainAsRootUrl = url.OriginalString.Contains(ProjectDefinition.Domain.OriginalString);
                    if (isUrlFromSameDomainAsRootUrl == false ||
                        ValidUrls.Contains(url) == true)
                    {
                        continue;
                    }
                    ValidUrls.Add(url);

                    if (ProjectDefinition.IsFixedListOfUrls)
                    {
                        if (ProjectDefinition.StartUrls.Contains(url))
                        {
                            browserActor.Tell(new UrlAndObjectParsing(url));
                        }
                        else
                        {
                            browserActor.Tell(new ObjectParsing(url));
                        }
                    }
                    else
                    {
                        browserActor.Tell(new UrlAndObjectParsing(url));
                    }
                }
                BrowserActors.Enqueue(browserActor);

            });
            Receive<CrawledObjectContent>(message => // ObjectValidator
            {
                ObjectsByCrawlerSchemaName[message.CrawlerSchemaName].Add(message.CrawledObject);
            });
            Receive<VisitedUrl>(message =>
            {
                VisitedUrls.Add(message.Url);
            });
        }


        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{ProjectActorName} PreStart", ConsoleColor.DarkYellow);
            Become(Ready);
        }

        protected override void PostStop()
        {
            ColorConsole.WriteLine($"{ProjectActorName} PostStop", ConsoleColor.DarkYellow);
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                exception =>
                {
                    return Directive.Restart;
                });
        }
        #endregion
    }
}
