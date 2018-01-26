using Neo4jClient;
using System;

namespace Neo4jClientVector.Attributes
{
    public class RelationshipAttribute : Attribute
    {
        public string Type { get; set; }
        public string Key { get; set; }
        public RelationshipDirection Direction { get; set; }
        public bool SameNodeMultipleAllowed { get; set; }
        public bool MultipleAllowed { get; set; }
        public RelationshipAttribute()
        {
            MultipleAllowed = true;
            SameNodeMultipleAllowed = false;
        }
    }
}
