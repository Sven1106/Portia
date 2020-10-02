using Akka.Actor;
using AkkaWebcrawler.Common.Messages;
using AkkaWebcrawler.Common.Models;
using System;
using System.Collections.Generic;

namespace AkkaWebcrawler.Common.Actors
{
    public class ProjectCoordinatorActor : ReceiveActor
    {
        private string ProjectCoordinatorActorName { get; set; }
        private Dictionary<Guid, IActorRef> Projects { get; set; }
        public ProjectCoordinatorActor()
        {
            ProjectCoordinatorActorName = Self.Path.Name;
            Projects = new Dictionary<Guid, IActorRef>();
        }
        private void Ready()
        {
            ColorConsole.WriteLine($"{ProjectCoordinatorActorName} has become Ready", ConsoleColor.DarkGreen);
            Receive<CreateProjectMessage>(message =>
            {
                Guid projectId = message.ProjectDefinition.ProjectId;
                bool projectExists = Projects.ContainsKey(projectId);
                if (projectExists == false)
                {
                    IActorRef newProject = Context.ActorOf(Props.Create<ProjectActor>(message.ProjectDefinition), ActorPaths.Project.Name);
                    Projects.Add(projectId, newProject);
                }
            });
        }


        #region Lifecycle Hooks
        protected override void PreStart()
        {
            ColorConsole.WriteLine($"{ProjectCoordinatorActorName} PreStart", ConsoleColor.DarkGreen);
            Become(Ready);
        }

        protected override void PostStop()
        {
            ColorConsole.WriteLine($"{ProjectCoordinatorActorName} PostStop", ConsoleColor.DarkGreen);
        }
        #endregion
    }
}