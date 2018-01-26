using Neo4jClientVector.Constants.Enums;
using Neo4jClientVector.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo4jClientVector.Models
{
    public class OrderBy
    {
        private Search Search;
        private Dictionary<string, string[]> Dict = new Dictionary<string, string[]>();
        public OrderBy(Search search)
        {
            Search = search;
        }
        public static OrderBy From(Search search)
        {
            return new OrderBy(search);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="order">The SearchOrder to match.</param>
        /// <param name="asc">The ASC expression</param>
        /// <param name="desc">The DESC expression</param>
        /// <returns></returns>
        public OrderBy When(string order, string asc, string desc)
        {
            Dict.Add(order, new string[] { asc, desc });
            return this;
        }
        public OrderBy When(string order, string field)
        {
            return When(order, field + " ASC", field + " DESC");
        }
        public string Render()
        {
            foreach (var item in Dict)
            {
                if (Search.Order == item.Key)
                {
                    return Search.Direction == SearchDirection.Ascending ? item.Value[0] : item.Value[1];
                }
            }
            return null;
        }
    }
}
