using SuperStock.Domain.Entities;

namespace SuperStock.Domain.Interfaces.Repositories
{
    public interface IVentaRepository : IBaseRepository<Venta>
    {
        /// <summary>
        /// Busqueda con filtros: rango de fechas, cajero, estado.
        /// </summary>
        Task<PaginatedResult<Venta>> SearchAsync(
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            string? cajeroId,
            string? estado,
            int page = 1,
            int pageSize = 10);
    }
}
