using Neo4jClient;
using Neo4jClientVector.Attributes;
using Neo4jClientVector.Models;
using Neo4jClientVector.Nodes;
using Neo4jClientVector.Relationships;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Neo4jClientVector.Helpers
{
    public static class Common
    {
        public static string RelationType(Relation relation)
        {
            return GraphRelationshipName(relation);
        }

        public static string RelationType<TRel>() where TRel : Relation
        {
            return GraphRelationshipName<TRel>();
        }

        public static string RelationType(Type relationType)
        {
            return Attribute<GraphRelationshipAttribute>(relationType).Type;
        }

        public static string Pattern<TVector>(string relationKey = null, string sourceKey = null, string targetKey = null, string relPath = null) where TVector : Vector
        {
            return PatternInternal(typeof(TVector), relationKey, sourceKey, targetKey, relPath);
        }

        public static string Pattern<TRel, TSource, TTarget>(Vector<TRel, TSource, TTarget> vector, string relationKey = null, string sourceKey = null, string targetKey = null)
            where TRel : Relation, new()
            where TSource : Entity
            where TTarget : Entity
        {
            return PatternInternal(vector.GetType(), relationKey, sourceKey, targetKey);
        }

        public static string Pattern(Type vectorType, string relationKey = null, string sourceKey = null, string targetKey = null)
        {
            var genericVectorType = vectorType.UnderlyingSystemType.BaseType;
            return PatternInternal(genericVectorType, relationKey, sourceKey, targetKey);
        }

        private static string PatternInternal(Type vectorType, string relationKey = null, string sourceKey = null, string targetKey = null, string relPath = null)
        {
            relPath = relPath ?? "";
            var type = Unpack(vectorType);
            var relationAttribute = Attribute<GraphRelationshipAttribute>(type.Relation);
            var RType = GraphRelationshipName(type.Relation);
            var SLabel = NodeLabel(type.Source);
            var TLabel = NodeLabel(type.Target);
            var RKey = relationKey ?? GraphRelationshipKey(type.Relation);
            var SKey = sourceKey ?? GraphNodeKey(type.Source);
            var TKey = targetKey ?? GraphNodeKey(type.Target);
            var pattern = "";
            if (relationAttribute.Direction == RelationshipDirection.Outgoing)
            {
                pattern = $"({SKey}:{SLabel})-[{RKey}:{RType}{relPath}]->({TKey}:{TLabel})";
            }
            else
            {
                pattern = $"({SKey}:{SLabel})<-[{RKey}:{RType}{relPath}]-({TKey}:{TLabel})";
            }
            return pattern;
        }

        public static string GraphRelationshipKey(Type type)
        {
            var name = GraphRelationshipName(type);
            var parts = name.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Substring(0, 1).ToLower());
            var abbrev = string.Join("", parts);
            return Attribute<GraphRelationshipAttribute>(type).__(x => x.Key) ?? abbrev;
        }

        public static string GraphRelationshipName(Type type)
        {
            return Attribute<GraphRelationshipAttribute>(type).__(x => x.Type);
        }

        public static string GraphRelationshipName<TRel>() where TRel : Relation
        {
            return Attribute<GraphRelationshipAttribute, TRel>().__(x => x.Type);
        }

        public static string GraphRelationshipName<TRel>(TRel relation) where TRel : Relation
        {
            return Attribute<GraphRelationshipAttribute>(relation).__(x => x.Type);
        }

        public static string GraphNodeKey<TEntity>() where TEntity : Entity
        {
            var type = typeof(TEntity);
            return Attribute<GraphNodeAttribute>(type).__(x => x.Key) ?? type.Name.Abbreviate();
        }

        public static string GraphNodeKey(Type type)
        {
            return Attribute<GraphNodeAttribute>(type).__(x => x.Key) ?? type.Name.Abbreviate();
        }

        public static string Abbreviate(string value)
        {
            return string.Join("", value.SplitCamelCase().Select(x => x.Substring(0, 1).ToLowerInvariant()));
        }

        public static string[] SplitCamelCase(string source)
        {
            return Regex.Split(source, @"(?<!^)(?=[A-Z])");
        }

        public static T Attribute<T>(object obj) where T : Attribute
        {
            var u = obj.GetType().UnderlyingSystemType;
            return u.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T;
        }

        public static T Attribute<T>(Type type) where T : Attribute
        {
            var u = type.UnderlyingSystemType;
            return u.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T;
        }

        public static T Attribute<T, U>() where T : Attribute
        {
            return typeof(U).GetCustomAttributes(typeof(T), true).FirstOrDefault() as T;
        }

        /// <summary>
        /// Unpacks the types that are specified by the generic type arguments.
        /// </summary>
        /// <typeparam name="TVector"></typeparam>
        /// <returns></returns>
        public static VectorType Unpack<TVector>() where TVector : Vector
        {
            return Memoizer.GetOrSet(() => InternalUnpack(typeof(TVector)), TimeSpan.FromDays(1), typeof(TVector));
        }

        /// <summary>
        /// Unpacks the types that are specified by the generic type arguments.
        /// </summary>
        /// <param name="specificVectorType"></param>
        /// <returns></returns>
        public static VectorType Unpack(Type specificVectorType)
        {
            return Memoizer.GetOrSet(() => InternalUnpack(specificVectorType), TimeSpan.FromDays(1), specificVectorType);
        }

        /// <summary>
        /// Unpacks the types that are specified by the generic type arguments.
        /// </summary>
        /// <param name="specificVectorType"></param>
        /// <returns></returns>
        public static VectorType InternalUnpack(Type specificVectorType)
        {
            var genericVectorType = specificVectorType.UnderlyingSystemType.BaseType;
            if (false == genericVectorType.BaseType.IsAssignableFrom(typeof(Vector)))
            {
                throw new ArgumentException("specificVectorType needs to inherit from Vector", "specificVectorType");
            }
            var args = genericVectorType.GetTypeInfo().GenericTypeArguments;
            var data = new VectorType
            {
                GenericVector = genericVectorType,
                Relation = args[0],
                Source = args[1],
                Target = args[2]
            };
            return data;
        }

        public static string NodeLabel<TEntity>()
        {
            return Attribute<GraphNodeAttribute, TEntity>().__(x => x.Name) ?? typeof(TEntity).UnderlyingSystemType.Name;
        }

        public static string NodeLabel(Type entityType)
        {
            return Attribute<GraphNodeAttribute>(entityType).__(x => x.Name) ?? entityType.UnderlyingSystemType.Name;
        }

        public static string ComputeHash(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value.ToLower());
            var hash = MD5.Create().ComputeHash(bytes);
            var encoded = BitConverter.ToString(hash);
            return encoded;
        }

        public static string Name<T>()
        {
            return typeof(T).Name;
        }
        /**
         * .Return((ICypherResultItem agent, ICypherResultItem fact, ICypherResultItem relationship,
                    ICypherResultItem entity, ICypherResultItem subject) => new GenericAgentAgentHyperNode
                    {
                        Agent = agent.As<Agent>(),
                        Act = fact.As<Act>(),
                        Relationship = relationship.Type(),
                        EntityNode = entity.As<Node<string>>(),
                        EntityType = Return.As<string>("head(labels(entity))")
                    }).OrderBy("fact.Guid").Results.ToList()
         */
        public static dynamic Dynamize(Node<string> node)
        {
            return JsonConvert.DeserializeObject<dynamic>(node.Data);
        }

    }
}
