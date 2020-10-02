using System.Collections.Generic;
namespace AkkaWebcrawler.Common.Models.Deserialization
{
    public class ScraperSchema
    {
        public string Name { get; private set; }
        public List<NodeAttribute> Nodes { get; private set; }
        public ScraperSchema(string name, List<NodeAttribute> nodes)
        {
            Name = name;
            Nodes = nodes;
        }
    }
}
