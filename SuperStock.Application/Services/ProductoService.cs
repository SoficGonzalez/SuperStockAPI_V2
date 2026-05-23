using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Application.Services
{
    public class ProductoService
    {
        private readonly IProductoRepository _productoRepository;

        public ProductoService(IProductoRepository productoRepository)
        {
            _productoRepository = productoRepository;
        }

        public async Task<Producto> Add(Producto producto)
        {
            // La validacion de unicidad esta en el repo (Cassandra no tiene UNIQUE).
            return await _productoRepository.AddAsync(producto);
        }

        public async Task<Producto?> GetById(Guid id)
        {
            return await _productoRepository.GetByIdAsync(id);
        }

        public async Task<Producto?> GetByCodigoBarras(string codigoBarras)
        {
            return await _productoRepository.GetByCodigoBarrasAsync(codigoBarras);
        }

        public async Task<PaginatedResult<Producto>> Search(
            string? categoria, string? nombre, bool? stockBajo, bool? activo,
            int page = 1, int pageSize = 10)
        {
            return await _productoRepository.SearchAsync(
                categoria, nombre, stockBajo, activo, page, pageSize);
        }

        public async Task<Producto> Update(Guid id, Producto producto)
        {
            var existente = await _productoRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Producto con id '{id}' no encontrado.");

            producto.CreatedAt = existente.CreatedAt;
            producto.CreatedBy = existente.CreatedBy;

            return await _productoRepository.UpdateAsync(id, producto);
        }

        public async Task<bool> Delete(Guid id)
        {
            var existente = await _productoRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Producto con id '{id}' no encontrado.");

            return await _productoRepository.DeleteAsync(id);
        }
    }
}