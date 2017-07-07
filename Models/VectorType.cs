using System;

namespace Neo4jClientVector.Models
{
    public class VectorType
    {
        public Type GenericVector { get; set; }
        public Type Relation { get; set; }
        public Type Source { get; set; }
        public Type Target { get; set; }
    }
}
