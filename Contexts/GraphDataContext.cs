using Neo4jClient;

namespace Neo4jClientVector.Contexts
{
    public interface IGraphDataContext : IGraphContext
    {
    }
    public class GraphDataContext : IGraphDataContext
    {
        public IGraphClient Client { get; private set; }

        public GraphDataContext(IGraphClient client)
        {
            Client = client;
        }
    }
}
