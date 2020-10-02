using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using AkkaWebcrawler.Common.Models;
using AkkaWebcrawler.Common.Models.Deserialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Actors
{
    public class UrlTrackerActor : ReceiveActor // TODO Should be persistent actor.
    {
        private string UrlTrackerActorName { get; set; }
        private List<Uri> ValidUrls { get; set; }
        private List<Uri> ProcessedUrls { get; set; }
        // TODO Add UrlsVisited logging
        private ProjectDefinition ProjectDefinition { get; set; }
        public UrlTrackerActor(ProjectDefinition projectDefinition)
        {
            UrlTrackerActorName = Self.Path.Name;
            ProjectDefinition = projectDefinition;
            ValidUrls = new List<Uri>();
            ProcessedUrls = new List<Uri>();
            //Task.Run(() => RenderCrawling());
        }
        
        private void Ready()
        {

            ColorConsole.WriteLine($"{UrlTrackerActorName} has become Ready", ConsoleColor.Red);
            Receive<UnprocessedUrlsMessage>(message =>
            {
                List<Uri> distinctUrls = message.Urls.Distinct().ToList(); // TODO Can ToLower() be implemented? or will it result in false positives when visiting the Urls later? And what about case sensitive urls?
                foreach (var url in distinctUrls)
                {
                    #region Checks if url is valid
                    bool isUrlFromProjectDomain = url.OriginalString.Contains(ProjectDefinition.Domain.OriginalString);
                    // bool doesUrlContainAnyDisallowedWords = disallowedWords.Any(url.ToString().Contains); //TODO Add check for disallowed words?
                    if (isUrlFromProjectDomain == false ||
                        ValidUrls.Contains(url) == true)
                    {
                        continue;
                    }
                    ValidUrls.Add(url);
                    #endregion

                    #region Creates messages based on the project definition.
                    if (ProjectDefinition.IsFixedListOfUrls) // Move to 'Shepherd' Actor?!
                    {
                        if (ProjectDefinition.StartUrls.Contains(url))
                        {
                            Context.ActorSelection(ActorPaths.Browser).Tell(new UrlForUrlAndObjectParsingMessage(url)); // BrowserActor is load balancer 
                        }
                        else
                        {
                            Context.ActorSelection(ActorPaths.Browser).Tell(new UrlForObjectParsingMessage(url)); // BrowserActor is load balancer 
                        }
                    }
                    else
                    {
                        Context.ActorSelection(ActorPaths.Browser).Tell(new UrlForUrlAndObjectParsingMessage(url)); // BrowserActor is load balancer 
                    }
                    #endregion
                }
            });
            Receive<ProcessedUrlMessage>(message =>
            {
                ProcessedUrls.Add(message.Url);
            });

            Receive<UrlForUrlAndObjectParsingMessage>(message =>
            {

            });
        }
        //public async Task RenderCrawling(int fps = 30)
        //{

        //    while (true)
        //    {
        //        string format = $"\nurls validated:                      = {ValidUrls.Count}    " +
        //                        $"\nurls visited:                        = {VisitedUrls.Count}    ";
        //        //$"\nobjs parsed:                         = {ObjectsByScraperSchemaName.Values.Sum(x => x.Count)}    ";
        //        ColorConsole.WriteLine(format, ConsoleColor.DarkYellow);
        //        await Task.Delay((int)TimeSpan.FromSeconds(1).TotalMilliseconds / fps);
        //    }
        //}

        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{UrlTrackerActorName} PreStart", ConsoleColor.Red);
            Become(Ready);
        }

        protected override void PostStop()
        {
            ColorConsole.WriteLine($"{UrlTrackerActorName} PostStop", ConsoleColor.Red);
        }
        #endregion
    }
}
