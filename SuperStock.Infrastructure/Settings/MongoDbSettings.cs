namespace SuperStock.Infrastructure.Settings
{
    /// <summary>
    /// Configuracion de conexion a MongoDB.
    /// Se mapea desde la seccion "MongoDb" de appsettings.json.
    /// </summary>
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }
}
