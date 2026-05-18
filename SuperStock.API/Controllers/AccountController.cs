using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperStock.API.DTOs;
using SuperStock.Application.Services;
using SuperStock.Domain.Entities;

namespace SuperStock.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly AuthService _authService;

        public AccountController(AuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registrar un nuevo usuario.
        /// Solo administradores pueden crear usuarios (en produccion).
        /// </summary>
        [HttpPost("register")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            if (dto == null)
                return BadRequest(new { Message = "El cuerpo de la solicitud es requerido." });

            try
            {
                var usuario = new Usuario
                {
                    Nombre = dto.Nombre,
                    Apellido = dto.Apellido,
                    Email = dto.Email,
                    Telefono = dto.Telefono,
                    Rol = dto.Rol,
                    Sucursal = dto.Sucursal
                };

                var result = await _authService.RegisterUser(usuario, dto.Password);

                return Ok(new
                {
                    Message = "Registro exitoso",
                    User = new
                    {
                        result.Id,
                        result.Email,
                        result.Nombre,
                        result.Apellido,
                        result.Rol,
                        result.Sucursal
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        /// <summary>
        /// Login. Retorna JWT token.
        /// Endpoint publico (sin [Authorize]).
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            if (dto == null)
                return BadRequest(new { Message = "El cuerpo de la solicitud es requerido." });

            try
            {
                var token = await _authService.Login(dto.Email, dto.Password);
                return Ok(new { Token = token });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { ex.Message });
            }
        }
    }
}
