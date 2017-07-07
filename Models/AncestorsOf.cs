using Neo4jClientVector.Nodes;
using System.Collections.Generic;


namespace Neo4jClientVector.Models
{
    public class AncestorsOf<T> where T : Entity
    {
        public T Entity { get; set; }
        public IEnumerable<string> Ancestors { get; set; }
    }
}
