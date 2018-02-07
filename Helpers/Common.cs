using Neo4jClient;
using Neo4jClientVector.Attributes;
using Neo4jClientVector.Models;
using Neo4jClientVector.Nodes;
using Neo4jClientVector.Relationships;
using Neo4jClientVector.Helpers;
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
            return Attribute<RelationshipAttribute>(relationType).Type;
        }

        public static string Vector<TVector>(string rel = null, string from = null, string to = null, string relPath = null, bool fromLabel = true) where TVector : Vector
        {
            return PatternInternal(typeof(TVector), rel, from, to, relPath, fromLabel: fromLabel);
        }

        public static string JoinVector<TVector>(string rel = null, string to = null, string relPath = null) where TVector : Vector
        {
            return JoinPatternInternal(typeof(TVector), rel: rel, to: to, relPath: relPath);
        }

        public static string Pattern<TRel, TSource, TTarget>(Vector<TRel, TSource, TTarget> vector, string rel = null, string from = null, string to = null, bool fromLabel = true)
            where TRel : Relation, new()
            where TSource : Entity
            where TTarget : Entity
        {
            return PatternInternal(vector.GetType(), rel, from, to, fromLabel: fromLabel);
        }

        public static string Pattern(Type vectorType, string rel = null, string from = null, string to = null, bool fromLabel = true)
        {
            var genericVectorType = vectorType.UnderlyingSystemType.BaseType;
            return PatternInternal(genericVectorType, rel, from, to, fromLabel: fromLabel);
        }

        public static string PatternInternal(Type vectorType, string rel = null, string from = null, string to = null, string relPath = null, bool fromLabel = true)
        {
            relPath = relPath ?? "";
            var type = Unpack(vectorType);
            var relationAttribute = Attribute<RelationshipAttribute>(type.Relation);
            var RType = GraphRelationshipName(type.Relation);
            var SLabel = fromLabel ? ":" + type.Source.Label() : "";
            var TLabel = type.Target.Label();
            var RKey = rel ?? RelationshipKey(type.Relation);
            var SKey = from ?? SourceNodeKey(type.Source);
            var TKey = to ?? TargetNodeKey(type.Target);
            var pattern = "";
            if (relationAttribute.Direction == RelationshipDirection.Outgoing)
            {
                pattern = $"({SKey}{SLabel})-[{RKey}:{RType}{relPath}]->({TKey}:{TLabel})";
            }
            else
            {
                pattern = $"({SKey}{SLabel})<-[{RKey}:{RType}{relPath}]-({TKey}:{TLabel})";
            }
            return pattern;
        }

        public static string JoinPatternInternal(Type vectorType, string rel = null, string to = null, string relPath = null)
        {
            relPath = relPath ?? "";
            var type = Unpack(vectorType);
            var relationAttribute = Attribute<RelationshipAttribute>(type.Relation);
            var RType = GraphRelationshipName(type.Relation);
            var TLabel = type.Target.Label();
            var RKey = rel ?? RelationshipKey(type.Relation);

            var TKey = to ?? TargetNodeKey(type.Target);
            var pattern = "";
            if (relationAttribute.Direction == RelationshipDirection.Outgoing)
            {
                pattern = $"-[{RKey}:{RType}{relPath}]->({TKey}:{TLabel})";
            }
            else
            {
                pattern = $"<-[{RKey}:{RType}{relPath}]-({TKey}:{TLabel})";
            }
            return pattern;
        }

        public static string RelationshipKey(Type type)
        {
            var name = GraphRelationshipName(type);
            var parts = name.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Substring(0, 1).ToLower());
            var abbrev = string.Join("", parts);
            return Attribute<RelationshipAttribute>(type).__(x => x.Key) ?? abbrev;
        }

        public static string GraphRelationshipName(Type type)
        {
            return Attribute<RelationshipAttribute>(type).__(x => x.Type);
        }

        public static string GraphRelationshipName<TRel>() where TRel : Relation
        {
            return Attribute<RelationshipAttribute, TRel>().__(x => x.Type);
        }

        public static string GraphRelationshipName<TRel>(TRel relation) where TRel : Relation
        {
            return Attribute<RelationshipAttribute>(relation).__(x => x.Type);
        }

        public static string GraphNodeKey<TEntity>() where TEntity : Root
        {
            var type = typeof(TEntity);
            return Attribute<NodeAttribute>(type).__(x => x.Key) ?? type.Name.Abbreviate();
        }

        public static string NodeKey(Type type)
        {
            return Attribute<NodeAttribute>(type).__(x => x.Key) ?? type.Name.Abbreviate();
        }

        public static string SourceNodeKey(Type type)
        {
            return Attribute<SourceNodeAttribute>(type).__(x => x.Key) ?? NodeKey(type);
        }

        public static string TargetNodeKey(Type type)
        {
            return Attribute<TargetNodeAttribute>(type).__(x => x.Key) ?? NodeKey(type);
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

        public static Tuple<VectorType, VectorType> UnpackHyperVector<THyperVector>() where THyperVector : HyperVector
        {
            return Memoizer.GetOrSet(() => InternalUnpackHyperVector(typeof(THyperVector)), TimeSpan.FromDays(1), typeof(THyperVector));
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

        public static Tuple<VectorType, VectorType> InternalUnpackHyperVector(Type specificVectorType)
        {
            var genericHyperVectorType = specificVectorType.UnderlyingSystemType.BaseType;
            if (false == genericHyperVectorType.BaseType.IsAssignableFrom(typeof(HyperVector)))
            {
                throw new ArgumentException("specificVectorType needs to inherit from HyperVector", "specificVectorType");
            }
            var args = genericHyperVectorType.GetTypeInfo().GenericTypeArguments;
            var left = InternalUnpack(args[0]);
            var right = InternalUnpack(args[1]);            
            return new Tuple<VectorType, VectorType>(left, right);
        }

        public static string NodeLabel<TEntity>()
        {
            return Attribute<NodeAttribute, TEntity>().__(x => x.Label) ?? typeof(TEntity).UnderlyingSystemType.Name;
        }

        public static string NodeLabel(Type entityType)
        {
            return Attribute<NodeAttribute>(entityType).__(x => x.Label) ?? entityType.UnderlyingSystemType.Name;
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

