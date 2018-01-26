using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo4jClientVector.Attributes
{
    public abstract class VectorAttribute : Attribute
    {
        public string Key { get; set; }
    }
    public class TargetVectorAttribute : VectorAttribute { }
    public class SourceVectorAttribute : VectorAttribute { }
}
