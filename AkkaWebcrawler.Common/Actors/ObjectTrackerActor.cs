using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using AkkaWebcrawler.Common.Models.Deserialization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace AkkaWebcrawler.Common.Actors
{
    public class ObjectTrackerActor : ReceiveActor // TODO Should be persistent actor.
    {
        private string ObjectTrackerActorName { get; set; }
        private Dictionary<string, JArray> ObjectsByScraperSchemaName { get; set; } = new Dictionary<string, JArray>();
        public ObjectTrackerActor(List<ScraperSchema> scraperSchemas)
        {
            ObjectTrackerActorName = Self.Path.Name;
            scraperSchemas.ForEach((scraperSchema) =>
            {
                ObjectsByScraperSchemaName.Add(scraperSchema.Name, new JArray());
            });
        }

        private void Ready()
        {
            ColorConsole.WriteLine($"{ObjectTrackerActorName} has become Ready", ConsoleColor.Yellow);
            Receive<ObjectContentMessage>(message => // ObjectValidator
            {
                //TODO Check if object already exists
                ObjectsByScraperSchemaName[message.ScraperSchemaName].Add(message.JObject);
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
