using Neo4jClient;
using Neo4jClient.Cypher;
using Neo4jClientVector.Attributes;
using Neo4jClientVector.Models;
using Neo4jClientVector.Nodes;
using Neo4jClientVector.Relationships;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Neo4jClientVector.Helpers
{
    public static class TypeExtensions
    {
        public static string Label(this Type type)
        {
            return Common.NodeLabel(type);
        }
        public static string NodeKey(this Type type)
        {
            return Common.NodeKey(type);
        }
        public static string RelationshipKey(this Type type)
        {
            return Common.RelationshipKey(type);
        }
        public static bool Implements<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type);
        }
    }
    public static class StringExtensions
    {
        public static string Join<TVector>(this string prefix, string rel = null, string to = null, string relPath = null) where TVector : Vector
        {
            return prefix + Common.JoinVector<TVector>(rel: rel, to: to, relPath: relPath);
        }

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
        public static MethodInfo GetGenericMethod<T>(this Type type, string methodName)
        {
            return type.GetMethod(methodName).__(x => x.MakeGenericMethod(typeof(T)));
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
        /// <summary>
        /// Returns a vectorised PATH statement for <typeparamref name="TVector"/>. Default value of <paramref name="path"/> is 'p'.
        /// </summary>
        /// <typeparam name="TVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="path"></param>
        /// <param name="rel"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="relPath"></param>
        /// <param name="fromLabel"></param>
        /// <returns></returns>
        public static ICypherFluentQuery Path<TVector>(this ICypherFluentQuery query, string path = null, string pathPattern = null, string rel = null, string from = null, string to = null, string relPath = null, bool fromLabel = true) where TVector : Vector
        {
            path = path ?? "p";
            var vars = ProcessVars(pathPattern);
            if (vars.Successful())
            {
                var result = vars.Data;
                from = result.From;
                rel = result.Rel;
                relPath = result.RelPath;
                to = result.To;
            }
            var pattern = $"{path}=" + Common.Vector<TVector>(rel, from, to, relPath, fromLabel);
            query = query.Match(pattern);
            return query;
        }

        /// <summary>
        /// Returns a vectorised PATH statement for hypernode starting with <typeparamref name="TFirstVector"/> and ending in <typeparamref name="TSecondVector"/>. Default value of <paramref name="path"/> is 'p'.
        /// </summary>
        /// <typeparam name="TFirstVector"></typeparam>
        /// <typeparam name="TSecondVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="path"></param>
        /// <param name="rel"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="relPath"></param>
        /// <param name="fromLabel"></param>
        /// <param name="to2"></param>
        /// <param name="rel2"></param>
        /// <param name="relPath2"></param>
        /// <returns></returns>
        public static ICypherFluentQuery Path<TFirstVector, TSecondVector>(this ICypherFluentQuery query,
            string path = null, string pathPattern = null, string rel = null, string from = null, string to = null, string relPath = null, bool fromLabel = true, string to2 = null, string rel2 = null, string relPath2 = null)
            where TFirstVector : Vector
            where TSecondVector : Vector
        {
            path = path ?? "p";
            var vars = ProcessVars(pathPattern);
            if (vars.Successful())
            {
                var result = vars.Data;
                from = result.From;
                rel = result.Rel;
                relPath = result.RelPath;
                to = result.To;
                rel2 = result.Rel2;
                relPath2 = result.RelPath2;
                to2 = result.To2;
            }
            var pattern = $"{path}=" + Common.Vector<TFirstVector>(rel, from, to, relPath, fromLabel).Join<TSecondVector>(rel: rel2, to: to2, relPath: relPath2);
            query = query.Match(pattern);
            return query;
        }

        /// <summary>
        /// Matches the source and target nodes of the <paramref name="TVector"/>.
        /// </summary>
        /// <typeparam name="TVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="sourceGuid"></param>
        /// <param name="targetGuid"></param>
        /// <returns></returns>
        public static ICypherFluentQuery Match<TVector>(this ICypherFluentQuery query, string from, string to, Guid sourceGuid, Guid targetGuid) where TVector : Vector
        {
            var vectorType = Common.Unpack<TVector>();
            query = query.Match(
                    "(" + from + ":" + Common.NodeLabel(vectorType.Source) + " { Guid: {sourceGuid} })",
                    "(" + to + ":" + Common.NodeLabel(vectorType.Target) + " { Guid: {targetGuid} })")
                    .WithParams(new { sourceGuid, targetGuid });
            return query;
        }

        /// <summary>
        /// Creates a unique <paramref name="relation"/> between <paramref name="sourceKey"/> and <paramref name="targetKey"/>.
        /// </summary>
        /// <typeparam name="TVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="sourceKey"></param>
        /// <param name="targetKey"></param>
        /// <param name="relation"></param>
        /// <returns></returns>
        public static ICypherFluentQuery Relate<TVector>(this ICypherFluentQuery query, string sourceKey, string targetKey, Relation relation) where TVector : Vector
        {
            query = query.CreateUnique(CreateUniquePattern<TVector>(sourceKey, "r", targetKey))
                         .WithParams(new { relation });
            return query;
        }

        public static string CreateUniquePattern<TVector>(string sourceKey, string relationKey, string targetKey, string relationParam = "relation") where TVector : Vector
        {
            var type = Common.Unpack<TVector>();
            var relationAttribute = Attribute.GetCustomAttribute(type.Relation, typeof(RelationshipAttribute)) as RelationshipAttribute;
            if (relationAttribute.Direction == RelationshipDirection.Outgoing)
            {
                return "(" + sourceKey + " )-[" + relationKey + ":" + relationAttribute.Type + " {" + relationParam + "}]->(" + targetKey + ")";
            }
            else
            {
                return "(" + sourceKey + ")<-[" + relationKey + ":" + relationAttribute.Type + " {" + relationParam + "}]-(" + targetKey + ")";
            }
        }

        //public static ICypherFluentQuery CreateUnique<TRel, TSource, TTarget>(this ICypherFluentQuery query, Vector<TRel, TSource, TTarget> vector, string relKey = "relation", string sourceKey = "source", string targetKey = "target")
        //    where TRel : Relation, new()
        //    where TSource : Entity
        //    where TTarget : Entity
        //{
        //    query = query.CreateUnique(Common.Pattern(vector.GetType(), relKey, sourceKey, targetKey));
        //    return query;
        //}

        public static ICypherFluentQuery From<TEntity>(this ICypherFluentQuery query, string nodeVar = null) where TEntity : Root
        {
            nodeVar = nodeVar ?? Common.GraphNodeKey<TEntity>();
            query = query.Match($"({nodeVar}:{Common.NodeLabel<TEntity>()})");
            return query;
        }

        public static ICypherFluentQuery From<TEntity>(this ICypherFluentQuery query, Guid guid) where TEntity : Root
        {
            query = query.Match($"({Common.GraphNodeKey<TEntity>()}:{Common.NodeLabel<TEntity>()} {{ Guid: {{guid}} }})").WithParams(new { guid });
            return query;
        }

        public static ICypherFluentQuery From<TEntity>(this ICypherFluentQuery query, string nodeVar, Guid guid) where TEntity : Root
        {
            query = query.Match("(" + nodeVar + ":" + Common.NodeLabel<TEntity>() + " { Guid: {" + nodeVar + "Guid} })").WithParam(nodeVar + "Guid", guid);
            return query;
        }

        public static ICypherFluentQuery OptMatch<TVector>(this ICypherFluentQuery query) where TVector : Vector
        {
            query = query.OptionalMatch(Common.Vector<TVector>());
            return query;
        }

        /// <summary>
        /// Returns a vectorised MATCH statement for <typeparamref name="TVector"/>.
        /// </summary>
        /// <typeparam name="TVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="from"></param>
        /// <param name="rel"></param>
        /// <param name="relPath"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static ICypherFluentQuery Match<TVector>(this ICypherFluentQuery query, string path = null, string from = null, string rel = null, string relPath = null, string to = null) where TVector : Vector
        {
            var vars = ProcessVars(path);
            if (vars.Successful())
            {
                var result = vars.Data;
                from = result.From;
                rel = result.Rel;
                relPath = result.RelPath;
                to = result.To;
            }
            query = query.Match(Common.Vector<TVector>(rel, from, to, relPath));
            return query;
        }

        public static ICypherFluentQuery Merge<TVector>(this ICypherFluentQuery query, string path = null) where TVector : Vector
        {
            string from = null;
            string rel = null;
            string relPath = null;
            string to = null;
            var vars = ProcessVars(path);
            if (vars.Successful())
            {
                var result = vars.Data;
                from = result.From;
                rel = result.Rel;
                relPath = result.RelPath;
                to = result.To;
            }
            query = query.Merge(Common.Vector<TVector>(rel, from, to, relPath, fromLabel: false, toLabel: false));
            return query;
        }

        public static string Pattern<TVector>(this ICypherFluentQuery query, string from = null, string rel = null, string relPath = null, string to = null) where TVector : Vector
        {
            return Common.Vector<TVector>(rel, from, to, relPath);
        }

        public static ICypherFluentQuery Filter<TSearch>(this ICypherFluentQuery query, TSearch search, Func<ICypherFluentQuery, TSearch, ICypherFluentQuery> filter) where TSearch : class
        {
            query = filter(query, search);
            return query;
        }

        public static ICypherFluentQuery If(this ICypherFluentQuery query, bool condition, Func<ICypherFluentQuery, ICypherFluentQuery> thenDo, Func<ICypherFluentQuery, ICypherFluentQuery> elseDo = null)
        {
            if (condition)
            {
                query = thenDo(query);
            }
            else
            {
                if (elseDo != null)
                {
                    query = elseDo(query);
                }
            }
            return query;
        }

        /// <summary>
        /// Returns a MATCH statement for a hypernode starting with <typeparamref name="TFirstVector"/> and ending in <typeparamref name="TSecondVector"/>.
        /// </summary>
        /// <typeparam name="TFirstVector"></typeparam>
        /// <typeparam name="TSecondVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="from"></param>
        /// <param name="rel"></param>
        /// <param name="relPath"></param>
        /// <param name="to"></param>
        /// <param name="rel2"></param>
        /// <param name="relPath2"></param>
        /// <param name="to2"></param>
        /// <returns></returns>
        public static ICypherFluentQuery Match<TFirstVector, TSecondVector>(this ICypherFluentQuery query, string path = null, string from = null, string rel = null, string relPath = null, string to = null, string rel2 = null, string relPath2 = null, string to2 = null) where TFirstVector : Vector where TSecondVector : Vector
        {
            var vars = ProcessVars(path);
            if (vars.Successful())
            {
                var result = vars.Data;
                from = result.From;
                rel = result.Rel;
                relPath = result.RelPath;
                to = result.To;
                rel2 = result.Rel2;
                relPath2 = result.RelPath2;
                to2 = result.To2;
            }
            var pattern = HyperNodeVector<TFirstVector, TSecondVector>(from, rel, relPath, to, rel2, relPath2, to2);
            query = query.Match(pattern);
            return query;
        }

        static string VarPart(string relPath)
        {
            var parts = relPath.Replace("[", "").Replace("]", "").Split(':');
            if (parts.Length < 2)
            {
                return null;
            }
            return parts[1];
        }

        static string Part(string value)
        {
            if (value.Contains(":"))
            {
                value = value.Split(':')[0];
            }
            var scrubbed = value.Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "");
            if (scrubbed == "")
            {
                return "";
            }
            if (scrubbed == "_")
            {
                return null;
            }
            return scrubbed;
        }

        public static string HyperNodeVector<TFirstVector, TSecondVector>(string from, string rel, string relPath, string to, string rel2, string relPath2, string to2)
            where TFirstVector : Vector
            where TSecondVector : Vector
        {
            return Common.Vector<TFirstVector>(rel, from, to, relPath) + Common.JoinVector<TSecondVector>(rel: rel2, relPath: relPath2, to: to2);
        }

        /// <summary>
        /// Returns an OPTIONAL MATCH statement for a hypernode starting with <typeparamref name="TFirstVector"/> and ending in <typeparamref name="TSecondVector"/>.
        /// </summary>
        /// <typeparam name="TFirstVector"></typeparam>
        /// <typeparam name="TSecondVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="from"></param>
        /// <param name="rel"></param>
        /// <param name="relPath"></param>
        /// <param name="to"></param>
        /// <param name="rel2"></param>
        /// <param name="relPath2"></param>
        /// <param name="to2"></param>
        /// <returns></returns>
        public static ICypherFluentQuery OptMatch<TFirstVector, TSecondVector>(this ICypherFluentQuery query, string path = null, string from = null, string rel = null, string relPath = null, string to = null, string rel2 = null, string relPath2 = null, string to2 = null) where TFirstVector : Vector where TSecondVector : Vector
        {
            if (path.HasValue())
            {
                var parts = path.Split('-');
                from = Part(parts[0]);
                rel = Part(parts[1]);
                relPath = VarPart(parts[1]);
                to = Part(parts[2]);
                if (parts.Length > 3)
                {
                    rel2 = Part(parts[3]);
                    relPath2 = VarPart(parts[3]);
                    to2 = Part(parts[4]);
                }
            }
            var pattern = HyperNodeVector<TFirstVector, TSecondVector>(from, rel, relPath, to, rel2, relPath2, to2);
            query = query.OptionalMatch(pattern);
            return query;
        }

        /// <summary>
        /// Returns a vectorised OPTIONAL MATCH statement for <typeparamref name="TVector"/>.
        /// </summary>
        /// <typeparam name="TVector"></typeparam>
        /// <param name="query"></param>
        /// <param name="from"></param>
        /// <param name="rel"></param>
        /// <param name="relPath"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static ICypherFluentQuery OptMatch<TVector>(this ICypherFluentQuery query, string path = null, string from = null, string rel = null, string relPath = null, string to = null) where TVector : Vector
        {
            var vars = ProcessVars(path);
            if (vars.Successful())
            {
                var result = vars.Data;
                from = result.From;
                rel = result.Rel;
                relPath = result.RelPath;
                to = result.To;
            }
            var pattern = Common.Vector<TVector>(rel, from, to, relPath);
            query = query.OptionalMatch(pattern);
            return query;
        }

        public static Result<VectorVars> ProcessVars(string path)
        {
            var ignore = Result.Rejected<VectorVars>();
            if (false == path.HasValue())
            {
                return ignore;
            }
            var parts = path.Split('-');
            if (parts.Length == 0)
            {
                return ignore;
            }
            var vars = new VectorVars { };
            if (parts.Length == 1)
            {
                if (path.StartsWith("-"))
                {
                    vars.To = Part(parts[0]);
                }
                else if (path.EndsWith("-"))
                {
                    vars.From = Part(parts[0]);
                }
            }
            else
            {
                vars.From = Part(parts[0]);
                vars.Rel = Part(parts[1]);
                vars.RelPath = VarPart(parts[1]);
                vars.To = Part(parts[2]);
                if (parts.Length > 3)
                {
                    vars.Rel2 = Part(parts[3]);
                    vars.RelPath2 = VarPart(parts[3]);
                    vars.To2 = Part(parts[4]);
                }
            }
            return Result.Success(vars);
        }

        public class VectorVars
        {
            public string From { get; set; }
            public string Rel { get; set; }
            public string RelPath { get; set; }
            public string To { get; set; }
            public string Rel2 { get; set; }
            public string RelPath2 { get; set; }
            public string To2 { get; set; }
        }

        /// <summary>
        /// Returns a WHERE statement that always evaluates to true, so that dynamic AND WHERE clauses can be added to it.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static ICypherFluentQuery Where(this ICypherFluentQuery query)
        {
            return query.Where("1 = 1");
        }

        public static ICypherFluentQuery AndWhere(this ICypherFluentQuery query, string prop, string value)
        {
            return query.AndWhere($"{prop} = '{value}'");
        }

        public static ICypherFluentQuery AndWhere(this ICypherFluentQuery query, string prop, bool value)
        {
            return query.AndWhere(prop, value.ToString());
        }

        public static ICypherFluentQuery AndWhere(this ICypherFluentQuery query, string prop, Guid? value)
        {
            return query.AndWhere($"{prop} = '{value.Value}'");
        }

        /// <summary>
        /// Returns an AND WHERE statement that performs a case-insensitive partial match on <paramref name="value"/>.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="prop"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ICypherFluentQuery AndWhereLike(this ICypherFluentQuery query, string prop, string value)
        {
            return query.AndWhere($"{prop} =~ '{PartialMatch(value)}'");
        }

        public static ICypherFluentQuery Where(this ICypherFluentQuery query, string prop, string value)
        {
            return query.Where($"{prop} = '{value}'");
        }

        public static ICypherFluentQuery Where(this ICypherFluentQuery query, string prop, Guid value)
        {
            return query.Where($"{prop} = '{value}'");
        }

        public static ICypherFluentQuery Where(this ICypherFluentQuery query, string prop, Guid? value)
        {
            return query.Where($"{prop} = '{value.Value}'");
        }

        /// <summary>
        /// Returns a WHERE statement that performs a case-insensitive partial match on <paramref name="value"/>.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="prop"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ICypherFluentQuery WhereLike(this ICypherFluentQuery query, string prop, string value)
        {
            return query.Where($"{prop} =~ '{PartialMatch(value)}'");
        }

        static string PartialMatch(string value)
        {
            value = value.Trim().Replace("'", "\\'");
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
        public static async Task<TOutput> To<TInput, TOutput>(this TInput result, Func<TInput, TOutput> transformer)
            where TInput : class
            where TOutput : class, new()
        {
            return transformer(result);
        }
        public static async Task<TResult> FirstOrDefaultAsync<TResult>(this ICypherFluentQuery query, Expression<Func<ICypherResultItem, TResult>> expression)
        {
            var returnExp = query.Return(expression);
            return await returnExp.FirstOrDefaultAsync();
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
        public static async Task<List<TResult>> ToListAsync<TResult>(this ICypherFluentQuery query, Expression<Func<ICypherResultItem, TResult>> expression, string orderBy = null, int? skip = null, int? limit = null)
        {
            if (orderBy.HasValue())
            {
                var returnExp = query.Return(expression).OrderBy(orderBy);
                if (skip.HasValue && limit.HasValue)
                {
                    var test = returnExp.Skip(skip.Value).Limit(limit.Value);
                    return (await test.ResultsAsync).ToList();
                }
                return (await returnExp.ResultsAsync).ToList();
            }
            return (await query.Return(expression).ResultsAsync).ToList();
        }
    }
    public static class ListExtensions
    {
        /// <summary>
        /// Returns <paramref name="list"/> if it has any elements, otherwise returns an empty List&lt;T&gt;.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static List<T> OrEmpty<T>(this List<T> list)
        {
            return list.HasValues() ? list : new List<T>();
        }
    }
    public static class GenericExtensions
    {
        public static T ToEnum<T>(this string value, T defaultValue = default(T)) where T : struct, IConvertible
        {
            if (value == null)
            {
                return defaultValue;
            }
            T parse = defaultValue;
            Enum.TryParse(value, out parse);
            return parse;
        }
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
