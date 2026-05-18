using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Reemplaza IUserRepository + IRoleRepository del proyecto Tickets.
    /// Ya no dependemos de ASP.NET Identity; todo vive en MongoDB.
    /// </summary>
    public interface IUsuarioRepository : IBaseRepository<Usuario>
    {
        Task<Usuario?> GetByEmailAsync(string email);
        Task<bool> ExistsByEmailAsync(string email);
    }
}
