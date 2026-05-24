using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces.Repositories
{
    public interface IProveedorRepository : IBaseRepository<Proveedor>
    {
        Task<PaginatedResult<Proveedor>> SearchAsync(
            string? nombre,
            string? categoria,
            bool? activo,
            int page = 1,
            int pageSize = 10);
    }
}
