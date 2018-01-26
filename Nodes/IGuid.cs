using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo4jClientVector.Nodes
{
    public interface IGuid
    {
        Guid Guid { get; set; }
    }
    public interface IName
    {
        string Name { get; set; }
    }
    public interface IDescription
    {
        string Description { get; set; }
    }
    public interface IEntity : IGuid, IName
    {
        DateTimeOffset DateAddedUTC { get; set; }
        DateTimeOffset? DateModifiedUTC { get; set; }
    }
}
