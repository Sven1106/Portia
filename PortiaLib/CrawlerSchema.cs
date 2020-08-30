using System.Collections.Generic;
namespace PortiaLib
{
    public class CrawlerSchema
    {
        public string Name { get; private set; }
        public List<NodeAttribute> Nodes { get; private set; }
        public CrawlerSchema(string name, List<NodeAttribute> nodes)
        {
            Name = name;
            Nodes = nodes;
        }
    }
}
