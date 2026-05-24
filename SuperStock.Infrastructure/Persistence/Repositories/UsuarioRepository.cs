using Cassandra;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Repositorio de Usuario sobre Cassandra.
    /// Cada metodo construye CQL parametrizado (prevencion de inyeccion).
    /// </summary>
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly ISession _session;

        public UsuarioRepository(CassandraDbContext context)
        {
            _session = context.Session;
        }

        public async Task<Usuario> AddAsync(Usuario entity)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            var stmt = new SimpleStatement(@"
                INSERT INTO usuarios
                    (id, nombre, apellido, email, telefono, password_hash, rol, sucursal,
                     activo, ultimo_acceso, created_at, updated_at, is_deleted, created_by)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                entity.Id, entity.Nombre, entity.Apellido, entity.Email, entity.Telefono,
                entity.PasswordHash, entity.Rol, entity.Sucursal, entity.Activo,
                entity.UltimoAcceso, entity.CreatedAt, entity.UpdatedAt,
                entity.IsDeleted, entity.CreatedBy);

            await _session.ExecuteAsync(stmt);
            return entity;
        }

        public async Task<Usuario?> GetByIdAsync(Guid id)
        {
            var stmt = new SimpleStatement("SELECT * FROM usuarios WHERE id = ?", id);
            var rs = await _session.ExecuteAsync(stmt);
            var row = rs.FirstOrDefault();
            if (row == null) return null;

            var u = MapRow(row);
            return u.IsDeleted ? null : u;
        }

        public async Task<Usuario?> GetByEmailAsync(string email)
        {
            var stmt = new SimpleStatement("SELECT * FROM usuarios WHERE email = ?", email);
            var rs = await _session.ExecuteAsync(stmt);
            return rs.Select(MapRow).FirstOrDefault(u => !u.IsDeleted);
        }

        public async Task<bool> ExistsByEmailAsync(string email)
        {
            var existing = await GetByEmailAsync(email);
            return existing != null;
        }

        public async Task<Usuario> UpdateAsync(Guid id, Usuario entity)
        {
            entity.Id = id;
            entity.UpdatedAt = DateTime.UtcNow;

            // Verificar existencia
            var existing = await GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Usuario '{id}' no encontrado.");

            // Conservar CreatedAt/CreatedBy del original (no se sobreescriben en update).
            entity.CreatedAt = existing.CreatedAt;
            entity.CreatedBy = existing.CreatedBy;

            var stmt = new SimpleStatement(@"
                UPDATE usuarios SET
                    nombre = ?, apellido = ?, email = ?, telefono = ?,
                    password_hash = ?, rol = ?, sucursal = ?, activo = ?,
                    ultimo_acceso = ?, updated_at = ?, is_deleted = ?
                WHERE id = ?",
                entity.Nombre, entity.Apellido, entity.Email, entity.Telefono,
                entity.PasswordHash, entity.Rol, entity.Sucursal, entity.Activo,
                entity.UltimoAcceso, entity.UpdatedAt, entity.IsDeleted,
                id);

            await _session.ExecuteAsync(stmt);
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var existing = await GetByIdAsync(id);
            if (existing == null) return false;

            // Soft delete
            var stmt = new SimpleStatement(
                "UPDATE usuarios SET is_deleted = true, updated_at = ? WHERE id = ?",
                DateTime.UtcNow, id);

            await _session.ExecuteAsync(stmt);
            return true;
        }

        private static Usuario MapRow(Row row) => new()
        {
            Id = row.GetValue<Guid>("id"),
            Nombre = row.GetValue<string>("nombre") ?? string.Empty,
            Apellido = row.GetValue<string>("apellido") ?? string.Empty,
            Email = row.GetValue<string>("email") ?? string.Empty,
            Telefono = row.GetValue<string>("telefono") ?? string.Empty,
            PasswordHash = row.GetValue<string>("password_hash") ?? string.Empty,
            Rol = row.GetValue<string>("rol") ?? "cajero",
            Sucursal = row.GetValue<string>("sucursal") ?? string.Empty,
            Activo = row.GetValue<bool>("activo"),
            UltimoAcceso = row.GetValue<DateTimeOffset?>("ultimo_acceso")?.UtcDateTime,
            CreatedAt = row.GetValue<DateTimeOffset>("created_at").UtcDateTime,
            UpdatedAt = row.GetValue<DateTimeOffset>("updated_at").UtcDateTime,
            IsDeleted = row.GetValue<bool>("is_deleted"),
            CreatedBy = row.GetValue<string>("created_by") ?? string.Empty
        };
    }
}
