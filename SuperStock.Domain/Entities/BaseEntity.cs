using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Entidad base para todas las colecciones MongoDB.
    /// Usa string como Id pero mapeado al _id de MongoDB.
    /// </summary>
    public class BaseEntity
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public string CreatedBy { get; set; } = string.Empty;
    }
}