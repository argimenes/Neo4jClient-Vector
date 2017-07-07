using Neo4jClient.Cypher;
using Neo4jClientVector.Nodes;
using Neo4jClientVector.Relationships;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Neo4jClientVector.Helpers
{
    public static class TypeExtensions
    {
        public static bool Implements<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type);
        }
    }
    public static class StringExtensions
    {
        public static string[] SplitCamelCase(this string value)
        {
            return Common.SplitCamelCase(value);
        }

        public static string Abbreviate(this string value)
        {
            return Common.Abbreviate(value);
        }

        public static Dictionary<string, string> ToDictionary(this string value, char outer, char inner)
        {
            return value.Split(new char[] { outer }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(x => x.Split(inner))
                              .ToDictionary(x => x[0].Trim(), x => x[1].Trim());
        }
        public static bool HasValue(this string value)
        {
            return false == string.IsNullOrWhiteSpace(value);
        }
        public static bool IsEmpty(this string value)
        {
            return false == value.HasValue();
        }

        public static string FormatWith(this string format, object source)
        {
            return FormatWith(format, null, source);
        }

        public static string FormatWith(this string format, IFormatProvider provider, object source)
        {
            if (format == null)
            {
                throw new ArgumentNullException("format");
            }

            var r = new Regex(@"(?<start>\{)+(?<property>[\w\.\[\]]+)(?<format>:[^}]+)?(?<end>\})+",
              RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            var values = new List<object>();
            var rewrittenFormat = r.Replace(format, m =>
            {
                var startGroup = m.Groups["start"];
                var propertyGroup = m.Groups["property"];
                var formatGroup = m.Groups["format"];
                var endGroup = m.Groups["end"];

                values.Add((propertyGroup.Value == "0") ? source : source.GetPropertyValue(propertyGroup.Value) ?? "");

                return new string('{', startGroup.Captures.Count) + (values.Count - 1) + formatGroup.Value
                  + new string('}', endGroup.Captures.Count);
            });

            return string.Format(provider, rewrittenFormat, values.ToArray());
        }
    }
    public static class ObjectExtensions
    {
        /// <summary>
        /// Call a dynamically-provided <paramref name="methodName"/> on the <paramref name="obj"/> with the given <paramref name="args"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static T Call<T>(this object obj, string methodName, params object[] args)
        {
            if (obj == null)
            {
                return default(T);
            }
            return (T)(obj.__(x => x.GetType()).GetMethod(methodName).__(x => x.Invoke(obj, args)));
        }

        public static T Call<T>(this object obj, string methodName, Type genericType, params object[] args)
        {
            if (obj == null)
            {
                return default(T);
            }
            return (T)(obj.GetType()
                          .GetMethod(methodName)
                          .__(x => x.MakeGenericMethod(genericType))
                          .__(x => x.Invoke(obj, args)));
        }
        public static object GetPropertyValue(this object value, string propertyName)
        {
            try
            {
                if (value == null || propertyName == null)
                {
                    return null;
                }
                return value.GetType().GetProperty(propertyName).__(x => x.GetValue(value, null));
            }
            catch (Exception ex)
            {
                var log = log4net.LogManager.GetLogger("DefaultLogger");
                log.Error(new
                {
                    value,
                    propertyName
                }, ex);
                return propertyName.HasValue() ? "{" + propertyName + "}" : "";
            }
        }
    }

    public static class ICypherFluentQueryExtensions
    {
        //public static ICypherFluentQuery CreateUnique<TRel, TSource, TTarget>(this ICypherFluentQuery query, Vector<TRel, TSource, TTarget> vector, string relKey = "relation", string sourceKey = "source", string targetKey = "target")
        //    where TRel : Relation, new()
        //    where TSource : Entity
        //    where TTarget : Entity
        //{
        //    query = query.CreateUnique(Common.Pattern(vector.GetType(), relKey, sourceKey, targetKey));
        //    return query;
        //}

        public static ICypherFluentQuery Match<TEntity>(this ICypherFluentQuery query, string nodeVar = null) where TEntity : Entity
        {
            nodeVar = nodeVar ?? Common.GraphNodeKey<TEntity>();
            query = query.Match($"({nodeVar}:{Common.NodeLabel<TEntity>()})");
            return query;
        }

        public static ICypherFluentQuery Match<TEntity>(this ICypherFluentQuery query, Guid guid) where TEntity : Entity
        {
            query = query.Match($"({Common.GraphNodeKey<TEntity>()}:{Common.NodeLabel<TEntity>()} {{ Guid: {{guid}} }})").WithParams(new { guid });
            return query;
        }

        public static ICypherFluentQuery OptionalMatch<TVector>(this ICypherFluentQuery query) where TVector : Vector
        {
            query = query.OptionalMatch(Common.Pattern<TVector>());
            return query;
        }

        public static ICypherFluentQuery OptionalMatch<TVector>(this ICypherFluentQuery query, string from = null, string rel = null, string relPath = null, string to = null) where TVector : Vector
        {
            var pattern = Common.Pattern<TVector>(rel, from, to, relPath);
            query = query.OptionalMatch(pattern);
            return query;
        }

        public static ICypherFluentQuery WhereStart(this ICypherFluentQuery query)
        {
            return query.Where("1 = 1");
        }

        public static ICypherFluentQuery AndWhereLike(this ICypherFluentQuery query, string prop, string value)
        {
            return query.AndWhere($"{prop} =~ '{PartialMatch(value)}'");
        }

        static string PartialMatch(string value)
        {
            value = value.Trim().Replace("'", "");
            value = "(?i).*" + value + ".*";
            return value;
        }
        public static long Count(this ICypherFluentQuery query, string targetVar = "x")
        {
            return query.FirstOrDefault(() => Return.As<long>($"count(distinct {targetVar})"));
        }
        public static async Task<long> CountAsync(this ICypherFluentQuery query, string targetVar = "x")
        {
            return await query.FirstOrDefaultAsync(() => Return.As<long>($"count(distinct {targetVar})"));
        }
        public static async Task<TResult> FirstOrDefaultAsync<TResult>(this ICypherFluentQuery query, Expression<Func<ICypherResultItem, ICypherResultItem, ICypherResultItem, TResult>> expression)
        {
            return await query.Return(expression).FirstOrDefaultAsync();
        }
        public static async Task<TResult> FirstOrDefaultAsync<TResult>(this ICypherFluentQuery query, Expression<Func<ICypherResultItem, ICypherResultItem, TResult>> expression)
        {
            return await query.Return(expression).FirstOrDefaultAsync();
        }
        public static async Task<TResult> FirstOrDefaultAsync<TResult>(this ICypherFluentQuery query, Expression<Func<ICypherResultItem, TResult>> expression)
        {
            return await query.Return(expression).FirstOrDefaultAsync();
        }
        public static async Task<TResult> FirstOrDefaultAsync<TResult>(this ICypherFluentQuery query, Expression<Func<TResult>> expression)
        {
            return await query.Return(expression).FirstOrDefaultAsync();
        }
        public static TResult FirstOrDefault<TResult>(this ICypherFluentQuery query, Expression<Func<ICypherResultItem, TResult>> expression)
        {
            return query.Return(expression).FirstOrDefault();
        }
        public static TResult FirstOrDefault<TResult>(this ICypherFluentQuery query, Expression<Func<TResult>> expression)
        {
            return query.Return(expression).FirstOrDefault();
        }
        public static TResult FirstOrDefault<TResult>(this ICypherFluentQuery<TResult> query)
        {
            return query.Limit(1).Results.FirstOrDefault();
        }
        public static async Task<TResult> FirstOrDefaultAsync<TResult>(this ICypherFluentQuery<TResult> query)
        {
            return (await query.Limit(1).ResultsAsync).FirstOrDefault();
        }
        public static async Task<List<TResult>> ToListAsync<TResult>(this ICypherFluentQuery query, Expression<Func<ICypherResultItem, TResult>> expression, string orderBy = null)
        {
            if (orderBy.HasValue())
            {
                return (await query.Return(expression).OrderBy(orderBy).ResultsAsync).ToList();
            }
            return (await query.Return(expression).ResultsAsync).ToList();
        }
    }
    public static class GenericExtensions
    {
        public static T To<T>(this object value)
        {
            var t = typeof(T);

            // Get the type that was made nullable.
            var valueType = Nullable.GetUnderlyingType(typeof(T));

            if (valueType != null)
            {
                // Nullable type.

                if (value == null)
                {
                    // you may want to do something different here.
                    return default(T);
                }
                else
                {
                    // Convert to the value type.
                    var result = Convert.ChangeType(value, valueType);

                    // Cast the value type to the nullable type.
                    return (T)result;
                }
            }
            else
            {
                // Not nullable.
                return (T)Convert.ChangeType(value, typeof(T));
            }
        }
        /// <summary>
        /// Null conditional
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="value"></param>
        /// <param name="transformer"></param>
        /// <param name="defaultResult"></param>
        /// <returns></returns>
        public static U __<T, U>(this T value, Func<T, U> transformer, U defaultResult = default(U))
        {
            if (EqualityComparer<T>.Default.Equals(value, default(T)))
            {
                return defaultResult;
            }
            return transformer(value);
        }
        public static bool IsNull<T>(this T value) where T : class
        {
            return null == value;
        }
        public static bool HasValues<T>(this IEnumerable<T> values)
        {
            return values != null && values.Any();
        }
        public static IEnumerable<TVector> NonEmpty<TVector, TRel, TSource, TTarget>(this IEnumerable<TVector> list)
            where TRel : Relation
            where TSource : Entity
            where TTarget : Entity
            where TVector : Vector<TRel, TSource, TTarget>
        {
            return list.Where(x => x.Target != null || x.Source != null);
        }
    }
}
