using MongoDB.Driver;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Reemplaza UserRepository + RoleRepository del proyecto Tickets.
    /// En Tickets, estos dependian de ASP.NET Identity (UserManager, RoleManager)
    /// que a su vez necesitaban EF Core. Aqui todo es MongoDB directo.
    /// </summary>
    public class UsuarioRepository : BaseRepository<Usuario>, IUsuarioRepository
    {
        public UsuarioRepository(MongoDbContext context) : base(context.Usuarios) { }

        public async Task<Usuario?> GetByEmailAsync(string email)
        {
            var filter = Builders<Usuario>.Filter.Eq(u => u.Email, email)
                       & Builders<Usuario>.Filter.Eq(u => u.IsDeleted, false);

            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<bool> ExistsByEmailAsync(string email)
        {
            var filter = Builders<Usuario>.Filter.Eq(u => u.Email, email)
                       & Builders<Usuario>.Filter.Eq(u => u.IsDeleted, false);

            return await _collection.CountDocumentsAsync(filter) > 0;
        }
    }
}
