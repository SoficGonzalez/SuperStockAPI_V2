using System.Linq.Expressions;
using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Interfaz base para todos los repositorios MongoDB.
    /// Misma firma que el original de Tickets pero adaptada:
    ///   - string Id en vez de int Id (ObjectId de Mongo)
    ///   - PaginatedResult para listas
    ///   - Update y Delete agregados (CRUD completo)
    /// </summary>
    public interface IBaseRepository<TEntity> where TEntity : BaseEntity
    {
        Task<TEntity> AddAsync(TEntity entity);
        Task<TEntity?> GetByIdAsync(string id);
        Task<PaginatedResult<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            int page = 1,
            int pageSize = 10);
        Task<TEntity> UpdateAsync(string id, TEntity entity);
        Task<bool> DeleteAsync(string id);
    }
}
