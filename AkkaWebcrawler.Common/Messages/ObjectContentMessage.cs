using Newtonsoft.Json.Linq;

namespace AkkaWebcrawler.Common.Messages
{
    public class ObjectContentMessage
    {
        public string ScraperSchemaName { get; private set; } // For matching
        public JObject JObject { get; private set; }
        public ObjectContentMessage(string scraperSchemaName, JObject jObject)
        {
            ScraperSchemaName = scraperSchemaName;
            this.JObject = jObject;
        }
    }
}
