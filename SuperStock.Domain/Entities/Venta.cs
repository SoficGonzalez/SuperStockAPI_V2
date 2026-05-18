namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Documento de venta autocontenido.
    /// Los items se embeben directamente para evitar JOINs en cobro de caja.
    /// </summary>
    public class Venta : BaseEntity
    {
        public string NumeroTicket { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        /// <summary>
        /// Referencia desnormalizada del cajero que proceso la venta.
        /// </summary>
        public CajeroRef Cajero { get; set; } = new();

        /// <summary>
        /// Items de la venta embebidos. Cada item es una copia snapshot
        /// del producto al momento de la venta (precio puede cambiar despues).
        /// </summary>
        public List<VentaItem> Items { get; set; } = new();

        public decimal Total { get; set; }

        public string MetodoPago { get; set; } = string.Empty; // "efectivo" | "tarjeta"

        public string Estado { get; set; } = "completada"; // "completada" | "anulada"
    }

    public class CajeroRef
    {
        public string UsuarioId { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;
    }

    public class VentaItem
    {
        public string ProductoId { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public int Cantidad { get; set; }

        public decimal PrecioUnitario { get; set; }

        public decimal Subtotal { get; set; }
    }
}
