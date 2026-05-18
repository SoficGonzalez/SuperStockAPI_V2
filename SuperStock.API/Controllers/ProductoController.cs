using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperStock.API.DTOs;
using SuperStock.Application.Services;
using SuperStock.Domain.Entities;
using SuperStock.API.Helpers;

namespace SuperStock.API.Controllers
{
    /// <summary>
    /// Reemplaza TicketController del proyecto Tickets.
    /// Misma estructura: [Authorize], inyeccion del servicio, y UserId desde JWT claims.
    /// Se agregan: CRUD completo, paginacion, y busqueda con multiples filtros.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductoController : ControllerBase
    {
        private readonly ProductoService _productoService;

        public ProductoController(ProductoService productoService)
        {
            _productoService = productoService;
        }

        /// <summary>
        /// GET /api/producto?categoria=perecederos&amp;nombre=pollo&amp;stockBajo=true&amp;activo=true&amp;page=1&amp;pageSize=10
        /// Busqueda con filtros multiples y paginacion.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] string? categoria,
            [FromQuery] string? nombre,
            [FromQuery] bool? stockBajo,
            [FromQuery] bool? activo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _productoService.Search(categoria, nombre, stockBajo, activo, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// GET /api/producto/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var producto = await _productoService.GetById(id);
            if (producto == null)
                return NotFound(new { Message = $"Producto '{id}' no encontrado." });

            return Ok(producto);
        }

        /// <summary>
        /// GET /api/producto/barcode/{codigoBarras}
        /// Endpoint especifico para escaneo en caja (POS).
        /// </summary>
        [HttpGet("barcode/{codigoBarras}")]
        public async Task<IActionResult> GetByBarcode(string codigoBarras)
        {
            var producto = await _productoService.GetByCodigoBarras(codigoBarras);
            if (producto == null)
                return NotFound(new { Message = $"Producto con codigo '{codigoBarras}' no encontrado." });

            return Ok(producto);
        }

        /// <summary>
        /// POST /api/producto
        /// Crear un nuevo producto.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin,bodeguero")]
        public async Task<IActionResult> Create([FromBody] ProductoDTO dto)
        {
            if (dto == null)
                return BadRequest(new { Message = "El cuerpo de la solicitud es requerido." });

            // UserId desde JWT claim (mismo patron que TicketController)
            var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { Message = "Usuario no autenticado." });

            try
            {
                var producto = new Producto
                {
                    CodigoBarras = dto.CodigoBarras,
                    Nombre = dto.Nombre,
                    Marca = dto.Marca,
                    Categoria = dto.Categoria,
                    Subcategoria = dto.Subcategoria,
                    PrecioVenta = dto.PrecioVenta,
                    PrecioCosto = dto.PrecioCosto,
                    StockActual = dto.StockActual,
                    StockMinimo = dto.StockMinimo,
                    UnidadMedida = dto.UnidadMedida,
                    Activo = dto.Activo,
                    Detalles = JsonValueNormalizer.NormalizeDictionary(dto.Detalles),
                    CreatedBy = userId,
                    Proveedor = dto.Proveedor != null ? new ProveedorRef
                    {
                        ProveedorId = dto.Proveedor.ProveedorId,
                        Nombre = dto.Proveedor.Nombre,
                        Telefono = dto.Proveedor.Telefono
                    } : null
                };

                var result = await _productoService.Add(producto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/producto/{id}
        /// Actualizar un producto existente.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "admin,bodeguero")]
        public async Task<IActionResult> Update(string id, [FromBody] ProductoDTO dto)
        {
            if (dto == null)
                return BadRequest(new { Message = "El cuerpo de la solicitud es requerido." });

            try
            {
                var producto = new Producto
                {
                    CodigoBarras = dto.CodigoBarras,
                    Nombre = dto.Nombre,
                    Marca = dto.Marca,
                    Categoria = dto.Categoria,
                    Subcategoria = dto.Subcategoria,
                    PrecioVenta = dto.PrecioVenta,
                    PrecioCosto = dto.PrecioCosto,
                    StockActual = dto.StockActual,
                    StockMinimo = dto.StockMinimo,
                    UnidadMedida = dto.UnidadMedida,
                    Activo = dto.Activo,
                    Detalles = JsonValueNormalizer.NormalizeDictionary(dto.Detalles),
                    Proveedor = dto.Proveedor != null ? new ProveedorRef
                    {
                        ProveedorId = dto.Proveedor.ProveedorId,
                        Nombre = dto.Proveedor.Nombre,
                        Telefono = dto.Proveedor.Telefono
                    } : null
                };

                var result = await _productoService.Update(id, producto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { ex.Message });
            }
        }

        /// <summary>
        /// DELETE /api/producto/{id}
        /// Soft delete (marca IsDeleted = true).
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _productoService.Delete(id);
                return Ok(new { Message = $"Producto '{id}' eliminado." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { ex.Message });
            }
        }
    }
}
