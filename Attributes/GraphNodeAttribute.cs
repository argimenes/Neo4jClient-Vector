using System;

namespace Neo4jClientVector.Attributes
{
    public class NodeAttribute : Attribute
    {
        public string Label { get; set; }
        public string Key { get; set; }
    }
}
