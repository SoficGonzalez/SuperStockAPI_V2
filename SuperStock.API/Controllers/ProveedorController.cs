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
    public class ProveedorController : ControllerBase
    {
        private readonly ProveedorService _proveedorService;

        public ProveedorController(ProveedorService proveedorService)
        {
            _proveedorService = proveedorService;
        }

        /// <summary>
        /// GET /api/proveedor?nombre=rico&amp;categoria=perecederos&amp;activo=true&amp;page=1&amp;pageSize=10
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] string? nombre,
            [FromQuery] string? categoria,
            [FromQuery] bool? activo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _proveedorService.Search(nombre, categoria, activo, page, pageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var proveedor = await _proveedorService.GetById(id);
            if (proveedor == null)
                return NotFound(new { Message = $"Proveedor '{id}' no encontrado." });
            return Ok(proveedor);
        }

        [HttpPost]
        [Authorize(Roles = "admin,gerente")]
        public async Task<IActionResult> Create([FromBody] ProveedorDTO dto)
        {
            if (dto == null)
                return BadRequest(new { Message = "El cuerpo de la solicitud es requerido." });

            var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var proveedor = new Proveedor
            {
                Nombre = dto.Nombre,
                Contacto = dto.Contacto,
                Telefono = dto.Telefono,
                Email = dto.Email,
                Direccion = dto.Direccion,
                CategoriaSuministro = dto.CategoriaSuministro,
                CondicionesPago = dto.CondicionesPago,
                Activo = dto.Activo,
                CreatedBy = userId ?? string.Empty
            };

            var result = await _proveedorService.Add(proveedor);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "admin,gerente")]
        public async Task<IActionResult> Update(string id, [FromBody] ProveedorDTO dto)
        {
            if (dto == null)
                return BadRequest(new { Message = "El cuerpo de la solicitud es requerido." });

            try
            {
                var proveedor = new Proveedor
                {
                    Nombre = dto.Nombre,
                    Contacto = dto.Contacto,
                    Telefono = dto.Telefono,
                    Email = dto.Email,
                    Direccion = dto.Direccion,
                    CategoriaSuministro = dto.CategoriaSuministro,
                    CondicionesPago = dto.CondicionesPago,
                    Activo = dto.Activo
                };

                var result = await _proveedorService.Update(id, proveedor);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _proveedorService.Delete(id);
                return Ok(new { Message = $"Proveedor '{id}' eliminado." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { ex.Message });
            }
        }
    }
}
