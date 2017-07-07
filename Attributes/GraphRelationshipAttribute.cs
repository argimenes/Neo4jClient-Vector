using Neo4jClient;
using System;

namespace Neo4jClientVector.Attributes
{
    public class GraphRelationshipAttribute : Attribute
    {
        public string Type { get; set; }
        public string Key { get; set; }
        public RelationshipDirection Direction { get; set; }
        public bool SameNodeMultipleAllowed { get; set; }
        public bool MultipleAllowed { get; set; }
        public GraphRelationshipAttribute()
        {
            MultipleAllowed = true;
            SameNodeMultipleAllowed = false;
        }
    }
}
