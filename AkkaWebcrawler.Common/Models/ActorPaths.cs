using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AkkaWebcrawler.Common.Models
{
    /// <summary>
    /// Static helper class used to define paths to fixed-name actors
    /// (helps eliminate errors when using <see cref="ActorSelection"/>)
    /// </summary>
    public static class ActorPaths
    {
        //ActorMeta has to be initialized in the correct order to work!!!
        //ActorMeta has to be initialized in the correct order to work!!!
        //ActorMeta has to be initialized in the correct order to work!!!
        //ActorMeta has to be initialized in the correct order to work!!!
        //ActorMeta has to be initialized in the correct order to work!!!
        #region SignalRWebcrawlerActorSystem
        public static readonly ActorMetaData SignalRBridge = new ActorMetaData("SignalRBridge");
        #endregion
        #region MyRegion

        #endregion
        public static readonly ActorMetaData ProjectCoordinator = new ActorMetaData("ProjectCoordinator");
        public static readonly ActorMetaData Project = new ActorMetaData("Project", ProjectCoordinator, true);
        public static readonly ActorMetaData UrlParser = new ActorMetaData("UrlParser", Project);
        public static readonly ActorMetaData UrlTracker = new ActorMetaData("UrlTracker", Project);
        public static readonly ActorMetaData ObjectParser = new ActorMetaData("ObjectParser", Project);
        public static readonly ActorMetaData ObjectTracker = new ActorMetaData("ObjectTracker", Project);
        public static readonly ActorMetaData Browser = new ActorMetaData("Browser", Project);
        public static readonly ActorMetaData Page = new ActorMetaData("Page", Browser, true);


        public static ActorSelection ActorSelection(this IUntypedActorContext context, ActorMetaData targetActorMetaData)
        {
            var pathWithOutAdress = context.Self.Path.ToString().Replace(context.Self.Path.Address.ToString(), "");
            var source = pathWithOutAdress.TrimStart('/').Split('/').ToList();
            source.Reverse();

            var target = new List<string>();
            var currentActorMetaData = targetActorMetaData;
            while (currentActorMetaData != null)
            {
                if (currentActorMetaData.Name != null)
                {
                    target.Add(currentActorMetaData.Name);
                }
                else
                {
                    target.Add(null);
                }
                currentActorMetaData = currentActorMetaData.Parent;
            }
            target.Add("user");
            string relativePath = CreateInterpretedRelativePath(source, target);
            return context.ActorSelection(relativePath);
        }

        private static string CreateInterpretedRelativePath(List<string> sourcePathNames, List<string> targetPathNames)
        {

            var sourcePaths = sourcePathNames.Select((value, index) => new { value, index });
            var targetPaths = targetPathNames.Select((value, index) => new { value, index });
            var matches = (from pair1 in sourcePaths
                          join pair2 in targetPaths on pair1.value equals pair2.value
                          select new
                          {
                              Value = pair1.value,
                              IndexInSource = pair1.index,
                              IndexInTarget = pair2.index
                          }).ToList();
            var firstPathInCommon = matches.FirstOrDefault();


            // TODO Should there be added an interpreter for when target is further down the hierarchy than the source
            // TODO See what happens if the TargetPathName is an actor with a custom ID or a build in ID?
            string finalPathName = "../" + targetPathNames.FirstOrDefault();
            for (int i = 0; i < firstPathInCommon.IndexInSource - firstPathInCommon.IndexInTarget; i++)
            {
                finalPathName = "../" + finalPathName;
            }
            return finalPathName;
        }
    }
    /// <summary>
    /// Meta-data class. Nested/child actors can build path 
    /// based on their parent(s) / position in hierarchy.
    /// </summary>
    public class ActorMetaData
    {
        public ActorMetaData(string name)
        {
            SetActorMetaData(name);
        }
        public ActorMetaData(string name, ActorMetaData parent)
        {
            SetActorMetaData(name, parent);
        }
        public ActorMetaData(string name, ActorMetaData parent, bool useBuildInId)
        {
            SetActorMetaData(name, parent, useBuildInId);
        }

        private void SetActorMetaData(string name, ActorMetaData parent = null, bool useBuildInAkkaId = false)
        {
            Name = name;
            Parent = parent;
            if (useBuildInAkkaId == true)
            {
                Name = null;
            }
        }

        public string Name { get; private set; }
        public ActorMetaData Parent { get; private set; }
    }
}
