using Akka.Actor;
using AkkaWebcrawler.Common.Interfaces;
using AkkaWebcrawler.Common.Messages;

namespace AkkaWebcrawler.Common.Actors
{
    public class SignalRBridgeActor : ReceiveActor // This is used to bridge/connect SignalR with the actor system.
    {
        private readonly IActorRef _projectCoordinator;
        private readonly IScraperEventsPusher _webcrawlerEventsPusher;

        public SignalRBridgeActor(IScraperEventsPusher webcrawlerEventsPusher, IActorRef projectCoordinator)
        {
            _webcrawlerEventsPusher = webcrawlerEventsPusher;
            _projectCoordinator = projectCoordinator;

            Receive<CreateProjectMessage>(message =>
            {
                _projectCoordinator.Tell(message);
            });
            Receive<ProjectCreatedMessage>(message => {
                _webcrawlerEventsPusher.ProjectCreated();
            });
        }
    }
}
