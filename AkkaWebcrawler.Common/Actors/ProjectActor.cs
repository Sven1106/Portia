using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using AkkaWebcrawler.Common.Models;
using AkkaWebcrawler.Common.Models.Deserialization;
using System;
using System.Collections.Generic;

namespace AkkaWebcrawler.Common.Actors
{
    public class ProjectActor : ReceiveActor
    {
        private string ProjectActorName { get; set; }
        public ProjectActor(ProjectDefinition projectDefinition)
        {
            ProjectActorName = Self.Path.Name;
            List<ScraperSchema> scraperSchemas = projectDefinition.ScraperSchemas;
            XpathConfigurationForPuppeteer crawlerConfiguration = new XpathConfigurationForPuppeteer(projectDefinition);
            Context.ActorOf(Props.Create<UrlParserActor>(), ActorPaths.UrlParser.Name); // TODO Add coordinator for scaling
            Context.ActorOf(Props.Create<ObjectParserActor>(scraperSchemas), ActorPaths.ObjectParser.Name); // TODO Add coordinator for scaling
            Context.ActorOf(Props.Create<BrowserActor>(crawlerConfiguration), ActorPaths.Browser.Name); // TODO Add coordinator for scaling

            #region SingleTons
            IActorRef urlTracker = Context.ActorOf(Props.Create<UrlTrackerActor>(projectDefinition), ActorPaths.UrlTracker.Name); // there can NEVER be more than ONE instance!!!!
            Context.ActorOf(Props.Create<ObjectTrackerActor>(scraperSchemas), ActorPaths.ObjectTracker.Name); // there can NEVER be more than ONE instance!!!!
            #endregion
            urlTracker.Tell(new UnprocessedUrlsMessage(projectDefinition.StartUrls)); // START URL
        }

        private void Ready()
        {
            ColorConsole.WriteLine($"{ProjectActorName} has become Ready", ConsoleColor.DarkYellow);           
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
