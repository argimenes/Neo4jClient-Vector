using Neo4jClientVector.Core.Services;
using Neo4jClient.Cypher;
using Neo4jClientVector.Contexts;
using Neo4jClientVector.Models;
using Neo4jClientVector.Nodes;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Neo4jClientVector.Services
{
    public interface IService<TEntity> : IService where TEntity : Entity
    {
        Task<TSearch> PageAsync<TSearch>(Search<TEntity> query, ICypherFluentQuery records, Expression<Func<ICypherResultItem, TEntity>> selector = null, string orderBy = null, string startNode = "x")
            where TSearch : Search<TEntity>, new();
        Task<TEntity> FindAsync(Guid guid);
        TEntity Find(Guid guid);
        Task<List<TEntity>> AllAsync(string orderBy = null);
        Task<TEntity> FindByURICodeAsync(string uriCode);
        Task<Result> DeleteNodeAsync(Guid guid);
        Task<Result> UndeleteNodeAsync(Guid guid);
    }
    public class Service<TEntity> : Service where TEntity : Entity, new()
    {
        #region constructor
        public Service(IGraphDataContext _db) : base(_db)
        {

        }
        #endregion

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
            return await FindByURICodeAsync<TEntity>(uriCode);
        }

        public async Task<TSearch> PageAsync<TSearch>(Search<TEntity> query, ICypherFluentQuery records, Expression<Func<ICypherResultItem, TEntity>> selector = null, string orderBy = null, string startNode = "x")
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
