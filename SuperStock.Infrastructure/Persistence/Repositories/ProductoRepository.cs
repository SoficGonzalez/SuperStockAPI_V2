using Cassandra;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    public class ProductoRepository : IProductoRepository
    {
        private readonly ISession _session;

        public ProductoRepository(CassandraDbContext context)
        {
            _session = context.Session;
        }

        public async Task<Producto> AddAsync(Producto entity)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            // Validar unicidad de codigo de barras (Cassandra no soporta UNIQUE)
            if (!string.IsNullOrWhiteSpace(entity.CodigoBarras))
            {
                var existing = await GetByCodigoBarrasAsync(entity.CodigoBarras);
                if (existing != null)
                    throw new InvalidOperationException(
                        $"Ya existe un producto con el codigo de barras '{entity.CodigoBarras}'.");
            }

            var stmt = new SimpleStatement(@"
                INSERT INTO productos
                    (id, codigo_barras, nombre, marca, categoria, subcategoria,
                     precio_venta, precio_costo, stock_actual, stock_minimo, unidad_medida,
                     activo, proveedor_id, proveedor_nombre, proveedor_telefono, detalles,
                     created_at, updated_at, is_deleted, created_by)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                entity.Id, entity.CodigoBarras, entity.Nombre, entity.Marca,
                entity.Categoria, entity.Subcategoria, entity.PrecioVenta, entity.PrecioCosto,
                entity.StockActual, entity.StockMinimo, entity.UnidadMedida, entity.Activo,
                entity.Proveedor?.ProveedorId, entity.Proveedor?.Nombre ?? string.Empty,
                entity.Proveedor?.Telefono ?? string.Empty,
                entity.Detalles ?? new Dictionary<string, string>(),
                entity.CreatedAt, entity.UpdatedAt, entity.IsDeleted, entity.CreatedBy);

            await _session.ExecuteAsync(stmt);
            return entity;
        }

        public async Task<Producto?> GetByIdAsync(Guid id)
        {
            var stmt = new SimpleStatement("SELECT * FROM productos WHERE id = ?", id);
            var rs = await _session.ExecuteAsync(stmt);
            var row = rs.FirstOrDefault();
            if (row == null) return null;

            var p = MapRow(row);
            return p.IsDeleted ? null : p;
        }

        public async Task<Producto?> GetByCodigoBarrasAsync(string codigoBarras)
        {
            var stmt = new SimpleStatement(
                "SELECT * FROM productos WHERE codigo_barras = ?", codigoBarras);
            var rs = await _session.ExecuteAsync(stmt);
            return rs.Select(MapRow).FirstOrDefault(p => !p.IsDeleted);
        }

        public async Task<PaginatedResult<Producto>> SearchAsync(
            string? categoria, string? nombre, bool? stockBajo, bool? activo,
            int page = 1, int pageSize = 10)
        {
            // En Cassandra no hay LIMIT + OFFSET nativo confiable.
            // Estrategia: trae candidatos por indice y filtra en memoria.
            // Para datasets grandes habria que rediseniar con sharding por categoria.
            string cql;
            var args = new List<object>();

            if (!string.IsNullOrWhiteSpace(categoria))
            {
                cql = "SELECT * FROM productos WHERE categoria = ?";
                args.Add(categoria);
            }
            else if (activo.HasValue)
            {
                cql = "SELECT * FROM productos WHERE activo = ?";
                args.Add(activo.Value);
            }
            else
            {
                // ALLOW FILTERING habilita filtros sin indice (costoso pero util en dev).
                cql = "SELECT * FROM productos ALLOW FILTERING";
            }

            var stmt = args.Count == 0
                ? new SimpleStatement(cql)
                : new SimpleStatement(cql, args.ToArray());
            var rs = await _session.ExecuteAsync(stmt);
            var all = rs.Select(MapRow).Where(p => !p.IsDeleted).ToList();

            // Filtros adicionales en memoria
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                all = all.Where(p =>
                    p.Nombre.Contains(nombre, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (stockBajo == true)
                all = all.Where(p => p.StockActual <= p.StockMinimo).ToList();

            if (activo.HasValue && !string.IsNullOrWhiteSpace(categoria))
                all = all.Where(p => p.Activo == activo.Value).ToList();

            var totalCount = all.Count;
            var items = all
                .OrderBy(p => p.Nombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PaginatedResult<Producto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<Producto> UpdateAsync(Guid id, Producto entity)
        {
            entity.Id = id;
            entity.UpdatedAt = DateTime.UtcNow;

            var existing = await GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Producto '{id}' no encontrado.");

            entity.CreatedAt = existing.CreatedAt;
            entity.CreatedBy = existing.CreatedBy;

            var stmt = new SimpleStatement(@"
                UPDATE productos SET
                    codigo_barras = ?, nombre = ?, marca = ?, categoria = ?,
                    subcategoria = ?, precio_venta = ?, precio_costo = ?,
                    stock_actual = ?, stock_minimo = ?, unidad_medida = ?, activo = ?,
                    proveedor_id = ?, proveedor_nombre = ?, proveedor_telefono = ?,
                    detalles = ?, updated_at = ?, is_deleted = ?
                WHERE id = ?",
                entity.CodigoBarras, entity.Nombre, entity.Marca, entity.Categoria,
                entity.Subcategoria, entity.PrecioVenta, entity.PrecioCosto,
                entity.StockActual, entity.StockMinimo, entity.UnidadMedida, entity.Activo,
                entity.Proveedor?.ProveedorId, entity.Proveedor?.Nombre ?? string.Empty,
                entity.Proveedor?.Telefono ?? string.Empty,
                entity.Detalles ?? new Dictionary<string, string>(),
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
                "UPDATE productos SET is_deleted = true, updated_at = ? WHERE id = ?",
                DateTime.UtcNow, id);

            await _session.ExecuteAsync(stmt);
            return true;
        }

        private static Producto MapRow(Row row)
        {
            var proveedorIdRaw = row.GetValue<Guid?>("proveedor_id");
            ProveedorRef? proveedor = proveedorIdRaw.HasValue && proveedorIdRaw.Value != Guid.Empty
                ? new ProveedorRef
                {
                    ProveedorId = proveedorIdRaw.Value,
                    Nombre = row.GetValue<string>("proveedor_nombre") ?? string.Empty,
                    Telefono = row.GetValue<string>("proveedor_telefono") ?? string.Empty
                }
                : null;

            var detalles = row.GetValue<IDictionary<string, string>>("detalles");

            return new Producto
            {
                Id = row.GetValue<Guid>("id"),
                CodigoBarras = row.GetValue<string>("codigo_barras") ?? string.Empty,
                Nombre = row.GetValue<string>("nombre") ?? string.Empty,
                Marca = row.GetValue<string>("marca") ?? string.Empty,
                Categoria = row.GetValue<string>("categoria") ?? string.Empty,
                Subcategoria = row.GetValue<string>("subcategoria") ?? string.Empty,
                PrecioVenta = row.GetValue<decimal>("precio_venta"),
                PrecioCosto = row.GetValue<decimal>("precio_costo"),
                StockActual = row.GetValue<int>("stock_actual"),
                StockMinimo = row.GetValue<int>("stock_minimo"),
                UnidadMedida = row.GetValue<string>("unidad_medida") ?? string.Empty,
                Activo = row.GetValue<bool>("activo"),
                Proveedor = proveedor,
                Detalles = detalles != null ? new Dictionary<string, string>(detalles) : null,
                CreatedAt = row.GetValue<DateTimeOffset>("created_at").UtcDateTime,
                UpdatedAt = row.GetValue<DateTimeOffset>("updated_at").UtcDateTime,
                IsDeleted = row.GetValue<bool>("is_deleted"),
                CreatedBy = row.GetValue<string>("created_by") ?? string.Empty
            };
        }
    }
}