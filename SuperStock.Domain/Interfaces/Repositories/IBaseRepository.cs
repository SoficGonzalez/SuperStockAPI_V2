using System.Linq.Expressions;
using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Interfaz base para repositorios Cassandra.
    /// Id es Guid (UUID en CQL).
    /// Se removio FindAsync con expresiones LINQ porque Cassandra no soporta
    /// LINQ contra el cluster como Mongo lo hace; los filtros se construyen
    /// directamente en cada repositorio concreto con CQL.
    /// </summary>
    public interface IBaseRepository<TEntity> where TEntity : BaseEntity
    {
        Task<TEntity> AddAsync(TEntity entity);
        Task<TEntity?> GetByIdAsync(Guid id);
        Task<TEntity> UpdateAsync(Guid id, TEntity entity);
        Task<bool> DeleteAsync(Guid id);
    }
}
