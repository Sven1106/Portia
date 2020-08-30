using Newtonsoft.Json.Linq;

namespace AkkaWebcrawler.Common.Messages
{
    public class CrawledObjectContent
    {
        public string CrawlerSchemaName { get; private set; } // For matching
        public JObject CrawledObject { get; private set; }
        public CrawledObjectContent(string crawlerSchemaName, JObject crawledJObject)
        {
            CrawlerSchemaName = crawlerSchemaName;
            CrawledObject = crawledJObject;
        }
    }
}
