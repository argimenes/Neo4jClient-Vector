using System;

namespace Neo4jClientVector.Models
{
    public class VectorIdent
    {
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public Guid? RelationId { get; set; }
    }
}
