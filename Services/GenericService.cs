﻿using Neo4jClientVector.Core.Services;
using Neo4jClientVector.Contexts;
using Neo4jClientVector.Models;
using Neo4jClientVector.Nodes;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Neo4jClientVector.Relationships;
using Neo4jClientVector.Helpers;
using Neo4jClient.Cypher;
using AutoMapper;

namespace Neo4jClientVector.Services
{
    public interface IService<TEntity> : IService where TEntity : Entity
    {
        Task<TSearch> PageAsync<TSearch>(Search<TEntity> query, ICypherFluentQuery records, Expression<Func<ICypherResultItem, TEntity>> selector = null, OrderBy orderBy = null, string startNode = "x")
            where TSearch : Search<TEntity>, new();
        Task<TEntity> FindAsync(Guid guid);
        TEntity Find(Guid guid);
        Task<List<TEntity>> AllAsync(string orderBy = null);
        Task<TEntity> FindByURICodeAsync(string uriCode);
        Task<Result> DeleteNodeAsync(Guid guid);
        Task<Result> UndeleteNodeAsync(Guid guid);
        Task<Result> SaveOrUpdateAsync(TEntity data);
    }
    public abstract class Service<TEntity> : Service where TEntity : Entity, new()
    {
        #region constructor
        public Service(IGraphDataContext _db) : base(_db)
        {

        }
        #endregion

        protected static string Vector<TVector>(string relationKey = null, string from = null, string to = null, string relPath = null, bool sourceLabel = true) where TVector : Vector
        {
            return Common.Vector<TVector>(relationKey, from, to, relPath, sourceLabel);
        }

        protected async Task<Result> SaveVectorAsync<TVector>(TEntity entity, TVector vector)
            where TVector : Vector
        {
            var ident = ToVectorIdent(vector);
            ident.SourceId = entity.Guid;
            if (ident.RelationId.HasValue)
            {
                await DeleteAsync<TVector>(ident);
            }
            return await RelateAsync<TVector>(ident);
        }

        protected VectorIdent ToVectorIdent<TVector>(TVector vector)
            where TVector : Vector
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TVector, Vector<Relation, Entity, Entity>>();
            });
            var generic = Mapper.Map<Vector<Relation, Entity, Entity>>(vector); // vector as Vector<Relation, Entity, Entity>;
            var ident = new VectorIdent
            {
                SourceId = generic.Source.__(x => x.Guid),
                RelationId = generic.Relation.__(x => x.Guid),
                TargetId = generic.Target.__(x => x.Guid)
            };
            return ident;
        }

        public abstract Task<Result> SaveOrUpdateAsync(TEntity data);

        public async Task<Result> UpdateAsync(Guid guid, Action<TEntity> update = null)
        {
            return await UpdateAsync<TEntity>(guid, update);
        }

        public async Task<Result> UndeleteNodeAsync(Guid guid)
        {
            return await UndeleteNodeAsync<TEntity>(guid);
        }

        public async Task<Result> DeleteNodeAsync(Guid guid)
        {
            return await DeleteNodeAsync<TEntity>(guid);
        }

        public async Task<TEntity> FindByURICodeAsync(string uriCode)
        {
            return await FindByCodeAsync<TEntity>(uriCode);
        }

        public async Task<TSearch> PageAsync<TSearch>(Search<TEntity> query, ICypherFluentQuery records, Expression<Func<ICypherResultItem, TEntity>> selector = null, OrderBy orderBy = null, string startNode = null)
            where TSearch : Search<TEntity>, new()
        {
            return await PageAsync<TEntity, TSearch>(query, records, selector: selector, orderBy: orderBy, startNode: startNode);
        }

        public async Task<List<TEntity>> AllAsync(string orderBy = null)
        {
            return await AllAsync<TEntity>(orderBy: orderBy);
        }

        public async Task<TEntity> FindAsync(Guid guid)
        {
            return await FindAsync<TEntity>(guid);
        }

        public TEntity Find(Guid guid)
        {
            return Find<TEntity>(guid);
        }
    }
}
