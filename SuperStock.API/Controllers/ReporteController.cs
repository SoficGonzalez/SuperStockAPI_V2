using Cassandra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperStock.Domain.Entities;
using SuperStock.Infrastructure.Persistence;
using System.Text.Json;

namespace SuperStock.API.Controllers
{
    /// <summary>
    /// Reportes sobre Cassandra. A diferencia de Mongo (Aggregation Pipeline),
    /// aqui las agregaciones se hacen en memoria sobre los resultados de CQL.
    /// Cassandra no es una base de datos analitica; para reportes pesados se
    /// recomienda exportar a Spark/Presto. Para volumenes pequenos esto es suficiente.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "admin,gerente")]
    public class ReporteController : ControllerBase
    {
        private readonly CassandraDbContext _context;

        public ReporteController(CassandraDbContext context)
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

            var stmt = new SimpleStatement(
                "SELECT fecha, total, estado, is_deleted FROM ventas WHERE estado = ?",
                "completada");

            var rs = await _context.Session.ExecuteAsync(stmt);

            var data = rs
                .Where(r => !r.GetValue<bool>("is_deleted"))
                .Select(r => new
                {
                    Fecha = r.GetValue<DateTimeOffset>("fecha").UtcDateTime,
                    Total = r.GetValue<decimal>("total")
                })
                .Where(v => v.Fecha >= desde && v.Fecha <= hasta)
                .GroupBy(v => v.Fecha.ToString("yyyy-MM-dd"))
                .Select(g => new
                {
                    Fecha = g.Key,
                    TotalVentas = g.Sum(x => x.Total),
                    CantidadTransacciones = g.Count(),
                    PromedioTicket = Math.Round(g.Average(x => x.Total), 2)
                })
                .OrderByDescending(x => x.Fecha)
                .ToList();

            return Ok(new
            {
                Periodo = new { Desde = desde.ToString("yyyy-MM-dd"), Hasta = hasta.ToString("yyyy-MM-dd") },
                Data = data
            });
        }

        /// <summary>
        /// GET /api/reporte/productos-stock-bajo
        /// </summary>
        [HttpGet("productos-stock-bajo")]
        public async Task<IActionResult> ProductosStockBajo()
        {
            var stmt = new SimpleStatement(
                "SELECT nombre, categoria, stock_actual, stock_minimo, proveedor_nombre, activo, is_deleted FROM productos WHERE activo = ?",
                true);

            var rs = await _context.Session.ExecuteAsync(stmt);

            var data = rs
                .Where(r => !r.GetValue<bool>("is_deleted"))
                .Select(r => new
                {
                    Nombre = r.GetValue<string>("nombre"),
                    Categoria = r.GetValue<string>("categoria"),
                    StockActual = r.GetValue<int>("stock_actual"),
                    StockMinimo = r.GetValue<int>("stock_minimo"),
                    Proveedor = r.GetValue<string>("proveedor_nombre")
                })
                .Where(p => p.StockActual <= p.StockMinimo)
                .Select(p => new
                {
                    p.Nombre,
                    p.Categoria,
                    p.StockActual,
                    p.StockMinimo,
                    Deficit = p.StockMinimo - p.StockActual,
                    p.Proveedor
                })
                .OrderByDescending(p => p.Deficit)
                .ToList();

            return Ok(data);
        }

        /// <summary>
        /// GET /api/reporte/ventas-por-categoria?fechaDesde=2026-01-01&amp;fechaHasta=2026-01-31
        /// Agrupa los items vendidos por nombre de producto.
        /// </summary>
        [HttpGet("ventas-por-categoria")]
        public async Task<IActionResult> VentasPorCategoria(
            [FromQuery] DateTime? fechaDesde,
            [FromQuery] DateTime? fechaHasta)
        {
            var desde = fechaDesde ?? DateTime.UtcNow.AddDays(-30);
            var hasta = fechaHasta ?? DateTime.UtcNow;

            var stmt = new SimpleStatement(
                "SELECT fecha, items_json, estado, is_deleted FROM ventas WHERE estado = ?",
                "completada");

            var rs = await _context.Session.ExecuteAsync(stmt);

            var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            // Aplanar todos los items en una sola lista
            var todosItems = rs
                .Where(r => !r.GetValue<bool>("is_deleted"))
                .Select(r => new
                {
                    Fecha = r.GetValue<DateTimeOffset>("fecha").UtcDateTime,
                    ItemsJson = r.GetValue<string>("items_json")
                })
                .Where(v => v.Fecha >= desde && v.Fecha <= hasta)
                .SelectMany(v =>
                {
                    if (string.IsNullOrWhiteSpace(v.ItemsJson)) return new List<VentaItem>();
                    return JsonSerializer.Deserialize<List<VentaItem>>(v.ItemsJson, jsonOpts) ?? new();
                })
                .ToList();

            var data = todosItems
                .GroupBy(i => i.Nombre)
                .Select(g => new
                {
                    Producto = g.Key,
                    CantidadVendida = g.Sum(i => i.Cantidad),
                    TotalVendido = Math.Round(g.Sum(i => i.Subtotal), 2)
                })
                .OrderByDescending(x => x.TotalVendido)
                .ToList();

            return Ok(new
            {
                Periodo = new { Desde = desde.ToString("yyyy-MM-dd"), Hasta = hasta.ToString("yyyy-MM-dd") },
                TopProductos = data
            });
        }
    }
}