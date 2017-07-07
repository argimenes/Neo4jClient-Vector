using System;

namespace Neo4jClientVector.Nodes
{
    public interface IIdentifiable
    {
        Guid Guid { get; set; }
    }
    public interface INameable
    {
        string Name { get; set; }
    }
    public interface IEntity : IIdentifiable, INameable
    {
        DateTime DateAddedUTC { get; set; }
        DateTime? DateModifiedUTC { get; set; }
    }
    public class Entity : IEntity
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// Unique plain text identifier, such as used in a blog post slug (e.g., hyphenated title)
        /// </summary>
        public string URICode { get; set; }
        public DateTime DateAddedUTC { get; set; }
        public DateTime? DateModifiedUTC { get; set; }
        public bool IsDeleted { get; set; }

        public Entity()
        {
            DateAddedUTC = DateTime.UtcNow;
        }
    }
}
