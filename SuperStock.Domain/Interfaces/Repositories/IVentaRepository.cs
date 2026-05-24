using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces.Repositories
{
    public interface IVentaRepository : IBaseRepository<Venta>
    {
        Task<PaginatedResult<Venta>> SearchAsync(
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            Guid? cajeroId,
            string? estado,
            int page = 1,
            int pageSize = 10);
    }
}
