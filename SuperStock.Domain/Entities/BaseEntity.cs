using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Entidad base para todas las tablas de Cassandra.
    /// Usa Guid (UUID en CQL) como Id, sin atributos de mapeo:
    /// el mapeo a CQL se hace explicitamente en los repositorios.
    /// </summary>
    public class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public string CreatedBy { get; set; } = string.Empty;
    }
}