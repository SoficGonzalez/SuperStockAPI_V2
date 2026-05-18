namespace SuperStock.API.DTOs
{
    // ── Auth DTOs ────────────────────────────────────────────

    public class RegisterDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Rol { get; set; } = "cajero";
        public string Sucursal { get; set; } = string.Empty;
    }

    public class LoginDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // ── Producto DTOs ────────────────────────────────────────

    public class ProductoDTO
    {
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Subcategoria { get; set; } = string.Empty;
        public decimal PrecioVenta { get; set; }
        public decimal PrecioCosto { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Referencia al proveedor (opcional al crear).
        /// </summary>
        public ProveedorRefDTO? Proveedor { get; set; }

        /// <summary>
        /// Atributos especificos de la categoria.
        /// Se envia como JSON libre. Ejemplos:
        ///   Perecederos: { "fecha_vencimiento": "2026-03-15", "temperatura": "0-4 C" }
        ///   Limpieza:    { "concentracion": "5%", "uso_recomendado": "pisos" }
        /// </summary>
        public Dictionary<string, object>? Detalles { get; set; }
    }

    public class ProveedorRefDTO
    {
        public string ProveedorId { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
    }

    // ── Venta DTOs ───────────────────────────────────────────

    public class VentaDTO
    {
        public List<VentaItemDTO> Items { get; set; } = new();
        public string MetodoPago { get; set; } = "efectivo";
    }

    public class VentaItemDTO
    {
        public string ProductoId { get; set; } = string.Empty;
        public int Cantidad { get; set; }
    }

    // ── Proveedor DTOs ───────────────────────────────────────

    public class ProveedorDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string Contacto { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public List<string> CategoriaSuministro { get; set; } = new();
        public string CondicionesPago { get; set; } = "contado";
        public bool Activo { get; set; } = true;
    }
}
