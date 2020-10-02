using AkkaWebcrawler.Common.Models.Deserialization;
using System.Collections.Generic;
using System.Linq;
namespace AkkaWebcrawler.Common.Models
{
    public class XpathConfigurationForPuppeteer
    {
        public string XpathForAcceptCookiesButton { get; private set; }
        public string XpathForLoadMoreButton { get; private set; }
        public List<string> AbsoluteXpathsForElementsToEnsureExist { get; private set; }

        public XpathConfigurationForPuppeteer(ProjectDefinition projectDefinition)
        {
            XpathForAcceptCookiesButton = projectDefinition.XpathForAcceptCookiesButton;
            XpathForLoadMoreButton = projectDefinition.XpathForLoadMoreButton;
            AbsoluteXpathsForElementsToEnsureExist = new List<string>();
            foreach (var scraperSchema in projectDefinition.ScraperSchemas)
            {
                foreach (var node in scraperSchema.Nodes)
                {
                    AbsoluteXpathsForElementsToEnsureExist.AddRange(GetAllXpathsOnRequiredNodes(node));
                }
            }
        }
        private List<string> GetAllXpathsOnRequiredNodes(NodeAttribute node)
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
