using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces;

namespace SuperStock.Infrastructure.Security
{
    /// <summary>
    /// Mismo patron que JWTService del proyecto Tickets.
    /// Diferencia: en Tickets, los roles venian de ASP.NET Identity como IList&lt;string&gt;.
    /// Aqui el rol es un campo directo del Usuario en MongoDB (un solo rol por usuario).
    /// </summary>
    public class JWTService : IJWTService
    {
        private readonly IConfiguration _configuration;

        public JWTService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(Usuario usuario)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id),
                new(ClaimTypes.Email, usuario.Email),
                new(ClaimTypes.GivenName, usuario.NombreCompleto),
                new(ClaimTypes.Role, usuario.Rol)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["JWTKey"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWTIssuer"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(int.Parse(_configuration["JWTLifeTime"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
