using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperStock.Infrastructure.Persistence;

namespace SuperStock.API.Controllers
{
    /// <summary>
    /// Endpoints de reportes que usan el Aggregation Pipeline de MongoDB.
    ///
    /// CONCEPTO: Aggregation Pipeline
    /// Framework de procesamiento de datos que encadena operaciones como
    /// $match, $group, $sort, $project para transformar y resumir documentos
    /// directamente en la base de datos, sin traer datos crudos a la app.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "admin,gerente")]
    public class ReporteController : ControllerBase
    {
        private readonly MongoDbContext _context;

        public ReporteController(MongoDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/reporte/ventas-por-dia?fechaDesde=2026-01-01&amp;fechaHasta=2026-01-31
        /// </summary>
        [HttpGet("ventas-por-dia")]
        public async Task<IActionResult> VentasPorDia(
            [FromQuery] DateTime? fechaDesde,
            [FromQuery] DateTime? fechaHasta)
        {
            var desde = fechaDesde ?? DateTime.UtcNow.AddDays(-30);
            var hasta = fechaHasta ?? DateTime.UtcNow;

            var pipeline = new[]
            {
                new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
                {
                    { "fecha", new MongoDB.Bson.BsonDocument
                        {
                            { "$gte", desde },
                            { "$lte", hasta }
                        }
                    },
                    { "estado", "completada" },
                    { "isDeleted", false }
                }),
                new MongoDB.Bson.BsonDocument("$group", new MongoDB.Bson.BsonDocument
                {
                    { "_id", new MongoDB.Bson.BsonDocument("$dateToString",
                        new MongoDB.Bson.BsonDocument
                        {
                            { "format", "%Y-%m-%d" },
                            { "date", "$fecha" }
                        })
                    },
                    { "totalVentas", new MongoDB.Bson.BsonDocument("$sum", "$total") },
                    { "cantidadTransacciones", new MongoDB.Bson.BsonDocument("$sum", 1) },
                    { "promedioTicket", new MongoDB.Bson.BsonDocument("$avg", "$total") }
                }),
                new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("_id", -1)),
                new MongoDB.Bson.BsonDocument("$project", new MongoDB.Bson.BsonDocument
                {
                    { "_id", 0 },
                    { "fecha", "$_id" },
                    { "totalVentas", 1 },
                    { "cantidadTransacciones", 1 },
                    { "promedioTicket", new MongoDB.Bson.BsonDocument("$round",
                        new MongoDB.Bson.BsonArray { "$promedioTicket", 2 }) }
                })
            };

            var list = new List<MongoDB.Bson.BsonDocument>();
            using (var cursor = await _context.Ventas.AggregateAsync<MongoDB.Bson.BsonDocument>(pipeline))
            {
                while (await cursor.MoveNextAsync())
                {
                    list.AddRange(cursor.Current);
                }
            }

            return Ok(new
            {
                Periodo = new { Desde = desde.ToString("yyyy-MM-dd"), Hasta = hasta.ToString("yyyy-MM-dd") },
                Data = list.Select(d => new
                {
                    Fecha = d["fecha"].AsString,
                    TotalVentas = d["totalVentas"].ToDecimal(),
                    CantidadTransacciones = d["cantidadTransacciones"].AsInt32,
                    PromedioTicket = d["promedioTicket"].ToDecimal()
                })
            });
        }

        /// <summary>
        /// GET /api/reporte/productos-stock-bajo
        /// </summary>
        [HttpGet("productos-stock-bajo")]
        public async Task<IActionResult> ProductosStockBajo()
        {
            var pipeline = new[]
            {
                new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
                {
                    { "activo", true },
                    { "isDeleted", false },
                    { "$expr", new MongoDB.Bson.BsonDocument("$lte",
                        new MongoDB.Bson.BsonArray { "$stockActual", "$stockMinimo" }) }
                }),
                new MongoDB.Bson.BsonDocument("$addFields", new MongoDB.Bson.BsonDocument
                {
                    { "deficit", new MongoDB.Bson.BsonDocument("$subtract",
                        new MongoDB.Bson.BsonArray { "$stockMinimo", "$stockActual" }) }
                }),
                new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("deficit", -1)),
                new MongoDB.Bson.BsonDocument("$project", new MongoDB.Bson.BsonDocument
                {
                    { "nombre", 1 },
                    { "categoria", 1 },
                    { "stockActual", 1 },
                    { "stockMinimo", 1 },
                    { "deficit", 1 },
                    { "proveedor", 1 }
                })
            };

            var list = new List<MongoDB.Bson.BsonDocument>();
            using (var cursor = await _context.Productos.AggregateAsync<MongoDB.Bson.BsonDocument>(pipeline))
            {
                while (await cursor.MoveNextAsync())
                {
                    list.AddRange(cursor.Current);
                }
            }

            return Ok(list);
        }

        /// <summary>
        /// GET /api/reporte/ventas-por-categoria?fechaDesde=2026-01-01&amp;fechaHasta=2026-01-31
        /// </summary>
        [HttpGet("ventas-por-categoria")]
        public async Task<IActionResult> VentasPorCategoria(
            [FromQuery] DateTime? fechaDesde,
            [FromQuery] DateTime? fechaHasta)
        {
            var desde = fechaDesde ?? DateTime.UtcNow.AddDays(-30);
            var hasta = fechaHasta ?? DateTime.UtcNow;

            var pipeline = new[]
            {
                new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
                {
                    { "fecha", new MongoDB.Bson.BsonDocument { { "$gte", desde }, { "$lte", hasta } } },
                    { "estado", "completada" },
                    { "isDeleted", false }
                }),
                new MongoDB.Bson.BsonDocument("$unwind", "$items"),
                new MongoDB.Bson.BsonDocument("$group", new MongoDB.Bson.BsonDocument
                {
                    { "_id", "$items.nombre" },
                    { "cantidadVendida", new MongoDB.Bson.BsonDocument("$sum", "$items.cantidad") },
                    { "totalVendido", new MongoDB.Bson.BsonDocument("$sum", "$items.subtotal") }
                }),
                new MongoDB.Bson.BsonDocument("$sort", new MongoDB.Bson.BsonDocument("totalVendido", -1)),
                new MongoDB.Bson.BsonDocument("$project", new MongoDB.Bson.BsonDocument
                {
                    { "_id", 0 },
                    { "producto", "$_id" },
                    { "cantidadVendida", 1 },
                    { "totalVendido", new MongoDB.Bson.BsonDocument("$round",
                        new MongoDB.Bson.BsonArray { "$totalVendido", 2 }) }
                })
            };

            var list = new List<MongoDB.Bson.BsonDocument>();
            using (var cursor = await _context.Ventas.AggregateAsync<MongoDB.Bson.BsonDocument>(pipeline))
            {
                while (await cursor.MoveNextAsync())
                {
                    list.AddRange(cursor.Current);
                }
            }

            return Ok(new
            {
                Periodo = new { Desde = desde.ToString("yyyy-MM-dd"), Hasta = hasta.ToString("yyyy-MM-dd") },
                TopProductos = list
            });
        }
    }
}