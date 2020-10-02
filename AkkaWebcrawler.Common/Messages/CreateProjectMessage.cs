using AkkaWebcrawler.Common.Models.Deserialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace AkkaWebcrawler.Common.Messages
{
    public class CreateProjectMessage
    {
        public ProjectDefinition ProjectDefinition { get; private set; }

        // TODO Add token
        public CreateProjectMessage(ProjectDefinition projectDefinition)
        {
            ProjectDefinition = projectDefinition;
        }
    }
}
