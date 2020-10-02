using Akka.Actor;
using AkkaWebcrawler.Common.Actors;
using AkkaWebcrawler.Common.Interfaces;
using AkkaWebcrawler.Common.Models;
using System;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Web.Models
{

    public static class SignalRScraperActorSystem
    {
        private static ActorSystem _signalRScraperActorSystem;
        private static IScraperEventsPusher _signalRWebcrawlerEventsPusher;

        public static async Task Create()
        {
            _signalRWebcrawlerEventsPusher = new SignalRWebcrawlerEventsPusher();
            _signalRScraperActorSystem = ActorSystem.Create("SignalRScraperSystem");
            ActorReferences.ProjectCoordinator = await _signalRScraperActorSystem.ActorSelection("akka.tcp://ScraperSystem@127.0.0.1:9091/user/ProjectCoordinator")
                .ResolveOne(TimeSpan.FromSeconds(3)); 
            ActorReferences.SignalRBridge = _signalRScraperActorSystem.ActorOf(
                Props.Create(() => new SignalRBridgeActor(_signalRWebcrawlerEventsPusher, ActorReferences.ProjectCoordinator)),
                ActorPaths.SignalRBridge.Name
            );
        }
        public static async Task Shutdown()
        {
            await _signalRScraperActorSystem.Terminate();
        }
        public static class ActorReferences
        {
            public static IActorRef ProjectCoordinator { get; set; }
            public static IActorRef SignalRBridge { get; set; }
        }
    }
}