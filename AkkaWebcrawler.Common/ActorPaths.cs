using System;
using System.Collections.Generic;
using System.Text;

namespace AkkaWebcrawler.Common
{
    /// <summary>
    /// Static helper class used to define paths to fixed-name actors
    /// (helps eliminate errors when using <see cref="ActorSelection"/>)
    /// </summary>
    public static class ActorPaths
    {
        public static readonly ActorMetaData ProjectActor = new ActorMetaData("Project");
        public static readonly ActorMetaData UrlParserActor = new ActorMetaData("UrlParser", ProjectActor);
        public static readonly ActorMetaData ObjectParserActor = new ActorMetaData("ObjectParser", ProjectActor);
        public static readonly ActorMetaData BrowserParserActor = new ActorMetaData("Browser", ProjectActor);
    }
}
