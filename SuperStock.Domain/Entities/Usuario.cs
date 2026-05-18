namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Usuario del sistema. Reemplaza ASP.NET Identity ya que MongoDB
    /// no usa EF Core. El hash de password se maneja con BCrypt.
    /// </summary>
    public class Usuario : BaseEntity
    {
        public string Nombre { get; set; } = string.Empty;

        public string Apellido { get; set; } = string.Empty;

        public string NombreCompleto => $"{Nombre} {Apellido}";

        public string Email { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        /// <summary>
        /// Hash BCrypt del password. Nunca se almacena en texto plano.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Rol del usuario en el sistema.
        /// Valores validos: "cajero" | "bodeguero" | "admin" | "gerente"
        /// </summary>
        public string Rol { get; set; } = "cajero";

        public string Sucursal { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;

        public DateTime? UltimoAcceso { get; set; }
    }
}
