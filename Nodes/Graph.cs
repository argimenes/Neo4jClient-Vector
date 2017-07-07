
namespace Neo4jClientVector.Nodes
{
    public abstract class Graph<T> where T : class
    {
        public T Entity { get; set; }
    }
}
