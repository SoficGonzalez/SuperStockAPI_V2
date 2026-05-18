using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implementacion base de repositorio para MongoDB.
    /// Reemplaza el BaseRepository de EF Core del proyecto Tickets.
    ///
    /// Diferencias clave vs EF Core:
    ///   - No hay DbContext.SaveChanges(); cada operacion es atomica.
    ///   - No hay change tracking; cada update es un ReplaceOne explicito.
    ///   - El Id es string mapeado a ObjectId via BsonRepresentation en BaseEntity.
    /// </summary>
    public abstract class BaseRepository<TEntity> : IBaseRepository<TEntity>
        where TEntity : BaseEntity
    {
        protected readonly IMongoCollection<TEntity> _collection;

        protected BaseRepository(IMongoCollection<TEntity> collection)
        {
            _collection = collection;
        }

        /// <summary>
        /// Construye el filtro por _id apuntando directamente al campo BSON
        /// con un ObjectId explicito. Evita ambiguedades de serializacion.
        /// </summary>
        private static FilterDefinition<TEntity> BuildIdFilter(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return Builders<TEntity>.Filter.Eq("_id", id);
            }
            return Builders<TEntity>.Filter.Eq("_id", objectId);
        }

        public async Task<TEntity> AddAsync(TEntity entity)
        {
            // MongoDB genera el _id automaticamente si Id esta vacio.
            if (string.IsNullOrWhiteSpace(entity.Id))
                entity.Id = ObjectId.GenerateNewId().ToString();

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            await _collection.InsertOneAsync(entity);
            return entity;
        }

        public async Task<TEntity?> GetByIdAsync(string id)
        {
            var filter = Builders<TEntity>.Filter.And(
                BuildIdFilter(id),
                Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false)
            );

            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<PaginatedResult<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            int page = 1,
            int pageSize = 10)
        {
            // Siempre excluir soft-deleted
            var filter = Builders<TEntity>.Filter.And(
                Builders<TEntity>.Filter.Where(predicate),
                Builders<TEntity>.Filter.Eq(e => e.IsDeleted, false)
            );

            var totalCount = await _collection.CountDocumentsAsync(filter);

            var items = await _collection.Find(filter)
                .SortByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PaginatedResult<TEntity>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<TEntity> UpdateAsync(string id, TEntity entity)
        {
            // Asegurar que el Id de la entidad coincide con el parametro
            // ANTES de serializar, para que el documento tenga el _id correcto.
            entity.Id = id;
            entity.UpdatedAt = DateTime.UtcNow;

            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException($"Id invalido: {id}");

            // Filtro construido directamente con ObjectId, sin pasar por
            // la serializacion del lambda Eq(e => e.Id, id).
            var filter = Builders<TEntity>.Filter.Eq("_id", objectId);

            var options = new ReplaceOptions { IsUpsert = false };
            var result = await _collection.ReplaceOneAsync(filter, entity, options);

            if (result.MatchedCount == 0)
                throw new KeyNotFoundException($"Entidad '{id}' no encontrada.");

            return entity;
        }

        /// <summary>
        /// Soft delete: marca IsDeleted = true en vez de borrar fisicamente.
        /// </summary>
        public async Task<bool> DeleteAsync(string id)
        {
            var update = Builders<TEntity>.Update
                .Set(e => e.IsDeleted, true)
                .Set(e => e.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(BuildIdFilter(id), update);

            return result.ModifiedCount > 0;
        }
    }
}