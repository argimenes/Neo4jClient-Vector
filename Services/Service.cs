using log4net;
using Neo4jClient;
using Neo4jClient.Cypher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using System.Linq.Expressions;
using System.Diagnostics;
using Neo4jClientVector.Models;
using Neo4jClientVector.Nodes;
using Neo4jClientVector.Relationships;
using Neo4jClientVector.Contexts;
using Neo4jClientVector.Helpers;
using Neo4jClientVector.Attributes;
using Neo4jClientVector.Constants.Enums;
using System.Reflection;
using AutoMapper;

namespace Neo4jClientVector.Core.Services
{
    public interface IService
    {
        Task<THyperVector> FindHyperVectorAsync<THyperVector>(Guid relationId) where THyperVector : HyperVector;
        Task<Result> DeleteAsync<TRel, TSource, TTarget>(Vector<TRel, TSource, TTarget> vector, bool replace = false)
            where TRel : Relation, new()
            where TSource : Root
            where TTarget : Root;
        Task<TVector> FindVectorAsync<TVector>(Guid relationId) where TVector : Vector;
        Task<Result> SaveOrUpdateInsideScopeAsync<TEntity>(TEntity entity, Action<TEntity> insert = null, Action<TEntity> update = null) where TEntity : Root, new();
        bool NArity(Type vectorType, string sourceGuid, string targetGuid);
        Task<Result> UpdateAsync<TEntity>(Guid guid, Action<TEntity> update = null) where TEntity : Root, new();
        Task<Result> SaveOrUpdateAsync<TEntity>(TEntity entity, Action<TEntity> insert = null, Action<TEntity> update = null) where TEntity : Root, new();
        T Find<T>(Guid guid) where T : Root;
        Task<Result> SaveAsync<TEntity>(TEntity entity) where TEntity : Root;
        Task<Result> RelateAsync<TRel, TSource, TTarget>(Vector<TRel, TSource, TTarget> vector, bool replace = false)
            where TRel : Relation, new()
            where TSource : Root
            where TTarget : Root;
        Task<Result> DeleteRelationAsync(Relation relation);
        Task<Result> DeleteRelationAsync<TRel>(Guid guid) where TRel : Relation;
    }
    public interface IService<TRoot> : IService where TRoot : Root
    {
        Task<TSearch> PageAsync<TSearch>(Search<TRoot> query, ICypherFluentQuery records, Expression<Func<ICypherResultItem, TRoot>> selector = null, OrderBy orderBy = null, string startNode = "x")
            where TSearch : Search<TRoot>, new();
        Task<TRoot> FindAsync(Guid guid);
        TRoot Find(Guid guid);
        Task<List<TRoot>> AllAsync(string orderBy = null);
    }
    public class Service<TRoot> : Service, IService<TRoot> where TRoot : Root
    {
        #region constructor
        public Service(IGraphDataContext _db) : base(_db)
        {
        }
        #endregion

        public async Task<TSearch> PageAsync<TSearch>(Search<TRoot> query, ICypherFluentQuery records, Expression<Func<ICypherResultItem, TRoot>> selector = null, OrderBy orderBy = null, string startNode = null)
            where TSearch : Search<TRoot>, new()
        {
            return await PageAsync<TRoot, TSearch>(query, records, selector: selector, orderBy: orderBy, entityKey: startNode);
        }

        public async Task<List<TRoot>> AllAsync(string orderBy = null)
        {
            return await AllAsync<TRoot>(orderBy: orderBy);
        }

        public async Task<TRoot> FindAsync(Guid guid)
        {
            return await FindAsync<TRoot>(guid);
        }

        public TRoot Find(Guid guid)
        {
            return Find<TRoot>(guid);
        }

        protected async Task<Result> SaveVectorAsync<TVector>(TRoot entity, TVector vector)
            where TVector : Vector
        {
            var ident = ToVectorIdent(vector);
            ident.SourceId = entity.Guid.Value;
            if (ident.RelationId.HasValue)
            {
                await DeleteAsync<TVector>(ident);
            }
            return await RelateAsync<TVector>(ident);
        }
    }

    public class Service : IService, IDisposable
    {
        #region constructor
        protected readonly ILog Log = LogManager.GetLogger("Default");
        protected readonly ICypherFluentQuery graph;
        protected readonly IGraphDataContext db;
        public Service(IGraphDataContext _db)
        {
            db = _db;
            graph = _db.Client.Cypher;
        }
        #endregion

