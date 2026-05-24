using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Application.Services
{
    public class VentaService
    {
        private readonly IVentaRepository _ventaRepository;
        private readonly IProductoRepository _productoRepository;

        public VentaService(IVentaRepository ventaRepository, IProductoRepository productoRepository)
        {
            _ventaRepository = ventaRepository;
            _productoRepository = productoRepository;
        }

        /// <summary>
        /// Registra una venta y actualiza el stock de cada producto.
        /// Si algun producto no tiene stock suficiente, lanza excepcion
        /// antes de registrar la venta (validacion fail-fast).
        /// </summary>
        public async Task<Venta> Add(Venta venta)
        {
            foreach (var item in venta.Items)
            {
                var producto = await _productoRepository.GetByIdAsync(item.ProductoId)
                    ?? throw new KeyNotFoundException($"Producto '{item.ProductoId}' no encontrado.");

                if (producto.StockActual < item.Cantidad)
                {
                    throw new InvalidOperationException(
                        $"Stock insuficiente para '{producto.Nombre}'. " +
                        $"Disponible: {producto.StockActual}, Solicitado: {item.Cantidad}");
                }

                item.Nombre = producto.Nombre;
                item.PrecioUnitario = producto.PrecioVenta;
                item.Subtotal = item.PrecioUnitario * item.Cantidad;
            }

            venta.Total = venta.Items.Sum(i => i.Subtotal);
            venta.Fecha = DateTime.UtcNow;
            venta.NumeroTicket = $"VTA-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            var ventaCreada = await _ventaRepository.AddAsync(venta);

            foreach (var item in venta.Items)
            {
                var producto = await _productoRepository.GetByIdAsync(item.ProductoId)
                    ?? throw new KeyNotFoundException($"Producto '{item.ProductoId}' no encontrado al descontar stock.");

                producto.StockActual -= item.Cantidad;
                await _productoRepository.UpdateAsync(producto.Id, producto);
            }

            return ventaCreada;
        }

        public async Task<Venta?> GetById(Guid id)
        {
            return await _ventaRepository.GetByIdAsync(id);
        }

        public async Task<PaginatedResult<Venta>> Search(
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            Guid? cajeroId,
            string? estado,
            int page = 1,
            int pageSize = 10)
        {
            return await _ventaRepository.SearchAsync(
                fechaDesde, fechaHasta, cajeroId, estado, page, pageSize);
        }

        /// <summary>
        /// Anula una venta y restaura el stock de los productos.
        /// </summary>
        public async Task<Venta> Anular(Guid id)
        {
            var venta = await _ventaRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Venta '{id}' no encontrada.");

            if (venta.Estado == "anulada")
                throw new InvalidOperationException("La venta ya esta anulada.");

            venta.Estado = "anulada";
            await _ventaRepository.UpdateAsync(id, venta);

            foreach (var item in venta.Items)
            {
                var producto = await _productoRepository.GetByIdAsync(item.ProductoId);
                if (producto != null)
                {
                    producto.StockActual += item.Cantidad;
                    await _productoRepository.UpdateAsync(producto.Id, producto);
                }
            }

            return venta;
        }
    }
}