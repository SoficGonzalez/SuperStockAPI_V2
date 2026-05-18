namespace SuperStock.Domain.Entities
{
    /// <summary>
    /// Producto con esquema polimorfico.
    /// El campo Detalles es un diccionario flexible que almacena atributos
    /// especificos de cada categoria sin requerir cambios de esquema.
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
        /// Referencia embebida al proveedor (desnormalizada para lectura rapida).
        /// </summary>
        public ProveedorRef? Proveedor { get; set; }

        /// <summary>
        /// Atributos especificos de la categoria del producto.
        /// Ejemplos:
        ///   Perecederos: { "fecha_vencimiento": "2026-03-15", "temperatura": "0-4 C" }
        ///   Limpieza:    { "concentracion": "5%", "advertencias": ["inflamable"] }
        /// Se almacena como BsonDocument en MongoDB, mapeado a Dictionary en C#.
        /// </summary>
        public Dictionary<string, object>? Detalles { get; set; }
    }

    /// <summary>
    /// Referencia embebida de proveedor dentro de un producto.
    /// Evita JOINs en la consulta mas frecuente (lectura en POS).
    /// </summary>
    public class ProveedorRef
    {
        public string ProveedorId { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;
    }
}