        protected static string Vector<TVector>(string relationKey = null, string from = null, string to = null, string relPath = null, bool sourceLabel = true) where TVector : Vector
        {
            return Common.Vector<TVector>(relationKey, from, to, relPath, sourceLabel);
        }

        protected VectorIdent ToVectorIdent<TVector>(TVector vector)
            where TVector : Vector
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TVector, Vector<Relation, Root, Root>>();
            });
            var generic = Mapper.Map<Vector<Relation, Root, Root>>(vector); // vector as Vector<Relation, Entity, Entity>;
            var ident = new VectorIdent
            {
                SourceId = generic.Source.__(x => x.Guid.Value),
                RelationId = generic.Relation.__(x => x.Guid),
                TargetId = generic.Target.__(x => x.Guid.Value)
            };
            return ident;
        }

        protected string Direction(Search search, string ascending, string descending)
        {
            return search.Direction == SearchDirection.Ascending ? ascending : descending;
        }

        protected string Key<TEntity>() where TEntity : Entity
        {
            return Common.GraphNodeKey<TEntity>();
        }

        public static string Collect<TVector>() where TVector : Vector
        {
            return Collect<TVector>(null);
        }

        protected static string Collect<TVector>(string targetKey) where TVector : Vector
        {
            return "collect(distinct " + Edge<TVector>(targetKey) + ")";
        }

        protected static string Rows<TVector>() where TVector : Vector
        {
            return Rows<TVector>(null);
        }

        protected static string Rows<TVector>(string targetKey) where TVector : Vector
        {
            var type = Common.Unpack<TVector>();
            var SK = type.Source.NodeKey();
            var RK = type.Relation.RelationshipKey();
            var TK = targetKey ?? type.Target.NodeKey();
            var pattern = Common.Vector<TVector>(fromLabel: false, from: SK, to: TK);
            return "[ " + pattern + " | { Source: " + SK + ", Relation: " + RK + ", Target: " + TK + " } ]";
        }

        protected static string HyperRows<THyperVector>() where THyperVector : HyperVector
        {
            return HyperRows<THyperVector>(null, null);
        }

        protected static string HyperRows<THyperVector>(string targetKey2) where THyperVector : HyperVector
        {
            return HyperRows<THyperVector>(null, targetKey2);
        }

        protected static string HyperRows<THyperVector>(string targetKey1, string targetKey2) where THyperVector : HyperVector
        {
            var genericHyperVectorType = typeof(THyperVector).UnderlyingSystemType.BaseType;
            var args = genericHyperVectorType.GetTypeInfo().GenericTypeArguments;
            var type = Common.UnpackHyperVector<THyperVector>();

            var SK1 = type.Item1.Source.NodeKey();
            var RK1 = type.Item1.Relation.RelationshipKey();
            var TK1 = targetKey1 ?? type.Item1.Target.NodeKey();
            var leftPattern = Common.PatternInternal(args[0].UnderlyingSystemType, fromLabel: false);

            var SK2 = type.Item2.Source.NodeKey();
            var RK2 = type.Item2.Relation.RelationshipKey();
            var TK2 = targetKey2 ?? type.Item2.Target.NodeKey();
            var rightPattern = Common.JoinPatternInternal(args[1].UnderlyingSystemType, to: TK2);

            var pattern = leftPattern + rightPattern;

            var rows = "[ " + pattern + " | { Left: { Source: " + SK1 + ", Relation: " + RK1 + ", Target: " + TK1 + " }, Right: { Source: " + SK2 + ", Relation: " + RK2 + ", Target: " + TK2 + " } } ]";
            return rows;
        }

        protected static string Rows<TVector>(string sourceKey, string targetKey) where TVector : Vector
        {
            var type = Common.Unpack<TVector>();
            var SK = sourceKey ?? type.Source.NodeKey();
            var RK = type.Relation.RelationshipKey();
            var TK = targetKey ?? type.Target.NodeKey();
            var pattern = Common.Vector<TVector>(fromLabel: false, from: SK, to: TK);
            return "[ " + pattern + " | { Source: " + SK + ", Relation: " + RK + ", Target: " + TK + " } ]";
        }

        protected static string Single<TVector>() where TVector : Vector
        {
            return Single<TVector>(null);
        }

        protected static string Single<TVector>(string sourceKey, string targetKey) where TVector : Vector
        {
            // return "head(" + Rows<TVector>(sourceKey, targetKey) + ")";
            return Rows<TVector>(sourceKey, targetKey) + "[0]";
        }

        protected static string Single<TVector>(string targetKey) where TVector : Vector
        {
            // return "head(" + Rows<TVector>(targetKey) + ")";
            return Rows<TVector>(targetKey) + "[0]";
        }

        protected static string Edge<TVector>(string targetKey) where TVector : Vector
        {
            var type = Common.Unpack<TVector>();
            var RK = type.Relation.RelationshipKey();
            var TK = targetKey ?? type.Target.NodeKey();
            return "{ Relation: " + RK + ", Target: " + TK + " }";
        }

        protected static string Edge<TVector>() where TVector : Vector
        {
            return Edge<TVector>(null);
        }

        protected static string Head<TVector>() where TVector : Vector
        {
            return "head(" + Collect<TVector>(null) + ")";
        }

        protected static string Head<TVector>(string target) where TVector : Vector
        {
            return "head(" + Collect<TVector>(target) + ")";
        }

        /// <summary>
        /// Checks to see if one or more relations already exists for the <paramref name="specificVectorType"/>.
        /// </summary>
        /// <param name="specificVectorType"></param>
        /// <param name="sourceGuid"></param>
        /// <param name="targetGuid"></param>
        /// <returns></returns>
        public bool NArity(Type specificVectorType, string sourceGuid, string targetGuid)
        {
            var type = Common.Unpack(specificVectorType);
            var relationAttribute = Attribute.GetCustomAttribute(type.Relation, typeof(RelationshipAttribute)) as RelationshipAttribute;
            if (relationAttribute.SameNodeMultipleAllowed)
            {
                return true;
            }
            var pattern = VectorPattern(relationAttribute, type.Source, sourceGuid, type.Target, targetGuid);
            var query = graph.Match(pattern).WithParams(new { sourceGuid, targetGuid });
            var total = query.Count("r");
            return total >= 1;
        }

        protected string CreateUniquePattern<TVector>(string sourceKey, string relationKey, string targetKey) where TVector : Vector
        {
            var type = Common.Unpack<TVector>();
            var relationAttribute = Attribute.GetCustomAttribute(type.Relation, typeof(RelationshipAttribute)) as RelationshipAttribute;
            if (relationAttribute.Direction == RelationshipDirection.Outgoing)
            {
                return "(" + sourceKey + " )-[" + relationKey + ":" + relationAttribute.Type + " {relationship}]->(" + targetKey + ")";
            }
            else
            {
                return "(" + sourceKey + ")<-[" + relationKey + ":" + relationAttribute.Type + " {relationship}]-(" + targetKey + ")";
            }
        }

        protected string VectorPattern<TVector>() where TVector : Vector
        {
            var type = Common.Unpack<TVector>();
            var relationAttribute = Attribute.GetCustomAttribute(type.Relation, typeof(RelationshipAttribute)) as RelationshipAttribute;
            var S = N(type.Source);
            var T = N(type.Target);
            if (relationAttribute.Direction == RelationshipDirection.Outgoing)
            {
                return "(:" + S + " { Guid: {sourceGuid} })-[r:" + relationAttribute.Type + "]->(:" + T + " { Guid: {targetGuid} })";
            }
            else
            {
                return "(:" + S + " { Guid: {sourceGuid} })<-[r:" + relationAttribute.Type + "]-(:" + T + " { Guid: {targetGuid} })";
            }
        }

        string VectorPattern(VectorType vectorType)
        {
            var S = N(vectorType.Source);
            var T = N(vectorType.Target);
            var relationAttribute = Attribute.GetCustomAttribute(vectorType.Relation, typeof(RelationshipAttribute)) as RelationshipAttribute;
            if (relationAttribute.Direction == RelationshipDirection.Outgoing)
            {
                return "(:" + S + " { Guid: {sourceGuid} })-[r:" + relationAttribute.Type + "]->(:" + T + " { Guid: {targetGuid} })";
            }
            else
            {
                return "(:" + S + " { Guid: {sourceGuid} })<-[r:" + relationAttribute.Type + "]-(:" + T + " { Guid: {targetGuid} })";
            }
        }

        string VectorPattern(VectorType vectorType, string sourceGuid, string targetGuid)
        {
            var S = N(vectorType.Source);
            var T = N(vectorType.Target);
            var attribute = Attribute.GetCustomAttribute(vectorType.Relation, typeof(RelationshipAttribute)) as RelationshipAttribute;
            if (attribute.Direction == RelationshipDirection.Outgoing)
            {
                return "(:" + S + " { Guid: {sourceGuid} })-[r:" + attribute.Type + "]->(:" + T + " { Guid: {targetGuid} })";
            }
            else
            {
                return "(:" + S + " { Guid: {sourceGuid} })<-[r:" + attribute.Type + "]-(:" + T + " { Guid: {targetGuid} })";
            }
        }

        string VectorPattern(RelationshipAttribute attribute, Type sourceType, string sourceGuid, Type targetType, string targetGuid)
        {
            var S = N(sourceType);
            var T = N(targetType);
            if (attribute.Direction == RelationshipDirection.Outgoing)
            {
                return "(:" + S + " { Guid: {sourceGuid} })-[r:" + attribute.Type + "]->(:" + T + " { Guid: {targetGuid} })";
            }
            else
            {
                return "(:" + S + " { Guid: {sourceGuid} })<-[r:" + attribute.Type + "]-(:" + T + " { Guid: {targetGuid} })";
            }
        }

        protected virtual ICypherFluentQuery FromCode<T>(string code = null, string key = null) where T : Root, ICode
        {
            key = key ?? Common.GraphNodeKey<T>();
            var query = graph.Match($"({key}:{N<T>()})");
            if (code.HasValue())
            {
                query = query.Where($"{key}.Code = '{code}'");
            }
            return query;
        }

        protected virtual ICypherFluentQuery From<T>(string key = null) where T : Root
        {
            key = key ?? Common.GraphNodeKey<T>();
            return graph.Match($"({key}:{N<T>()})");
        }

        protected async Task<TSearch> PageAsync<TEntity, TSearch>(
            Search<TEntity> query,
            ICypherFluentQuery records,
            Expression<Func<ICypherResultItem, TEntity>> selector = null,
            OrderBy orderBy = null,
            string entityKey = null)

            where TEntity : class
            where TSearch : Search<TEntity>, new()
        {
            entityKey = GetEntityKey(selector, entityKey);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            query.Count = (int)(await records.CountAsync(entityKey));
            query.MaxPage = (int)(query.Count / query.PageRows) + 1;
            if (query.Page < 1)
            {
                query.Page = 1;
            }
            if (query.Page > query.MaxPage)
            {
                query.Page = query.MaxPage;
            }
            int? skip = null, limit = null;
            if (false == query.Infinite)
            {
                skip = (query.Page - 1) * query.PageRows;
                limit = query.PageRows;
            }
            if (selector == null)
            {
                selector = As<TEntity>(entityKey);
            }
            query.Results = await records.ToListAsync(selector, skip: skip, limit: limit, orderBy: orderBy.__(x => x.Render()));
            stopwatch.Stop();
            query.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            return query.Downcast<TSearch>();
        }

        string GetEntityKey<TEntity>(Expression<Func<ICypherResultItem, TEntity>> selector, string entityKey) where TEntity : class
        {
            return selector.__(exp => exp.Parameters.FirstOrDefault().__(paramexp => paramexp.Name)) ?? entityKey ?? SearchEntityNodeKey<TEntity>();
        }

        static string SearchEntityNodeKey<TEntity>() where TEntity : class
        {
            var type = typeof(TEntity);
            var parentType = type.UnderlyingSystemType.BaseType;
            if (parentType.BaseType == typeof(Graph))
            {
                var graphArgs = parentType.GetTypeInfo().GenericTypeArguments;
                type = graphArgs[0];
            }
            if (parentType.BaseType == typeof(Cluster))
            {
                var clusterArgs = parentType.GetTypeInfo().GenericTypeArguments;
                type = clusterArgs[0];
            }
            return Common.NodeKey(type);
        }

        /// <summary>
        /// Returns a lambda expression that allows the <paramref name="startNode"/> value to be used dynamically in the Return.As cast.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="startNode"></param>
        /// <returns></returns>
        static Expression<Func<ICypherResultItem, TEntity>> As<TEntity>(string startNode) where TEntity : class
        {
            return LambdaGenericMethodCall<ICypherResultItem, TEntity>(startNode, "As");
        }

        protected static Expression<Func<TTarget, TEntity>> LambdaGenericMethodCall<TTarget, TEntity>(string lambdaParameter, string methodName) where TEntity : class
        {
            var parameter = Expression.Parameter(typeof(TTarget), lambdaParameter);
            var callee = Expression.Call(parameter, typeof(TTarget).GetGenericMethod<TEntity>(methodName));
            return Expression.Lambda<Func<TTarget, TEntity>>(callee, parameter);
        }

        public async Task<List<T>> AllAsync<T>(string orderBy = null) where T : Root
        {
            return await graph.Match($"(x:{N<T>()})")
                              .ToListAsync(x => x.As<T>(), orderBy);
        }

        protected TransactionScope NewScope()
        {
            return new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        }

        public async Task<Result> SaveAsync<TEntity>(Guid? guid, TEntity entity, Action<TEntity> insert = null, Action<TEntity> update = null) where TEntity : Entity
        {
            using (var scope = NewScope())
            {
                try
                {
                    if (entity.Guid.GetValueOrDefault() == Guid.Empty)
                    {
                        entity.Guid = Guid.NewGuid();
                        if (insert != null)
                        {
                            insert(entity);
                        }
                    }
                    else
                    {
                        if (update != null)
                        {
                            update(entity);
                        }
                    }
                    var query = graph
                        .Merge("(entity:" + N<TEntity>() + " { Guid: {guid} })")
                        .OnCreate().Set("entity = {entity}")
                        .OnMatch().Set("entity = {entity}")
                        .WithParams(new { guid = entity.Guid, entity });
                    await query.ExecuteWithoutResultsAsync();
                    scope.Complete();
                    return Success();
                }
                catch (NeoException ex)
                {
                    return Error(new { entity }, ex);
                }
                catch (Exception ex)
                {
                    return Error(new { entity }, ex);
                }
                finally
                {
                    scope.Dispose();
                }
            }
        }

        /// <summary>
        /// collect(distinct { Relation: <paramref name="relVar"/>, Target: <paramref name="targetVar"/> })
        /// </summary>
        /// <param name="relVar"></param>
        /// <param name="targetVar"></param>
        /// <returns></returns>
        protected string Vector(string relVar, string targetVar)
        {
            return "collect(distinct({ Relation: " + relVar + ", Target: " + targetVar + " }))";
        }

        protected string Vector(string targetVar)
        {
            return "collect(distinct({ Target: " + targetVar + " }))";
        }

        /// <summary>
        /// NodeLabel
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <returns></returns>
        public string N<TEntity>()
        {
            return Common.NodeLabel<TEntity>();
        }

        public string N(Type type)
        {
            return type.Label();
        }

        /// <summary>
        /// RelationType
        /// </summary>
        /// <typeparam name="TRel"></typeparam>
        /// <returns></returns>
        public string R<TRel>() where TRel : Relation
        {
            return Common.RelationType<TRel>();
        }

        public string R(Type type)
        {
            return Common.RelationType(type);
        }

        public async Task<Result> UpdateAsync<TEntity>(Guid guid, Action<TEntity> update = null) where TEntity : Root, new()
        {
            return await SaveOrUpdateAsync(new TEntity { Guid = guid }, update: update);
        }

        public async Task<Result> SaveOrUpdateAsync<TRoot>(TRoot entity, Action<TRoot> insert = null, Action<TRoot> update = null) where TRoot : Root, new()
        {
            using (var scope = NewScope())
            {
                try
                {
                    var save = await SaveOrUpdateInsideScopeAsync(entity, insert, update);
                    scope.Complete();
                    return save;
                }
                catch (Exception ex)
                {
                    return Error(ex);
                }
                finally
                {
                    scope.Dispose();
                }
            }
        }

        public async Task<Result> SaveOrUpdateInsideScopeAsync<TRoot>(TRoot entity, Action<TRoot> insert = null, Action<TRoot> update = null) where TRoot : Root, new()
        {
            if (entity.Guid.GetValueOrDefault() == Guid.Empty)
            {
                entity.Guid = Guid.NewGuid();
                if (entity is IDateAddedUTC)
                {
                    ((IDateAddedUTC)entity).DateAddedUTC = DateTime.UtcNow;
                }
                if (insert != null)
                {
                    insert(entity);
                }
            }
            else
            {
                var existing = await FindAsync<TRoot>(entity.Guid.Value);
                if (existing == null)
                {
                    return NotFound(new { label = N<TRoot>(), guid = entity.Guid });
                }
                if (entity is IDateModifiedUTC)
                {
                    ((IDateModifiedUTC)existing).DateModifiedUTC = DateTime.UtcNow;
                }
                if (update != null)
                {
                    update(existing);
                }
                entity = existing;
            }
            var save = await SaveAsync(entity);
            return save;
        }

        public async Task<Result> SaveAsync<TRoot>(TRoot entity) where TRoot : Root
        {
            try
            {
                var query = graph
                    .Merge("(e:" + N<TRoot>() + " { Guid: {guid} })")
                    .OnCreate().Set("e = {entity}")
                    .OnMatch().Set("e = {entity}")
                    .WithParams(new { guid = entity.Guid, entity });
                await query.ExecuteWithoutResultsAsync();
                return Success();
            }
            catch (Exception ex)
            {
                return Error(new { entity }, ex);
            }
        }

        public async Task<Result> DeleteAsync<TRel, TSource, TTarget>(Vector<TRel, TSource, TTarget> vector, bool replace = false)
            where TRel : Relation, new()
            where TSource : Root
            where TTarget : Root
        {
            try
            {
                string pattern = "";
                var attr = Common.Attribute<RelationshipAttribute>(typeof(TRel));
                if (attr.Direction == RelationshipDirection.Outgoing)
                {
                    pattern = $"(x:{N<TSource>()})-[r:{R<TRel>()}]->(y:{N<TTarget>()})";
                }
                else
                {
                    pattern = $"(x:{N<TSource>()})<-[r:{R<TRel>()}]-(y:{N<TTarget>()})";
                }
                var query = graph.Match(pattern).Where();
                if (vector.Source.__(x => x.Guid.GetValueOrDefault() != Guid.Empty))
                {
                    query = query.AndWhere((IGuid x) => x.Guid == vector.Source.Guid);
                }
                if (vector.Target.__(x => x.Guid.GetValueOrDefault() != Guid.Empty))
                {
                    query = query.AndWhere((IGuid y) => y.Guid == vector.Target.Guid);
                }
                if (vector.Relation.__(x => x.Guid.HasValue))
                {
                    query = query.AndWhere((IGuid r) => r.Guid == vector.Relation.Guid);
                }
                await query.Delete("r")
                           .ExecuteWithoutResultsAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Error(new { vector }, ex);
            }
        }

        public async Task<THyperVector> FindHyperVectorAsync<THyperVector>(Guid relationId) where THyperVector : HyperVector
        {
            var vectorType = Common.UnpackHyperVector<THyperVector>();
            var leftPattern = PatternPart(vectorType.Item1, Side.Left);
            var rightPattern = PatternPart(vectorType.Item2, Side.Right);
            var pattern = leftPattern + rightPattern;
            var query = graph.Match(pattern).Where((IGuid r1) => r1.Guid == relationId);
            var result = await query.FirstOrDefaultAsync(() => Return.As<THyperVector>("{ Left: { Source: x1, Relation: r1, Target: y1 }, Right: { Source: y1, Relation: r2, Target: y2 } }"));
            return result;
        }

        enum Side
        {
            Left,
            Right
        }

        string PatternPart(VectorType vectorType, Side side)
        {
            var suffix = side == Side.Left ? "1" : "2";
            var R = this.R(vectorType.Relation);
            var S = N(vectorType.Source);
            var T = N(vectorType.Target);
            var relationshipAttribute = Common.Attribute<RelationshipAttribute>(vectorType.Relation);
            string pattern = "";
            if (relationshipAttribute.Direction == RelationshipDirection.Outgoing)
            {
                var source = side == Side.Left ? $"(x{suffix}:{S})-" : "-";
                pattern = $"{source}[r{suffix}:{R}]->(y{suffix}:{T})";
            }
            else
            {
                var source = side == Side.Left ? $"(x{suffix}:{S})<-" : "-";
                pattern = $"{source}[r{suffix}:{R}]-(y{suffix}:{T})";
            }
            return pattern;
        }

        public async Task<TVector> FindVectorAsync<TVector>(Guid relationId) where TVector : Vector
        {
            var vectorType = Common.Unpack<TVector>();
            var pattern = PatternPart(vectorType, Side.Left);
            var query = graph.Match(pattern).Where((IGuid r1) => r1.Guid == relationId);
            var result = await query.FirstOrDefaultAsync(() => Return.As<TVector>("{ Source: x1, Relation: r1, Target: y1 }"));
            return result;
        }

        public async Task<VectorIdent> FindVectorIdentAsync<TVector>(Guid relationId) where TVector : Vector
        {
            var vectorType = Common.Unpack<TVector>();
            var R = this.R(vectorType.Relation);
            var S = N(vectorType.Source);
            var T = N(vectorType.Target);
            var relationshipAttribute = Common.Attribute<RelationshipAttribute>(vectorType.Relation);
            string pattern = "";
            if (relationshipAttribute.Direction == RelationshipDirection.Outgoing)
            {
                pattern = $"(x:{S})-[r:{R}]->(y:{T})";
            }
            else
            {
                pattern = $"(x:{S})<-[r:{R}]-(y:{T})";
            }
            var query = graph.Match(pattern).Where((IGuid r) => r.Guid == relationId);
            var result = await query.FirstOrDefaultAsync(() => new VectorIdent
            {
                SourceId = Return.As<Guid>("x.Guid"),
                RelationId = Return.As<Guid>("r.Guid"),
                TargetId = Return.As<Guid>("y.Guid")
            });
            return result;
        }

        public async Task<Result> RelateAsync<TRel, TSource, TTarget>(Vector<TRel, TSource, TTarget> vector, bool replace = false)
            where TRel : Relation, new()
            where TSource : Root
            where TTarget : Root
        {
            try
            {
                if (vector.Relation == null)
                {
                    vector.Relation = new TRel();
                }
                if (false == vector.Relation.Guid.HasValue)
                {
                    vector.Relation.Guid = Guid.NewGuid();
                }
                var query = graph.Match(
                    "(source:" + N<TSource>() + " { Guid: {sourceGuid} })",
                    "(target:" + N<TTarget>() + " { Guid: {targetGuid} })")
                    .WithParams(new { sourceGuid = vector.Source.Guid, targetGuid = vector.Target.Guid })
                    .CreateUnique(VectorPattern(vector))
                    .WithParams(new { relation = vector.Relation });
                await query.ExecuteWithoutResultsAsync();

                return Success();
            }
            catch (Exception ex)
            {
                return Error(new { vector }, ex);
            }
        }

        public async Task<Result> DeleteAsync<TVector>(VectorIdent ident)
            where TVector : Vector
        {
            try
            {
                string pattern = "";
                var vectorType = Common.Unpack<TVector>();
                var attr = Common.Attribute<RelationshipAttribute>(vectorType.Relation);
                if (attr.Direction == RelationshipDirection.Outgoing)
                {
                    pattern = $"(x:{N(vectorType.Source)})-[r:{R(vectorType.Relation)}]->(y:{N(vectorType.Target)})";
                }
                else
                {
                    pattern = $"(x:{N(vectorType.Source)})<-[r:{R(vectorType.Relation)}]-(y:{N(vectorType.Target)})";
                }
                var query = graph.Match(pattern).Where();
                if (ident.SourceId != Guid.Empty)
                {
                    query = query.AndWhere((IGuid x) => x.Guid == ident.SourceId);
                }
                //if (ident.TargetId != Guid.Empty)
                //{
                //    query = query.AndWhere((IGuid y) => y.Guid == ident.TargetId);
                //}
                if (ident.RelationId.HasValue)
                {
                    query = query.AndWhere((IGuid r) => r.Guid == ident.RelationId.Value);
                }
                await query.Delete("r")
                           .ExecuteWithoutResultsAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Error(new { TVector = typeof(TVector).UnderlyingSystemType, ident }, ex);
            }
        }

        public async Task<Result> RelateAsync<TVector>(Guid sourceGuid, Guid targetGuid, Guid? relationGuid = null)
            where TVector : Vector
        {
            try
            {
                await graph.Match<TVector>("x", "y", sourceGuid, targetGuid)
                           .Relate<TVector>("x", "y", new Relation { Guid = relationGuid ?? Guid.NewGuid() })
                           .ExecuteWithoutResultsAsync();
                return Success();
            }
            catch (Exception ex)
            {
                return Error(new { TVector = typeof(TVector).UnderlyingSystemType.Name, sourceGuid, targetGuid, relationGuid }, ex);
            }
        }

        public async Task<Result> RelateAsync<TVector>(VectorIdent ident)
            where TVector : Vector
        {
            return await RelateAsync<TVector>(ident.SourceId, ident.TargetId, ident.RelationId);
        }

        string VectorPattern<TRel, TSource, TTarget>(Vector<TRel, TSource, TTarget> vector)
            where TRel : Relation, new()
            where TSource : Root
            where TTarget : Root
        {
            var attribute = Common.Attribute<RelationshipAttribute>(vector.Relation);
            if (attribute.Direction == RelationshipDirection.Outgoing)
            {
                return "(source)-[r:" + R<TRel>() + " {relation}]->(target)";
            }
            else
            {
                return "(source)<-[r:" + R<TRel>() + " {relation}]-(target)";
            }
        }



        /// <summary>
        /// Hard-deletes the node and attached relationships.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="guid"></param>
        /// <returns></returns>
        public async Task<Result> PurgeNodeAsync<TEntity>(Guid guid) where TEntity : Entity
        {
            try
            {
                var query = graph.Match("(x:" + N<TEntity>() + " { Guid: {guid} })-[r]-()").WithParams(new { guid })
                                 .Delete("r, x");
                await query.ExecuteWithoutResultsAsync();
                return Success();
            }
            catch (Exception ex)
            {
                return Error(new { guid, TEntity = N<TEntity>() }, ex);
            }
        }

        public async Task<Result> DeleteRelationAsync<TRel>(Guid guid) where TRel : Relation
        {
            try
            {
                var query = graph.Match("()-[r:" + R<TRel>() + " { Guid: {guid} }]-()")
                                 .WithParams(new { guid })
                                 .Delete("r");
                await query.ExecuteWithoutResultsAsync();
                return Success();
            }
            catch (Exception ex)
            {
                return Error(new { guid, TRel = R<TRel>() }, ex);
            }
        }

        public async Task<Result> DeleteRelationAsync(Relation relation)
        {
            try
            {
                if (relation == null)
                {
                    return Rejected();
                }
                if (false == relation.Guid.HasValue)
                {
                    return Rejected();
                }
                var query = graph.Match("(x)-[r:" + R(relation) + " { Guid: {guid} }]->(y)")
                           .WithParams(new { guid = relation.Guid })
                           .Delete("r");
                await query.ExecuteWithoutResultsAsync();
                return Success();
            }
            catch (Exception ex)
            {
                return Error(new { relation }, ex);
            }
        }

        public T Find<T>(Guid guid) where T : Root
        {
            return FindQuery<T>(guid).FirstOrDefault(x => x.As<T>());
        }

        protected async Task<T> FindByNameAsync<T>(string name) where T : IName
        {
            return await graph.Match("(x:" + N<T>() + " { Name: {name} })").WithParams(new { name })
                              .FirstOrDefaultAsync(x => x.As<T>());
        }

        protected async Task<T> FindByCodeAsync<T>(string code) where T : Entity
        {
            return await graph.Match("(x:" + N<T>() + " { Code: {code} })").WithParams(new { code })
                              .FirstOrDefaultAsync(x => x.As<T>());
        }

        protected async Task<T> FindAsync<T>(Guid guid) where T : Root
        {
            return await FindQuery<T>(guid).FirstOrDefaultAsync(x => x.As<T>());
        }

        /// <summary>
        /// Returns (x)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="guid"></param>
        /// <returns></returns>
        ICypherFluentQuery FindQuery<T>(Guid guid) where T : IGuid
        {
            return graph.Match("(x:" + N<T>() + " { Guid: {guid} })").WithParams(new { guid });
        }

        #region helpers
        protected string R(Relation relation)
        {
            return Common.RelationType(relation);
        }

        protected Result Error(Exception ex)
        {
            Log.Error(ex);
            return Result.Error(exception: ex);
        }

        protected Result Success()
        {
            return Result.Success();
        }

        protected Result<T> Success<T>(T data)
        {
            return Result.Success(data);
        }

        protected Result Error(dynamic value, NeoException ex)
        {
            Log.Error(value, ex);
            return Result.Error(exception: ex);
        }

        protected Result Error(dynamic value, Exception ex)
        {
            Log.Error(value, ex);
            return Result.Error(exception: ex);
        }

        protected Result Rejected(string message = null)
        {
            Log.Error(message);
            return Result.Rejected(message);
        }

        protected Result NotFound(dynamic data = null)
        {
            Log.Error(data);
            return Result.Rejected().NotFound();
        }

        protected Result<T> Rejected<T>(string message = null)
        {
            return Result.Rejected<T>(message);
        }

        protected Result<T> Unauthorized<T>(string message = null)
        {
            return Result.Unauthorized<T>(message);
        }

        protected Result<T> Error<T>(dynamic value, Exception ex)
        {
            Log.Error(value, ex);
            return Result.Error<T>(exception: ex);
        }

        public void Dispose()
        {
            db.Client.Dispose();
        }
        #endregion
    }
}
