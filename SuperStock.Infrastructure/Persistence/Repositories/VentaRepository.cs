using MongoDB.Driver;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    public class VentaRepository : BaseRepository<Venta>, IVentaRepository
    {
        public VentaRepository(MongoDbContext context) : base(context.Ventas) { }

        public async Task<PaginatedResult<Venta>> SearchAsync(
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string? cajeroId,
            string? estado,
            int page = 1,
            int pageSize = 10)
        {
            var builder = Builders<Venta>.Filter;
            var filter = builder.Eq(v => v.IsDeleted, false);

            // Filtro 1: rango de fechas
            if (fechaDesde.HasValue)
                filter &= builder.Gte(v => v.Fecha, fechaDesde.Value);
            if (fechaHasta.HasValue)
                filter &= builder.Lte(v => v.Fecha, fechaHasta.Value);

            // Filtro 2: por cajero
            if (!string.IsNullOrWhiteSpace(cajeroId))
                filter &= builder.Eq(v => v.Cajero.UsuarioId, cajeroId);

            // Filtro 3: por estado (completada / anulada)
            if (!string.IsNullOrWhiteSpace(estado))
                filter &= builder.Eq(v => v.Estado, estado);

            var totalCount = await _collection.CountDocumentsAsync(filter);

            var items = await _collection.Find(filter)
                .SortByDescending(v => v.Fecha)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return new PaginatedResult<Venta>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}
