using MongoDB.Driver;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    public class ProveedorRepository : BaseRepository<Proveedor>, IProveedorRepository
    {
        public ProveedorRepository(MongoDbContext context) : base(context.Proveedores) { }

        public async Task<PaginatedResult<Proveedor>> SearchAsync(
            string? nombre,
            string? categoria,
            bool? activo,
            int page = 1,
            int pageSize = 10)
        {
            var builder = Builders<Proveedor>.Filter;
            var filter = builder.Eq(p => p.IsDeleted, false);

            if (!string.IsNullOrWhiteSpace(nombre))
                filter &= builder.Regex(p => p.Nombre, new MongoDB.Bson.BsonRegularExpression(nombre, "i"));

            // Filtra proveedores que suministran una categoria especifica
            if (!string.IsNullOrWhiteSpace(categoria))
                filter &= builder.AnyEq(p => p.CategoriaSuministro, categoria);

            if (activo.HasValue)
                filter &= builder.Eq(p => p.Activo, activo.Value);

            var totalCount = await _collection.CountDocumentsAsync(filter);

            var items = await _collection.Find(filter)
                .SortBy(p => p.Nombre)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PaginatedResult<Proveedor>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}
