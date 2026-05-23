using Cassandra;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;
using System.Text.Json;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Repositorio de Ventas. Los items se serializan a JSON en la columna items_json
    /// porque Cassandra no soporta listas de objetos sin UDT.
    /// </summary>
    public class VentaRepository : IVentaRepository
    {
        private readonly ISession _session;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public VentaRepository(CassandraDbContext context)
        {
            _session = context.Session;
        }

        public async Task<Venta> AddAsync(Venta entity)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            var itemsJson = JsonSerializer.Serialize(entity.Items, JsonOpts);

            var stmt = new SimpleStatement(@"
                INSERT INTO ventas
                    (id, numero_ticket, fecha, cajero_id, cajero_nombre,
                     items_json, total, metodo_pago, estado,
                     created_at, updated_at, is_deleted, created_by)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                entity.Id, entity.NumeroTicket, entity.Fecha,
                entity.Cajero.UsuarioId, entity.Cajero.Nombre,
                itemsJson, entity.Total, entity.MetodoPago, entity.Estado,
                entity.CreatedAt, entity.UpdatedAt, entity.IsDeleted, entity.CreatedBy);

            await _session.ExecuteAsync(stmt);
            return entity;
        }

        public async Task<Venta?> GetByIdAsync(Guid id)
        {
            var stmt = new SimpleStatement("SELECT * FROM ventas WHERE id = ?", id);
            var rs = await _session.ExecuteAsync(stmt);
            var row = rs.FirstOrDefault();
            if (row == null) return null;

            var v = MapRow(row);
            return v.IsDeleted ? null : v;
        }

        public async Task<PaginatedResult<Venta>> SearchAsync(
            DateTime? fechaDesde, DateTime? fechaHasta,
            Guid? cajeroId, string? estado,
            int page = 1, int pageSize = 10)
        {
            string cql;
            var args = new List<object>();

            if (cajeroId.HasValue)
            {
                cql = "SELECT * FROM ventas WHERE cajero_id = ?";
                args.Add(cajeroId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(estado))
            {
                cql = "SELECT * FROM ventas WHERE estado = ?";
                args.Add(estado);
            }
            else
            {
                cql = "SELECT * FROM ventas ALLOW FILTERING";
            }

            var stmt = args.Count == 0
                ? new SimpleStatement(cql)
                : new SimpleStatement(cql, args.ToArray());
            var rs = await _session.ExecuteAsync(stmt);
            var all = rs.Select(MapRow).Where(v => !v.IsDeleted).ToList();

            if (fechaDesde.HasValue)
                all = all.Where(v => v.Fecha >= fechaDesde.Value).ToList();

            if (fechaHasta.HasValue)
                all = all.Where(v => v.Fecha <= fechaHasta.Value).ToList();

            if (!string.IsNullOrWhiteSpace(estado) && cajeroId.HasValue)
                all = all.Where(v => v.Estado == estado).ToList();

            var totalCount = all.Count;
            var items = all
                .OrderByDescending(v => v.Fecha)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<Venta>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<Venta> UpdateAsync(Guid id, Venta entity)
        {
            entity.Id = id;
            entity.UpdatedAt = DateTime.UtcNow;

            var existing = await GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Venta '{id}' no encontrada.");

            entity.CreatedAt = existing.CreatedAt;
            entity.CreatedBy = existing.CreatedBy;

            var itemsJson = JsonSerializer.Serialize(entity.Items, JsonOpts);

            var stmt = new SimpleStatement(@"
                UPDATE ventas SET
                    numero_ticket = ?, fecha = ?, cajero_id = ?, cajero_nombre = ?,
                    items_json = ?, total = ?, metodo_pago = ?, estado = ?,
                    updated_at = ?, is_deleted = ?
                WHERE id = ?",
                entity.NumeroTicket, entity.Fecha,
                entity.Cajero.UsuarioId, entity.Cajero.Nombre,
                itemsJson, entity.Total, entity.MetodoPago, entity.Estado,
                entity.UpdatedAt, entity.IsDeleted,
                id);

            await _session.ExecuteAsync(stmt);
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var existing = await GetByIdAsync(id);
            if (existing == null) return false;

            var stmt = new SimpleStatement(
                "UPDATE ventas SET is_deleted = true, updated_at = ? WHERE id = ?",
                DateTime.UtcNow, id);

            await _session.ExecuteAsync(stmt);
            return true;
        }

        private static Venta MapRow(Row row)
        {
            var itemsJson = row.GetValue<string>("items_json");
            var items = string.IsNullOrWhiteSpace(itemsJson)
                ? new List<VentaItem>()
                : JsonSerializer.Deserialize<List<VentaItem>>(itemsJson, JsonOpts) ?? new();

            return new Venta
            {
                Id = row.GetValue<Guid>("id"),
                NumeroTicket = row.GetValue<string>("numero_ticket") ?? string.Empty,
                Fecha = row.GetValue<DateTimeOffset>("fecha").UtcDateTime,
                Cajero = new CajeroRef
                {
                    UsuarioId = row.GetValue<Guid>("cajero_id"),
                    Nombre = row.GetValue<string>("cajero_nombre") ?? string.Empty
                },
                Items = items,
                Total = row.GetValue<decimal>("total"),
                MetodoPago = row.GetValue<string>("metodo_pago") ?? string.Empty,
                Estado = row.GetValue<string>("estado") ?? "completada",
                CreatedAt = row.GetValue<DateTimeOffset>("created_at").UtcDateTime,
                UpdatedAt = row.GetValue<DateTimeOffset>("updated_at").UtcDateTime,
                IsDeleted = row.GetValue<bool>("is_deleted"),
                CreatedBy = row.GetValue<string>("created_by") ?? string.Empty
            };
        }
    }
}
