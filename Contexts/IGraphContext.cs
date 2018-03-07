using Neo4jClient;

namespace Neo4jClientVector.Contexts
{
    public interface IGraphContext
    {
        IGraphClient Client { get; }
    }
}
