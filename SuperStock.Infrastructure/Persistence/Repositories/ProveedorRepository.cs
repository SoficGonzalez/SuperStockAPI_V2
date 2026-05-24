using Cassandra;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    public class ProveedorRepository : IProveedorRepository
    {
        private readonly ISession _session;

        public ProveedorRepository(CassandraDbContext context)
        {
            _session = context.Session;
        }

        public async Task<Proveedor> AddAsync(Proveedor entity)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            var stmt = new SimpleStatement(@"
                INSERT INTO proveedores
                    (id, nombre, contacto, telefono, email, direccion,
                     categoria_suministro, condiciones_pago, activo,
                     created_at, updated_at, is_deleted, created_by)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                entity.Id, entity.Nombre, entity.Contacto, entity.Telefono,
                entity.Email, entity.Direccion, entity.CategoriaSuministro,
                entity.CondicionesPago, entity.Activo,
                entity.CreatedAt, entity.UpdatedAt, entity.IsDeleted, entity.CreatedBy);

            await _session.ExecuteAsync(stmt);
            return entity;
        }

        public async Task<Proveedor?> GetByIdAsync(Guid id)
        {
            var stmt = new SimpleStatement("SELECT * FROM proveedores WHERE id = ?", id);
            var rs = await _session.ExecuteAsync(stmt);
            var row = rs.FirstOrDefault();
            if (row == null) return null;

            var p = MapRow(row);
            return p.IsDeleted ? null : p;
        }

        public async Task<PaginatedResult<Proveedor>> SearchAsync(
            string? nombre, string? categoria, bool? activo,
            int page = 1, int pageSize = 10)
        {
            string cql;
            var args = new List<object>();

            if (activo.HasValue)
            {
                cql = "SELECT * FROM proveedores WHERE activo = ?";
                args.Add(activo.Value);
            }
            else
            {
                cql = "SELECT * FROM proveedores ALLOW FILTERING";
            }

            var stmt = args.Count == 0
                ? new SimpleStatement(cql)
                : new SimpleStatement(cql, args.ToArray());
            var rs = await _session.ExecuteAsync(stmt);
            var all = rs.Select(MapRow).Where(p => !p.IsDeleted).ToList();

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                all = all.Where(p =>
                    p.Nombre.Contains(nombre, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(categoria))
            {
                all = all.Where(p => p.CategoriaSuministro.Contains(categoria)).ToList();
            }

            var totalCount = all.Count;
            var items = all
                .OrderBy(p => p.Nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<Proveedor>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<Proveedor> UpdateAsync(Guid id, Proveedor entity)
        {
            entity.Id = id;
            entity.UpdatedAt = DateTime.UtcNow;

            var existing = await GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Proveedor '{id}' no encontrado.");

            entity.CreatedAt = existing.CreatedAt;
            entity.CreatedBy = existing.CreatedBy;

            var stmt = new SimpleStatement(@"
                UPDATE proveedores SET
                    nombre = ?, contacto = ?, telefono = ?, email = ?, direccion = ?,
                    categoria_suministro = ?, condiciones_pago = ?, activo = ?,
                    updated_at = ?, is_deleted = ?
                WHERE id = ?",
                entity.Nombre, entity.Contacto, entity.Telefono, entity.Email, entity.Direccion,
                entity.CategoriaSuministro, entity.CondicionesPago, entity.Activo,
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
                "UPDATE proveedores SET is_deleted = true, updated_at = ? WHERE id = ?",
                DateTime.UtcNow, id);

            await _session.ExecuteAsync(stmt);
            return true;
        }

        private static Proveedor MapRow(Row row)
        {
            var categorias = row.GetValue<IEnumerable<string>>("categoria_suministro");
            return new Proveedor
            {
                Id = row.GetValue<Guid>("id"),
                Nombre = row.GetValue<string>("nombre") ?? string.Empty,
                Contacto = row.GetValue<string>("contacto") ?? string.Empty,
                Telefono = row.GetValue<string>("telefono") ?? string.Empty,
                Email = row.GetValue<string>("email") ?? string.Empty,
                Direccion = row.GetValue<string>("direccion") ?? string.Empty,
                CategoriaSuministro = categorias?.ToList() ?? new List<string>(),
                CondicionesPago = row.GetValue<string>("condiciones_pago") ?? "contado",
                Activo = row.GetValue<bool>("activo"),
                CreatedAt = row.GetValue<DateTimeOffset>("created_at").UtcDateTime,
                UpdatedAt = row.GetValue<DateTimeOffset>("updated_at").UtcDateTime,
                IsDeleted = row.GetValue<bool>("is_deleted"),
                CreatedBy = row.GetValue<string>("created_by") ?? string.Empty
            };
        }
    }
}
