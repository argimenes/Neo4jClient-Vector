using Neo4jClientVector.Constants.Enums;
using System.Collections.Generic;

namespace Neo4jClientVector.Models
{
    public class Search<T> where T : class
    {
        /// <summary>
        /// Milliseconds
        /// </summary>
        public long ElapsedMilliseconds { get; set; }
        public int Count { get; set; }
        public int Page { get; set; }
        public int PageRows { get; set; }
        public int MaxPage { get; set; }
        public bool Infinite { get; set; }
        public string[] Groups { get; set; }
        public List<T> Results { get; set; }
        public SearchOrder Order { get; set; }
        public SearchDirection Direction { get; set; }
        public Search()
        {
            Page = 1;
            PageRows = 60;
            Direction = SearchDirection.Ascending;
        }
        /// <summary>
        /// Converts the parent Search type back to the child Search type.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <returns></returns>
        public U Downcast<U>() where U : Search<T>, new()
        {
            var item = new U
            {
                Results = Results,
                Direction = Direction,
                Order = Order,
                MaxPage = MaxPage,
                Page = Page,
                PageRows = PageRows,
                Count = Count
            };
            return item;
        }
    }
}
