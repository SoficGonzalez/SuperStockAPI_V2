using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces.Repositories
{
    public interface IProductoRepository : IBaseRepository<Producto>
    {
        Task<PaginatedResult<Producto>> SearchAsync(
            string? categoria,
            string? nombre,
            bool? stockBajo,
            bool? activo,
            int page = 1,
            int pageSize = 10);

        Task<Producto?> GetByCodigoBarrasAsync(string codigoBarras);
    }
}