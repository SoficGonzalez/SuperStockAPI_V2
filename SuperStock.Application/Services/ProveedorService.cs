using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Application.Services
{
    public class ProveedorService
    {
        private readonly IProveedorRepository _proveedorRepository;

        public ProveedorService(IProveedorRepository proveedorRepository)
        {
            _proveedorRepository = proveedorRepository;
        }

        public async Task<Proveedor> Add(Proveedor proveedor)
        {
            return await _proveedorRepository.AddAsync(proveedor);
        }

        public async Task<Proveedor?> GetById(string id)
        {
            return await _proveedorRepository.GetByIdAsync(id);
        }

        public async Task<PaginatedResult<Proveedor>> Search(
            string? nombre, string? categoria, bool? activo,
            int page = 1, int pageSize = 10)
        {
            return await _proveedorRepository.SearchAsync(nombre, categoria, activo, page, pageSize);
        }

        public async Task<Proveedor> Update(string id, Proveedor proveedor)
        {
            var existente = await _proveedorRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Proveedor con id '{id}' no encontrado.");

            proveedor.CreatedAt = existente.CreatedAt;
            proveedor.CreatedBy = existente.CreatedBy;

            return await _proveedorRepository.UpdateAsync(id, proveedor);
        }

        public async Task<bool> Delete(string id)
        {
            _ = await _proveedorRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Proveedor con id '{id}' no encontrado.");

            return await _proveedorRepository.DeleteAsync(id);
        }
    }
}
