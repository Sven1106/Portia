using System.Collections.Generic;

namespace AkkaWebcrawler.Common.Models.Deserialization
{
    public class NodeAttribute
    {
        public string Name { get; private set; }
        public NodeType Type { get; private set; }
        public bool GetMultipleFromPage { get; private set; }
        public bool IsRequired { get; private set; } // Can an object be required when its' properties aren't?
        public string Xpath { get; private set; }
        public List<NodeAttribute> Attributes { get; private set; }
        public NodeAttribute(string name, NodeType type, bool getMultipleFromPage, bool isRequired, string xpath, List<NodeAttribute> attributes)
        {
            Name = name;
            Type = type;
            GetMultipleFromPage = getMultipleFromPage;
            IsRequired = isRequired;
            Xpath = xpath;
            Attributes = attributes;
        }
    }
}
