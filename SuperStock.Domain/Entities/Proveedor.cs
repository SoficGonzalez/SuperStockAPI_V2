namespace SuperStock.Domain.Entities
{
    public class Proveedor : BaseEntity
    {
        public string Nombre { get; set; } = string.Empty;

        public string Contacto { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Direccion { get; set; } = string.Empty;

        /// <summary>
        /// Categorias que suministra este proveedor. Ej: ["perecederos", "carnes_aves"]
        /// </summary>
        public List<string> CategoriaSuministro { get; set; } = new();

        public string CondicionesPago { get; set; } = string.Empty; // "contado" | "credito_30_dias"

        public bool Activo { get; set; } = true;
    }
}
