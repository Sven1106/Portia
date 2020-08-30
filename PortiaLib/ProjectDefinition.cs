﻿using System;
using System.Collections.Generic;
namespace PortiaLib
{
    public class ProjectDefinition 
    {
        public Guid ProjectId { get; private set; } // Id is created on Client. Creates a new puppeteer instance pr Id.
        public string ProjectName { get; private set; }
        public Uri Domain { get; private set; }
        public List<Uri> StartUrls { get; private set; }
        public bool IsFixedListOfUrls { get; private set; }
        public string XpathForAcceptCookiesButton { get; private set; }
        public string XpathForLoadMoreButton { get; private set; }
        public List<CrawlerSchema> CrawlerSchemas { get; private set; }
        public ProjectDefinition(Guid projectId, string projectName, Uri domain, List<Uri> startUrls, bool isFixedListOfUrls, string xpathForAcceptCookiesButton, string xpathForLoadMoreButton, List<CrawlerSchema> crawlerSchemas)
        {
            ProjectId = projectId;
            ProjectName = projectName;
            Domain = domain;
            StartUrls = startUrls;
            IsFixedListOfUrls = isFixedListOfUrls;
            XpathForAcceptCookiesButton = xpathForAcceptCookiesButton;
            XpathForLoadMoreButton = xpathForLoadMoreButton;
            CrawlerSchemas = crawlerSchemas;
        }
    }
}
