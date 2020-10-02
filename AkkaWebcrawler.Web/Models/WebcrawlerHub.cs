using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using AkkaWebcrawler.Common.Models;
using AkkaWebcrawler.Common.Models.Deserialization;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace AkkaWebcrawler.Web.Models
{
    public class WebcrawlerHub : Hub
    {
        public void CreateProject(string json)
        {
            JsonTextReader reader = new JsonTextReader(new StringReader(json));
            JSchemaValidatingReader jSchemaReader = new JSchemaValidatingReader(reader);
            jSchemaReader.Schema = JSchema.Parse(RequestSchema.Json);
            IList<string> errorMessages = new List<string>();
            jSchemaReader.ValidationEventHandler += (o, a) => errorMessages.Add(a.Message);
            JsonSerializer serializer = new JsonSerializer();
            ProjectDefinition projectDefinition = serializer.Deserialize<ProjectDefinition>(jSchemaReader);
            if (errorMessages.Count > 0)
            {
                foreach (var eventMessage in errorMessages)
                {
                    Console.WriteLine(eventMessage);
                }
                Console.ReadKey();
                return;
            }
            SignalRScraperActorSystem
                .ActorReferences
                .SignalRBridge
                .Tell(new CreateProjectMessage(projectDefinition));
        }
    }
}