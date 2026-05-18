using MongoDB.Bson;
using MongoDB.Driver;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    public class ProductoRepository : BaseRepository<Producto>, IProductoRepository
    {
        public ProductoRepository(MongoDbContext context) : base(context.Productos) { }

        public async Task<PaginatedResult<Producto>> SearchAsync(
            string? categoria,
            string? nombre,
            bool? stockBajo,
            bool? activo,
            int page = 1,
            int pageSize = 10)
        {
            var builder = Builders<Producto>.Filter;

            var filter = builder.Eq(p => p.IsDeleted, false);

            if (!string.IsNullOrWhiteSpace(categoria))
                filter &= builder.Eq(p => p.Categoria, categoria);

            if (!string.IsNullOrWhiteSpace(nombre))
                filter &= builder.Regex(
                    p => p.Nombre,
                    new BsonRegularExpression(nombre, "i"));

            if (stockBajo == true)
                filter &= builder.Where(p => p.StockActual <= p.StockMinimo);

            if (activo.HasValue)
                filter &= builder.Eq(p => p.Activo, activo.Value);

            var totalCount = await _collection.CountDocumentsAsync(filter);

            var items = await _collection.Find(filter)
                .SortBy(p => p.Nombre)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PaginatedResult<Producto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<Producto?> GetByCodigoBarrasAsync(string codigoBarras)
        {
            var filter = Builders<Producto>.Filter.Eq(p => p.CodigoBarras, codigoBarras)
                       & Builders<Producto>.Filter.Eq(p => p.IsDeleted, false);

            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Producto?> GetByMongoIdAsync(string id)
        {
            var filters = new List<FilterDefinition<Producto>>();

            if (ObjectId.TryParse(id, out var objectId))
            {
                filters.Add(new BsonDocument("_id", objectId));
            }

            filters.Add(new BsonDocument("_id", id));
            filters.Add(Builders<Producto>.Filter.Eq(p => p.Id, id));

            var filter = Builders<Producto>.Filter.And(
                Builders<Producto>.Filter.Or(filters),
                Builders<Producto>.Filter.Eq(p => p.IsDeleted, false)
            );

            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
    }
}