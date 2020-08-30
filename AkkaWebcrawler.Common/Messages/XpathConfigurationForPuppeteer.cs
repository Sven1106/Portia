
using PortiaLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler.Common.Messages
{
    public class XpathConfigurationForPuppeteer
    {
        public string XpathForAcceptCookiesButton { get; private set; }
        public string XpathForLoadMoreButton { get; private set; }
        public List<string> AbsoluteXpathsForElementsToEnsureExist { get; private set; } = new List<string>();

        public XpathConfigurationForPuppeteer(ProjectDefinition projectDefinition)
        {
            XpathForAcceptCookiesButton = projectDefinition.XpathForAcceptCookiesButton;
            XpathForLoadMoreButton = projectDefinition.XpathForLoadMoreButton;
            foreach (var crawlerSchema in projectDefinition.CrawlerSchemas)
            {
                foreach (var node in crawlerSchema.Nodes)
                {
                    AbsoluteXpathsForElementsToEnsureExist.AddRange(GetXpathsOnRequiredNodes(node));
                }
            }
        }
        private List<string> GetXpathsOnRequiredNodes(NodeAttribute node)
        {
            List<string> xpaths = new List<string>();
            CreateAbsoluteXpathsRecursivelyOnRequiredNodes(ref xpaths, node);
            return xpaths;

        }
        private void CreateAbsoluteXpathsRecursivelyOnRequiredNodes(ref List<string> xpaths, NodeAttribute node, string currentXpath = "")
        {
            string nodeXpath = node.Xpath;
            if (currentXpath != "")
            {
                nodeXpath = nodeXpath.Replace("./", "/"); //removes relative prefix and prepares xpath for absolute path
            }
            currentXpath += nodeXpath;
            if (node.Attributes == null)
            {
                xpaths.Add(currentXpath);
            }
            else
            {
                foreach (var attribute in node.Attributes.Where(x => x.IsRequired == true))
                {
                    CreateAbsoluteXpathsRecursivelyOnRequiredNodes(ref xpaths, attribute, currentXpath);
                }
            }
        }
    }
}
