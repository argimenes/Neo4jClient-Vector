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
    public interface ICode
    {
        string Code { get; set; }
    }
    public interface IDescription
    {
        string Description { get; set; }
    }
    public interface IIsDeleted
    {
        bool IsDeleted { get; set; }
    }
    public interface IDateAddedUTC
    {
        DateTimeOffset DateAddedUTC { get; set; }
    }
    public interface IDateModifiedUTC
    {
        DateTimeOffset? DateModifiedUTC { get; set; }
    }
    public interface IRoot : IGuid
    {

    }
    public interface IEntity : IRoot, ICode, IIsDeleted, IDateAddedUTC, IDateModifiedUTC, IName
    {

    }
}
