namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Documento de venta autocontenido.
    /// Los items se serializan como JSON dentro de una columna text
    /// (Cassandra no soporta arrays anidados de objetos sin UDT).
    /// </summary>
    public class Venta : BaseEntity
    {
        public string NumeroTicket { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        /// <summary>
        /// Cajero (campos aplanados en Cassandra: cajero_id, cajero_nombre).
        /// </summary>
        public CajeroRef Cajero { get; set; } = new();

        /// <summary>
        /// Items de la venta. Se serializa como JSON en una columna text
        /// llamada items_json para preservar la estructura.
        /// </summary>
        public List<VentaItem> Items { get; set; } = new();

        public decimal Total { get; set; }

        public string MetodoPago { get; set; } = string.Empty; // "efectivo" | "tarjeta"

        public string Estado { get; set; } = "completada"; // "completada" | "anulada"
    }

    public class CajeroRef
    {
        public Guid UsuarioId { get; set; }

        public string Nombre { get; set; } = string.Empty;
    }

    public class VentaItem
    {
        public Guid ProductoId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public int Cantidad { get; set; }

        public decimal PrecioUnitario { get; set; }

        public decimal Subtotal { get; set; }
    }
}
