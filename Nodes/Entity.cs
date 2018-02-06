using Neo4jClientVector.Attributes;
using System;

namespace Neo4jClientVector.Nodes
{
    public interface IIdentifiable
    {
        Guid Guid { get; set; }
    }
    public interface IDisplayName
    {
        string DisplayName { get; }
    }
    public interface INameable
    {
        string Name { get; set; }
    }
    [Ident(Property = "Guid")]
    public class Root : IRoot
    {
        public Guid Guid { get; set; }
    }
    public class Entity : Root, IEntity, IDisplayName
    {
        public string Name { get; set; }
        /// <summary>
        /// Unique plain text identifier, such as used in a blog post slug (e.g., hyphenated title)
        /// </summary>
        public string Code { get; set; }
        public DateTimeOffset DateAddedUTC { get; set; }
        public DateTimeOffset? DateModifiedUTC { get; set; }
        public bool IsDeleted { get; set; }
        public string DisplayName
        {
            get
            {
                return Name;
            }
        }
        public Entity()
        {
            DateAddedUTC = DateTime.UtcNow;
        }
    }
}
