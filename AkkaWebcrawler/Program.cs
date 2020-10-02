using System;
using Akka.Actor;
using AkkaWebcrawler.Common.Actors;
using AkkaWebcrawler.Common.Models;

namespace AkkaWebcrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var webcrawlerSystem = ActorSystem.Create("ScraperSystem"))
            {
                IActorRef projectCoordinator = webcrawlerSystem.ActorOf(Props.Create<ProjectCoordinatorActor>(), ActorPaths.ProjectCoordinator.Name);
                Console.ReadLine();
            }
            Console.ReadLine();

        }
    }
}
