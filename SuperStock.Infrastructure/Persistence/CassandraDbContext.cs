using Cassandra;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperStock.Infrastructure.Settings;

namespace SuperStock.Infrastructure.Persistence
{
    /// <summary>
    /// Contexto de acceso a Cassandra. Reemplaza a MongoDbContext.
    ///
    /// Responsabilidades:
    ///   - Crear el Cluster y la Session (pool de conexiones thread-safe).
    ///   - Crear el keyspace si no existe (con ReplicationFactor configurable).
    ///   - Crear las tablas si no existen (idempotente, igual que InitializeAsync de Mongo).
    ///   - Crear indices secundarios para busquedas frecuentes.
    ///
    /// CONCEPTOS APLICADOS:
    ///   - Keyspace: contenedor logico de tablas, equivalente a una "database" en SQL.
    ///   - Replication Factor: cuantas copias del dato existen en el cluster.
    ///       RF=2 sobre 2+ nodos garantiza Alta Disponibilidad.
    ///   - SimpleStrategy: estrategia de replicacion para un solo datacenter.
    ///   - Consistency Level QUORUM: requiere mayoria de replicas para confirmar
    ///       lectura/escritura. Con RF=2, QUORUM = 2 (todas las replicas).
    /// </summary>
    public class CassandraDbContext : IDisposable
    {
        private readonly CassandraSettings _settings;
        private readonly ILogger<CassandraDbContext> _logger;
        private Cluster? _cluster;
        private ISession? _session;
        private readonly object _lock = new();

        public CassandraDbContext(
            IOptions<CassandraSettings> options,
            ILogger<CassandraDbContext> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Session compartida (singleton). El driver maneja internamente un pool
        /// de conexiones thread-safe.
        /// </summary>
        public ISession Session
        {
            get
            {
                if (_session != null) return _session;
                lock (_lock)
                {
                    _session ??= BuildSession();
                }
                return _session;
            }
        }

        private ISession BuildSession()
        {
            var contactPoints = _settings.ContactPoints
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            _logger.LogInformation(
                "Conectando a Cassandra. Contact points: {Points}, DC: {DC}, Keyspace: {KS}",
                string.Join(",", contactPoints), _settings.LocalDataCenter, _settings.Keyspace);

            _cluster = Cluster.Builder()
                .AddContactPoints(contactPoints)
                .WithPort(_settings.Port)
                // CONCEPTO: LocalDataCenter es obligatorio cuando se proveen contact points.
                .WithLoadBalancingPolicy(new DCAwareRoundRobinPolicy(_settings.LocalDataCenter))
                // CONCEPTO: QUORUM = mayoria de replicas. Con RF=2 requiere 2 nodos UP.
                .WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.Quorum))
                .Build();

            // Primer connect SIN keyspace (puede no existir todavia).
            var systemSession = _cluster.Connect();

            EnsureKeyspace(systemSession);

            // Reconectar apuntando al keyspace.
            systemSession.ChangeKeyspace(_settings.Keyspace);
            return systemSession;
        }

        private void EnsureKeyspace(ISession session)
        {
            var cql = $@"
                CREATE KEYSPACE IF NOT EXISTS {_settings.Keyspace}
                WITH replication = {{ 'class': 'SimpleStrategy', 'replication_factor': {_settings.ReplicationFactor} }}
            ";
            session.Execute(cql);
            _logger.LogInformation("Keyspace '{KS}' asegurado (RF={RF}).", _settings.Keyspace, _settings.ReplicationFactor);
        }

        /// <summary>
        /// Crea todas las tablas e indices necesarios. Idempotente.
        /// Equivalente al InitializeAsync de MongoDbContext.
        /// </summary>
        public async Task InitializeAsync()
        {
            var s = Session;

            // ── Tabla usuarios ──────────────────────────────────────────
            await s.ExecuteAsync(new SimpleStatement(@"
                CREATE TABLE IF NOT EXISTS usuarios (
                    id uuid PRIMARY KEY,
                    nombre text,
                    apellido text,
                    email text,
                    telefono text,
                    password_hash text,
                    rol text,
                    sucursal text,
                    activo boolean,
                    ultimo_acceso timestamp,
                    created_at timestamp,
                    updated_at timestamp,
                    is_deleted boolean,
                    created_by text
                )"));

            // Indice secundario en email (busquedas por GetByEmailAsync)
            await s.ExecuteAsync(new SimpleStatement(
                "CREATE INDEX IF NOT EXISTS idx_usuarios_email ON usuarios (email)"));

            // ── Tabla productos ─────────────────────────────────────────
            await s.ExecuteAsync(new SimpleStatement(@"
                CREATE TABLE IF NOT EXISTS productos (
                    id uuid PRIMARY KEY,
                    codigo_barras text,
                    nombre text,
                    marca text,
                    categoria text,
                    subcategoria text,
                    precio_venta decimal,
                    precio_costo decimal,
                    stock_actual int,
                    stock_minimo int,
                    unidad_medida text,
                    activo boolean,
                    proveedor_id uuid,
                    proveedor_nombre text,
                    proveedor_telefono text,
                    detalles map<text, text>,
                    created_at timestamp,
                    updated_at timestamp,
                    is_deleted boolean,
                    created_by text
                )"));

            await s.ExecuteAsync(new SimpleStatement(
                "CREATE INDEX IF NOT EXISTS idx_productos_codigo_barras ON productos (codigo_barras)"));

            await s.ExecuteAsync(new SimpleStatement(
                "CREATE INDEX IF NOT EXISTS idx_productos_categoria ON productos (categoria)"));

            await s.ExecuteAsync(new SimpleStatement(
                "CREATE INDEX IF NOT EXISTS idx_productos_activo ON productos (activo)"));

            // ── Tabla proveedores ───────────────────────────────────────
            await s.ExecuteAsync(new SimpleStatement(@"
                CREATE TABLE IF NOT EXISTS proveedores (
                    id uuid PRIMARY KEY,
                    nombre text,
                    contacto text,
                    telefono text,
                    email text,
                    direccion text,
                    categoria_suministro list<text>,
                    condiciones_pago text,
                    activo boolean,
                    created_at timestamp,
                    updated_at timestamp,
                    is_deleted boolean,
                    created_by text
                )"));

            await s.ExecuteAsync(new SimpleStatement(
                "CREATE INDEX IF NOT EXISTS idx_proveedores_activo ON proveedores (activo)"));

            // ── Tabla ventas ────────────────────────────────────────────
            // items_json: serializacion JSON de List<VentaItem> (Cassandra no soporta
            // listas de objetos sin UDT; usar text es la opcion mas portable).
            await s.ExecuteAsync(new SimpleStatement(@"
                CREATE TABLE IF NOT EXISTS ventas (
                    id uuid PRIMARY KEY,
                    numero_ticket text,
                    fecha timestamp,
                    cajero_id uuid,
                    cajero_nombre text,
                    items_json text,
                    total decimal,
                    metodo_pago text,
                    estado text,
                    created_at timestamp,
                    updated_at timestamp,
                    is_deleted boolean,
                    created_by text
                )"));

            await s.ExecuteAsync(new SimpleStatement(
                "CREATE INDEX IF NOT EXISTS idx_ventas_estado ON ventas (estado)"));

            await s.ExecuteAsync(new SimpleStatement(
                "CREATE INDEX IF NOT EXISTS idx_ventas_cajero_id ON ventas (cajero_id)"));

            _logger.LogInformation("Tablas e indices creados correctamente.");
        }

        public void Dispose()
        {
            _session?.Dispose();
            _cluster?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
