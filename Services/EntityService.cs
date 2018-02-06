using Neo4jClientVector.Core.Services;
using Neo4jClientVector.Contexts;
using Neo4jClientVector.Models;
using Neo4jClientVector.Nodes;
using System;
using System.Threading.Tasks;

namespace Neo4jClientVector.Services
{
    public interface IEntityService<TEntity> : IService<TEntity> where TEntity : Entity
    {
        Task<Result> DeleteNodeAsync(Guid guid);
        Task<Result> UndeleteNodeAsync(Guid guid);
        Task<Result> SaveOrUpdateAsync(TEntity data);
    }
    public abstract class EntityService<TEntity> : Service<TEntity> where TEntity : Entity, new()
    {
        #region constructor
        public EntityService(IGraphDataContext _db) : base(_db)
        {

        }
        #endregion

        /// <summary>
        /// Soft-deletes the node.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="guid"></param>
        /// <returns></returns>
        public async Task<Result> DeleteNodeAsync(Guid guid)
        {
            var existing = await FindAsync<TEntity>(guid);
            if (existing == null)
            {
                return Rejected().NotFound();
            }
            existing.IsDeleted = true;
            return await SaveAsync(existing);
        }

        /// <summary>
        /// Soft-undeletes the node.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="guid"></param>
        /// <returns></returns>
        public async Task<Result> UndeleteNodeAsync(Guid guid) 
        {
            var existing = await FindAsync<TEntity>(guid);
            if (existing == null)
            {
                return Rejected().NotFound();
            }
            existing.IsDeleted = false;
            return await SaveAsync(existing);
        }

        public abstract Task<Result> SaveOrUpdateAsync(TEntity data);

        public async Task<Result> UpdateAsync(Guid guid, Action<TEntity> update = null)
        {
            return await UpdateAsync<TEntity>(guid, update);
        }
    }
}
