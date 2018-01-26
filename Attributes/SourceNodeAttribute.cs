using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo4jClientVector.Attributes
{
    public class SourceNodeAttribute : Attribute
    {
        public string Key { get; set; }
    }
}
