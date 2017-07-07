using Neo4jClientVector.Nodes;

namespace Neo4jClientVector.Relationships
{
    public class Vector { }

    public interface IVector<out TRel, out TSource, out TTarget>
    where TRel : Relation
    where TSource : Entity
    where TTarget : Entity
    { }

    public class Vector<TRel, TSource, TTarget> : Vector, IVector<TRel, TSource, TTarget>
        where TRel : Relation
        where TSource : Entity
        where TTarget : Entity
    {
        public TRel Relation { get; set; }
        public TSource Source { get; set; }
        public TTarget Target { get; set; }
    }
}
