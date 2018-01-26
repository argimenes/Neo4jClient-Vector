
namespace Neo4jClientVector.Nodes
{
    public class Graph { }

    /// <summary>
    /// A representation of the nodes and relationships connected to Entity.
    /// Use the Cluster generic class if you just need to store the connected Nodes.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Graph<T> where T : class
    {
        public T Entity { get; set; }
    }
}
