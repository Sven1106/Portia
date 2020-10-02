using AkkaWebcrawler.Common.Interfaces;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AkkaWebcrawler.Web.Models
{
    public class SignalRWebcrawlerEventsPusher : IScraperEventsPusher
    {
        private static readonly IHubContext _webcrawlerHubContext;
        static SignalRWebcrawlerEventsPusher()
        {
            _webcrawlerHubContext = GlobalHost.ConnectionManager.GetHubContext<WebcrawlerHub>();
        }
        public void ProjectCreated()
        {
            _webcrawlerHubContext.Clients.All.playerJoined();
        }
    }
}