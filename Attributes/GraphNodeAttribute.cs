using System;

namespace Neo4jClientVector.Attributes
{
    public class GraphNodeAttribute : Attribute
    {
        public string Name { get; set; }
        public string Key { get; set; }
    }
}
