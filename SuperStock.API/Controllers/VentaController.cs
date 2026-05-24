using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperStock.API.DTOs;
using SuperStock.Application.Services;
using SuperStock.Domain.Entities;

namespace SuperStock.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VentaController : ControllerBase
    {
        private readonly VentaService _ventaService;

        public VentaController(VentaService ventaService)
        {
            _ventaService = ventaService;
        }

        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] DateTime? fechaDesde,
            [FromQuery] DateTime? fechaHasta,
            [FromQuery] Guid? cajeroId,
            [FromQuery] string? estado,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _ventaService.Search(fechaDesde, fechaHasta, cajeroId, estado, page, pageSize);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var venta = await _ventaService.GetById(id);
            if (venta == null)
                return NotFound(new { Message = $"Venta '{id}' no encontrada." });

            return Ok(venta);
        }

        [HttpPost]
        [Authorize(Roles = "admin,cajero")]
        public async Task<IActionResult> Create([FromBody] VentaDTO dto)
        {
            if (dto == null || dto.Items.Count == 0)
                return BadRequest(new { Message = "La venta debe tener al menos un item." });

            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { Message = "Usuario no autenticado." });

            try
            {
                var venta = new Venta
                {
                    Cajero = new CajeroRef
                    {
                        UsuarioId = userId,
                        Nombre = userName ?? "Desconocido"
                    },
                    Items = dto.Items.Select(i => new VentaItem
                    {
                        ProductoId = i.ProductoId,
                        Cantidad = i.Cantidad
                    }).ToList(),
                    MetodoPago = dto.MetodoPago,
                    CreatedBy = userIdStr
                };

                var result = await _ventaService.Add(venta);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPatch("{id:guid}/anular")]
        [Authorize(Roles = "admin,gerente")]
        public async Task<IActionResult> Anular(Guid id)
        {
            try
            {
                var result = await _ventaService.Anular(id);
                return Ok(new { Message = "Venta anulada exitosamente.", Venta = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ex.Message });
            }
        }
    }
}
