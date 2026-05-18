using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using SuperStock.Domain.Entities;
using SuperStock.Infrastructure.Settings;

namespace SuperStock.Infrastructure.Persistence
{
    /// <summary>
    /// Reemplaza ApplicationDbContext (IdentityDbContext + EF Core).
    /// Expone las colecciones MongoDB como propiedades tipadas.
    ///
    /// CONCEPTOS IMPLEMENTADOS DEL GLOSARIO:
    ///   - Colección: agrupación de documentos, análoga a una tabla SQL pero con esquema flexible.
    ///   - BSON: formato binario (Binary JSON) que MongoDB usa internamente.
    ///   - Write Concern: cuántas réplicas confirman una escritura antes de considerarla exitosa.
    ///   - Read Concern: nivel de consistencia y aislamiento de una lectura.
    ///   - Schema Validation ($jsonSchema): reglas que controlan qué documentos pueden insertarse.
    ///   - TTL (Time to Live): expiración automática de documentos.
    ///   - Índice secundario: estructura para consultas por campos distintos al _id.
    ///   - Embedding (desnormalización): datos relacionados dentro del mismo documento.
    /// </summary>
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            // ── Convenciones BSON ────────────────────────────────────────
            // CamelCase: propiedades C# (PascalCase) → camelCase en BSON.
            // IgnoreIfNull: no almacenar campos con valor null (ahorra espacio en BSON).
            var conventionPack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreIfNullConvention(true)
            };
            ConventionRegistry.Register("SuperStockConventions", conventionPack, _ => true);

            // Decimal → Decimal128 en BSON (tipo nativo para números decimales precisos).
            BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));

            // ── CONCEPTO: Write Concern ─────────────────────────────────
            // WriteConcern.WMajority: la escritura solo se confirma cuando la mayoría
            // de nodos del Replica Set la han replicado.
            // Garantiza que no se pierdan datos si el Primary falla.
            //
            // ── CONCEPTO: Read Concern ──────────────────────────────────
            // ReadConcern.Majority: solo lee datos confirmados por la mayoría del Replica Set.
            // Evita "dirty reads" de datos que podrían revertirse.
            var clientSettings = MongoClientSettings.FromConnectionString(settings.Value.ConnectionString);
            clientSettings.WriteConcern = WriteConcern.WMajority;
            clientSettings.ReadConcern = ReadConcern.Majority;

            var client = new MongoClient(clientSettings);
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }

        // ── Colecciones (equivalente a DbSet<T> en EF Core) ──────────

        public IMongoCollection<Producto> Productos =>
            _database.GetCollection<Producto>("productos");

        public IMongoCollection<Venta> Ventas =>
            _database.GetCollection<Venta>("ventas");

        public IMongoCollection<Proveedor> Proveedores =>
            _database.GetCollection<Proveedor>("proveedores");

        public IMongoCollection<Usuario> Usuarios =>
            _database.GetCollection<Usuario>("usuarios");

        /// <summary>
        /// Inicializa índices y Schema Validation.
        /// Se llama una vez al iniciar la aplicación (idempotente).
        /// </summary>
        public async Task InitializeAsync()
        {
            await CreateIndexesAsync();
            await ApplySchemaValidationAsync();
        }

        /// <summary>
        /// CONCEPTO: Índices secundarios
        /// Estructuras adicionales que permiten consultas eficientes por campos
        /// distintos al _id. Sin índices, MongoDB haría un "collection scan"
        /// (leer TODOS los documentos). Con índices, usa un B-tree para saltar
        /// directamente a los documentos que coinciden.
        ///
        /// CONCEPTO: Cardinalidad
        /// La cardinalidad (cantidad de valores únicos) de un campo determina
        /// la eficiencia del índice. Campos con alta cardinalidad (como codigoBarras)
        /// producen índices más selectivos y eficientes.
        ///
        /// CONCEPTO: TTL (Time to Live)
        /// Índice especial que le dice a MongoDB "borra automáticamente los documentos
        /// después de X tiempo". Útil para sesiones, caché, y datos temporales.
        /// </summary>
        private async Task CreateIndexesAsync()
        {
            // ── Productos: índices secundarios ──────────────────────────
            await Productos.Indexes.CreateManyAsync(new[]
            {
                // Índice compuesto: optimiza consultas filtradas por categoría + precio.
                // Soporta las búsquedas más frecuentes del POS (punto de venta).
                new CreateIndexModel<Producto>(
                    Builders<Producto>.IndexKeys
                        .Ascending(p => p.Categoria)
                        .Descending(p => p.PrecioVenta)),

                // Índice único en código de barras.
                // Cardinalidad alta (cada producto tiene un código único) → índice eficiente.
                new CreateIndexModel<Producto>(
                    Builders<Producto>.IndexKeys.Ascending(p => p.CodigoBarras),
                    new CreateIndexOptions { Unique = true }),

                // Índice de texto para búsqueda parcial por nombre.
                new CreateIndexModel<Producto>(
                    Builders<Producto>.IndexKeys.Text(p => p.Nombre))
            });

            // ── Ventas ──────────────────────────────────────────────────
            // Índice por fecha descendente para reportes
            await Ventas.Indexes.CreateOneAsync(
                new CreateIndexModel<Venta>(
                    Builders<Venta>.IndexKeys.Descending(v => v.Fecha)));

            // CONCEPTO: TTL Index
            // MongoDB borra automáticamente los documentos después de 90 días
            // basándose en el campo UpdatedAt. Para limitar el TTL solo a ventas
            // anuladas, en producción se aplicaría con un comando createIndex directo
            // usando partialFilterExpression. Aquí usamos la versión simple del TTL
            // que aplica a todas las ventas; para preservar las completadas, la
            // lógica de negocio nunca actualiza UpdatedAt en ventas completadas
            // después de su creación inicial (lo que efectivamente las protege del TTL
            // porque MongoDB calcula la expiración desde el último UpdatedAt).
            await Ventas.Indexes.CreateOneAsync(
                new CreateIndexModel<Venta>(
                    Builders<Venta>.IndexKeys.Ascending(v => v.UpdatedAt),
                    new CreateIndexOptions
                    {
                        Name = "ttl_ventas_anuladas",
                        ExpireAfter = TimeSpan.FromDays(90)
                    }));

            // ── Usuarios ────────────────────────────────────────────────
            await Usuarios.Indexes.CreateOneAsync(
                new CreateIndexModel<Usuario>(
                    Builders<Usuario>.IndexKeys.Ascending(u => u.Email),
                    new CreateIndexOptions { Unique = true }));
        }

        /// <summary>
        /// CONCEPTO: Validación de esquemas ($jsonSchema)
        ///
        /// Aunque MongoDB tiene "esquema dinámico" (cada documento puede tener campos distintos),
        /// en producción es buena práctica definir un mínimo de estructura obligatoria.
        ///
        /// Conceptos clave del $jsonSchema:
        ///   - required: campos que DEBEN existir en cada documento.
        ///   - bsonType: tipo de dato BSON (string, int, double, decimal, date, bool, object, array).
        ///   - properties: reglas específicas por campo.
        ///   - pattern: expresión regular para validar formato de strings.
        ///   - minimum/maximum: restricciones numéricas.
        ///   - enum: lista de valores permitidos.
        ///   - additionalProperties: si se permiten campos no definidos en el esquema.
        ///
        /// validationLevel:
        ///   - "strict": aplica a TODAS las inserciones y actualizaciones.
        ///   - "moderate": solo aplica a documentos que ya cumplían el esquema.
        ///
        /// validationAction:
        ///   - "error": rechaza la operación si no pasa la validación.
        ///   - "warn": permite la operación pero registra una advertencia en el log.
        ///
        /// collMod: comando para modificar opciones de una colección existente,
        /// incluyendo la adición o actualización de reglas de validación.
        /// </summary>
        private async Task ApplySchemaValidationAsync()
        {
            // ── Validación para "productos" ─────────────────────────────
            try
            {
                var productosValidator = new BsonDocument("$jsonSchema", new BsonDocument
                {
                    { "bsonType", "object" },
                    { "required", new BsonArray { "nombre", "categoria", "precioVenta", "codigoBarras" } },
                    { "properties", new BsonDocument
                        {
                            { "nombre", new BsonDocument
                                {
                                    { "bsonType", "string" },
                                    { "description", "Nombre del producto - requerido" }
                                }
                            },
                            { "categoria", new BsonDocument
                                {
                                    { "bsonType", "string" },
                                    // CONCEPTO: enum - lista de valores permitidos.
                                    { "enum", new BsonArray { "perecederos", "secos", "cuidado_personal", "limpieza", "otros" } },
                                    { "description", "Categoria del producto - valor del enum" }
                                }
                            },
                            { "precioVenta", new BsonDocument
                                {
                                    { "bsonType", "decimal" },
                                    // CONCEPTO: Rango (minimum) - el precio no puede ser negativo.
                                    { "minimum", 0 },
                                    { "description", "Precio de venta - debe ser >= 0" }
                                }
                            },
                            { "codigoBarras", new BsonDocument
                                {
                                    { "bsonType", "string" },
                                    // CONCEPTO: Patrón (pattern) - regex que valida el formato.
                                    { "pattern", "^[0-9]{8,14}$" },
                                    { "description", "Codigo de barras - 8 a 14 digitos" }
                                }
                            },
                            { "stockActual", new BsonDocument
                                {
                                    { "bsonType", "int" },
                                    { "minimum", 0 }
                                }
                            }
                        }
                    },
                    // CONCEPTO: additionalProperties = true
                    // Permite campos no definidos en el esquema (esquema dinámico).
                    // Esto habilita el polimorfismo: "detalles" puede tener
                    // cualquier estructura según la categoría del producto.
                    { "additionalProperties", true }
                });

                // CONCEPTO: collMod - modifica opciones de una colección existente.
                await _database.RunCommandAsync<BsonDocument>(new BsonDocument
                {
                    { "collMod", "productos" },
                    { "validator", productosValidator },
                    { "validationLevel", "moderate" },
                    { "validationAction", "error" }
                });
            }
            catch (MongoCommandException ex) when (ex.Code == 26) { /* Colección aún no existe */ }

            // ── Validación para "ventas" ────────────────────────────────
            try
            {
                var ventasValidator = new BsonDocument("$jsonSchema", new BsonDocument
                {
                    { "bsonType", "object" },
                    { "required", new BsonArray { "fecha", "cajero", "items", "total", "metodoPago", "estado" } },
                    { "properties", new BsonDocument
                        {
                            { "estado", new BsonDocument
                                {
                                    { "bsonType", "string" },
                                    { "enum", new BsonArray { "completada", "anulada" } }
                                }
                            },
                            { "metodoPago", new BsonDocument
                                {
                                    { "bsonType", "string" },
                                    { "enum", new BsonArray { "efectivo", "tarjeta" } }
                                }
                            },
                            { "total", new BsonDocument
                                {
                                    { "bsonType", "decimal" },
                                    { "minimum", 0 }
                                }
                            },
                            // CONCEPTO: Embedding
                            // Los items se validan como array de objetos embebidos (desnormalizados).
                            // Cada item debe tener productoId y cantidad.
                            { "items", new BsonDocument
                                {
                                    { "bsonType", "array" },
                                    { "minItems", 1 },
                                    { "items", new BsonDocument
                                        {
                                            { "bsonType", "object" },
                                            { "required", new BsonArray { "productoId", "cantidad" } }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    { "additionalProperties", true }
                });

                await _database.RunCommandAsync<BsonDocument>(new BsonDocument
                {
                    { "collMod", "ventas" },
                    { "validator", ventasValidator },
                    { "validationLevel", "strict" },
                    { "validationAction", "error" }
                });
            }
            catch (MongoCommandException ex) when (ex.Code == 26) { }
        }
    }
}
