namespace SuperStock.Infrastructure.Settings
{
    /// <summary>
    /// Configuracion de conexion a un cluster de Cassandra.
    /// Se mapea desde la seccion "Cassandra" de appsettings.json.
    /// </summary>
    public class CassandraSettings
    {
        /// <summary>
        /// Lista de hosts del cluster separados por coma.
        /// Ejemplo: "cassandra-seed,cassandra-node2"
        /// </summary>
        public string ContactPoints { get; set; } = "localhost";

        /// <summary>
        /// Puerto CQL (default 9042).
        /// </summary>
        public int Port { get; set; } = 9042;

        /// <summary>
        /// Nombre del datacenter local (default "dc1").
        /// Requerido por el driver para evitar errores de load-balancing.
        /// </summary>
        public string LocalDataCenter { get; set; } = "dc1";

        /// <summary>
        /// Nombre del keyspace donde viven las tablas.
        /// </summary>
        public string Keyspace { get; set; } = "superstock";

        /// <summary>
        /// Replication factor del keyspace (alta disponibilidad >= 2).
        /// </summary>
        public int ReplicationFactor { get; set; } = 2;
    }
}
