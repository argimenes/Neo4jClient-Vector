using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo4jClientVector.Models
{
    public abstract class Cluster { }
    /// <summary>
    /// A representation of the nodes connected to the Entity.
    /// Use the Graph generic class if you also need to store the relationships.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Cluster<T> : Cluster where T : class
    {
        public T Entity { get; set; }
    }
}
