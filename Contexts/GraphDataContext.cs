using Neo4jClient;

namespace Neo4jClientVector.Contexts
{
    public interface IGraphDataContext : IGraphContext
    {
    }
    public class GraphDataContext : IGraphDataContext
    {
        public GraphClient Client { get; private set; }

        public GraphDataContext(GraphClient client)
        {
            Client = client;
        }
    }
}
