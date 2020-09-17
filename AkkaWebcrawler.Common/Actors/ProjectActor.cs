using Akka.Actor;
using AkkaWebcrawler.Common.Messages;

using PortiaLib;
using System;

namespace AkkaWebcrawler.Common.Actors
{
    public class ProjectActor : ReceiveActor
    {
        private string ProjectActorName { get; set; }
        public ProjectActor(ProjectDefinition projectDefinition)
        {
            ProjectActorName = Self.Path.Name;
            XpathConfigurationForPuppeteer crawlerConfiguration = new XpathConfigurationForPuppeteer(projectDefinition);
            Context.ActorOf(Props.Create<UrlParserActor>(), ActorPaths.UrlParserActor.Name); // TODO Add coordinator for scaling
            Context.ActorOf(Props.Create<ObjectParserActor>(projectDefinition.CrawlerSchemas), ActorPaths.ObjectParserActor.Name); // TODO Add coordinator for scaling
            Context.ActorOf(Props.Create<BrowserActor>(crawlerConfiguration), ActorPaths.BrowserActor.Name); // TODO Add coordinator for scaling

            #region SingleTons
            IActorRef urlTracker = Context.ActorOf(Props.Create<UrlTrackerActor>(projectDefinition), ActorPaths.UrlTrackerActor.Name); // there can NEVER be more than ONE instance!!!!
            Context.ActorOf(Props.Create<ObjectTrackerActor>(projectDefinition.CrawlerSchemas), ActorPaths.ObjectTrackerActor.Name); // there can NEVER be more than ONE instance!!!!
            #endregion
            urlTracker.Tell(new UnprocessedUrls(projectDefinition.StartUrls)); // START URL
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
