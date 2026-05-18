using SuperStock.Domain.Entities;
using SuperStock.Domain.Interfaces;
using SuperStock.Domain.Interfaces.Repositories;

namespace SuperStock.Application.Services
{
    /// <summary>
    /// Reemplaza el AuthService del proyecto Tickets.
    ///
    /// En Tickets, el flujo era:
    ///   1. UserRepository.CreateUser → UserManager.CreateAsync (Identity hasheaba el password)
    ///   2. Login → UserManager.CheckPasswordAsync (Identity verificaba el hash)
    ///
    /// Aqui el flujo es:
    ///   1. Register → BCrypt.HashPassword → UsuarioRepository.AddAsync (nosotros hasheamos)
    ///   2. Login → UsuarioRepository.GetByEmail → BCrypt.Verify (nosotros verificamos)
    ///
    /// Misma logica de negocio, distinta infraestructura.
    /// </summary>
    public class AuthService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IJWTService _jwtService;

        public AuthService(IUsuarioRepository usuarioRepository, IJWTService jwtService)
        {
            _usuarioRepository = usuarioRepository;
            _jwtService = jwtService;
        }

        public async Task<Usuario> RegisterUser(Usuario usuario, string password)
        {
            if (string.IsNullOrWhiteSpace(usuario.Email))
                throw new ArgumentException("El email es requerido.", nameof(usuario));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("La contrasena es requerida.");

            if (await _usuarioRepository.ExistsByEmailAsync(usuario.Email))
                throw new InvalidOperationException($"Ya existe un usuario con el email '{usuario.Email}'.");

            // BCrypt genera el salt internamente y lo almacena en el hash
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

            return await _usuarioRepository.AddAsync(usuario);
        }

        public async Task<string> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("El email es requerido.", nameof(email));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("La contrasena es requerida.", nameof(password));

            var user = await _usuarioRepository.GetByEmailAsync(email);

            if (user == null)
                throw new UnauthorizedAccessException("Usuario o contrasena invalidos.");

            if (!user.Activo)
                throw new UnauthorizedAccessException("Usuario desactivado. Contacte al administrador.");

            // BCrypt.Verify compara el password plano contra el hash almacenado
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Usuario o contrasena invalidos.");

            // Actualizar ultimo acceso
            user.UltimoAcceso = DateTime.UtcNow;
            await _usuarioRepository.UpdateAsync(user.Id, user);

            return _jwtService.GenerateToken(user);
        }
    }
}
