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
            var existente = await _productoRepository.GetByCodigoBarrasAsync(producto.CodigoBarras);
            if (existente != null)
                throw new InvalidOperationException(
                    $"Ya existe un producto con el codigo de barras '{producto.CodigoBarras}'.");

            return await _productoRepository.AddAsync(producto);
        }

        public async Task<Producto?> GetById(string id)
        {
            return await _productoRepository.GetByMongoIdAsync(id);
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

        public async Task<Producto> Update(string id, Producto producto)
        {
            var existente = await _productoRepository.GetByMongoIdAsync(id)
                ?? throw new KeyNotFoundException($"Producto con id '{id}' no encontrado.");

            producto.CreatedAt = existente.CreatedAt;
            producto.CreatedBy = existente.CreatedBy;

            return await _productoRepository.UpdateAsync(id, producto);
        }

        public async Task<bool> Delete(string id)
        {
            var existente = await _productoRepository.GetByMongoIdAsync(id)
                ?? throw new KeyNotFoundException($"Producto con id '{id}' no encontrado.");

            return await _productoRepository.DeleteAsync(id);
        }
    }
}