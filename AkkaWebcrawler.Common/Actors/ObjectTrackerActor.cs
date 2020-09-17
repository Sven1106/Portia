using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using Newtonsoft.Json.Linq;
using PortiaLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace AkkaWebcrawler.Common.Actors
{
    public class ObjectTrackerActor : ReceiveActor // TODO Should be persistent actor.
    {
        private string ObjectTrackerActorName { get; set; }
        private Dictionary<string, JArray> ObjectsByCrawlerSchemaName { get; set; } = new Dictionary<string, JArray>();
        public ObjectTrackerActor(List<CrawlerSchema> crawlerSchemas)
        {
            ObjectTrackerActorName = Self.Path.Name;
            crawlerSchemas.ForEach((crawlerSchema) =>
            {
                ObjectsByCrawlerSchemaName.Add(crawlerSchema.Name, new JArray());
            });
        }

        private void Ready()
        {
            ColorConsole.WriteLine($"{ObjectTrackerActorName} has become Ready", ConsoleColor.Yellow);
            Receive<CrawledObjectContent>(message => // ObjectValidator
            {
                ObjectsByCrawlerSchemaName[message.CrawlerSchemaName].Add(message.CrawledObject);
            });

        }

        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{ObjectTrackerActorName} PreStart", ConsoleColor.Yellow);
            Become(Ready);
        }

        protected override void PostStop()
        {
            ColorConsole.WriteLine($"{ObjectTrackerActorName} PostStop", ConsoleColor.Yellow);
        }

        #endregion
    }
}
