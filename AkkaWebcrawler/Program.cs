using System;
using System.Collections.Generic;
using System.IO;
using Akka.Actor;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using AkkaWebcrawler.Common.Actors;
using PortiaLib;

namespace AkkaWebcrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var webcrawlerSystem = ActorSystem.Create("WebcrawlerSystem"))
            {
                string solutionRootPath = Directory.GetCurrentDirectory();
                var json = File.ReadAllText(Path.Combine(solutionRootPath, "ArlaRequest.json"));

                JSchemaValidatingReader jSchemaReader = new JSchemaValidatingReader(new JsonTextReader(new StringReader(json)));
                jSchemaReader.Schema = JSchema.Parse(File.ReadAllText(Path.Combine(solutionRootPath, "requestSchema.json")));

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

                IActorRef project = webcrawlerSystem.ActorOf(Props.Create<ProjectActor>(projectDefinition), Common.ActorPaths.ProjectActor.Name);
                Console.ReadLine();
            }
            Console.ReadLine();

        }
    }
}
