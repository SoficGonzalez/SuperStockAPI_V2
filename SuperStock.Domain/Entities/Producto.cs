namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Producto con esquema polimorfico (limitado en Cassandra).
    /// El campo Detalles se mapea a un map<text,text> en CQL,
    /// que permite cualquier clave-valor pero pierde el tipado original.
    /// </summary>
    public class Producto : BaseEntity
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
        /// Referencia desnormalizada al proveedor.
        /// En Cassandra se aplanan los campos como columnas individuales
        /// (proveedor_id, proveedor_nombre, proveedor_telefono).
        /// </summary>
        public ProveedorRef? Proveedor { get; set; }

        /// <summary>
        /// Atributos adicionales clave-valor. En Cassandra es map<text,text>.
        /// Los valores numericos/booleanos se almacenan como string serializado.
        /// </summary>
        public Dictionary<string, string>? Detalles { get; set; }
    }

    /// <summary>
    /// Referencia embebida de proveedor dentro de un producto.
    /// </summary>
    public class ProveedorRef
    {
        public Guid ProveedorId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;
    }
}
