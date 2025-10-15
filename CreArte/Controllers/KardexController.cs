using CreArte.Data;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Controllers
{
    public class KardexController : Controller
    {
        private readonly CreArteDbContext _db;

        public KardexController(CreArteDbContext db)
        {
            _db = db;
        }

        // -------------------------------------------------------
        // GET: /Kardex/Producto/{id}?desde=yyyy-MM-dd&hasta=yyyy-MM-dd
        // {id} = PRODUCTO_ID
        // -------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Producto(string id, DateTime? desde, DateTime? hasta)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 1) Encabezado de producto (nombre/imagen)
            var prod = await _db.PRODUCTO
                .Where(p => p.PRODUCTO_ID == id)
                .Select(p => new { p.PRODUCTO_ID, p.PRODUCTO_NOMBRE, p.IMAGEN_PRODUCTO })
                .FirstOrDefaultAsync();

            if (prod == null) return NotFound();

        //     Regla: tomamos el inventario más “reciente” por FECHA_MODIFICACION o FECHA_CREACION
         var invId = await _db.INVENTARIO
        .Where(i => i.PRODUCTO_ID == id && !i.ELIMINADO)
        .OrderByDescending(i => i.FECHA_MODIFICACION ?? i.FECHA_CREACION)
        .Select(i => i.INVENTARIO_ID)
        .FirstOrDefaultAsync();

            // 2) Rango de fechas normalizado
            //    - Si no se envía, usamos todo el histórico
            //    - Hasta incluye el día completo
            DateTime? fDesde = desde?.Date;
            DateTime? fHasta = hasta?.Date.AddDays(1).AddTicks(-1);

            // 3) SALDO INICIAL: suma de movimientos anteriores a "desde"
            int saldoInicial = 0;
            if (fDesde.HasValue)
            {
                var prev = await _db.KARDEX
                    .Where(k => k.PRODUCTO_ID == id
                             && k.ELIMINADO == false
                             && k.FECHA < fDesde.Value)
                    .Select(k => new { k.TIPO_MOVIMIENTO, k.CANTIDAD })
                    .ToListAsync();

                // Nuevo mapeo de signo por tipo
                saldoInicial = prev.Sum(m =>
                    (m.TIPO_MOVIMIENTO == "SALIDA" || m.TIPO_MOVIMIENTO == "AJUSTE SALIDA") ? -m.CANTIDAD :
                    (m.TIPO_MOVIMIENTO == "ENTRADA" || m.TIPO_MOVIMIENTO == "AJUSTE ENTRADA") ? +m.CANTIDAD :
                    0 // AJUSTE PRECIO u otros no afectan stock
                );
            }
            else
            {
                // Si no hay 'desde', el saldo inicial es 0 (mostramos todo el histórico)
                saldoInicial = 0;
            }

            // 4) Movimientos del rango
            var rangoQuery = _db.KARDEX
                .Where(k => k.PRODUCTO_ID == id && k.ELIMINADO == false);

            if (fDesde.HasValue) rangoQuery = rangoQuery.Where(k => k.FECHA >= fDesde.Value);
            if (fHasta.HasValue) rangoQuery = rangoQuery.Where(k => k.FECHA <= fHasta.Value);

            var movs = await rangoQuery
                .OrderBy(k => k.FECHA).ThenBy(k => k.KARDEX_ID) // orden estable
                .Select(k => new
                {
                    k.KARDEX_ID,
                    k.FECHA,
                    k.TIPO_MOVIMIENTO,
                    k.CANTIDAD,
                    k.COSTO_UNITARIO,
                    k.REFERENCIA
                })
                .ToListAsync();

            // 5) Totales + saldo acumulado por línea (en memoria)
            int totalE = 0, totalS = 0, totalA = 0; // TotalA = puedes usarlo para contar ajustes precio si quieres
            int saldoAcum = saldoInicial;

            var filas = new List<KardexMovimientoVM>(movs.Count);
            foreach (var m in movs)
            {
                // Totales por tipo (AJUSTE PRECIO no cambia cantidades)
                if (m.TIPO_MOVIMIENTO == "ENTRADA" || m.TIPO_MOVIMIENTO == "AJUSTE ENTRADA")
                    totalE += m.CANTIDAD;
                else if (m.TIPO_MOVIMIENTO == "SALIDA" || m.TIPO_MOVIMIENTO == "AJUSTE SALIDA")
                    totalS += m.CANTIDAD;
                else if (m.TIPO_MOVIMIENTO == "AJUSTE PRECIO")
                    totalA += 0; // si quieres contarlos, crea TotalAjustesPrecio aparte

                // Saldo acumulado (AJUSTE PRECIO = 0)
                int delta =
                    (m.TIPO_MOVIMIENTO == "SALIDA" || m.TIPO_MOVIMIENTO == "AJUSTE SALIDA") ? -m.CANTIDAD :
                    (m.TIPO_MOVIMIENTO == "ENTRADA" || m.TIPO_MOVIMIENTO == "AJUSTE ENTRADA") ? +m.CANTIDAD :
                    0;

                saldoAcum += delta;

                filas.Add(new KardexMovimientoVM
                {
                    KARDEX_ID = m.KARDEX_ID,
                    FECHA = m.FECHA,
                    TIPO_MOVIMIENTO = m.TIPO_MOVIMIENTO,
                    CANTIDAD = m.CANTIDAD,
                    COSTO_UNITARIO = m.COSTO_UNITARIO,
                    REFERENCIA = m.REFERENCIA,
                    SALDO_ACUMULADO = saldoAcum
                });
            }

            // 6) Construir VM
            var vm = new KardexProductoVM
            {
                PRODUCTO_ID = prod.PRODUCTO_ID,
                ProductoNombre = prod.PRODUCTO_NOMBRE,
                ImagenUrl = prod.IMAGEN_PRODUCTO,
                INVENTARIO_ID = invId,
                Filtros = new KardexProductoFilterVM { PRODUCTO_ID = id, Desde = desde, Hasta = hasta },
                SaldoInicial = saldoInicial,
                Movimientos = filas,
                TotalEntradas = totalE,
                TotalSalidas = totalS,
                TotalAjustes = totalA
            };

            return View(vm);
        }

        // ============================================================
        // BLOQUE 1: Utilidad privada para obtener el VM con filtros
        // - Reusa la misma lógica de /Kardex/Producto para evitar duplicar código
        // ============================================================
        private async Task<KardexProductoVM> BuildKardexProductoVmAsync(string id, DateTime? desde, DateTime? hasta, CancellationToken ct)
        {
            // 1) Encabezado de producto (nombre/imagen)
            var prod = await _db.PRODUCTO
                .Where(p => p.PRODUCTO_ID == id)
                .Select(p => new { p.PRODUCTO_ID, p.PRODUCTO_NOMBRE, p.IMAGEN_PRODUCTO })
                .FirstOrDefaultAsync(ct);

            if (prod == null)
                throw new InvalidOperationException("Producto no encontrado.");

            //Traer INVENTARIO_ID para el botón de regreso
            var invId = await _db.INVENTARIO
                .Where(i => i.PRODUCTO_ID == id && !i.ELIMINADO)
                .OrderByDescending(i => i.FECHA_MODIFICACION ?? i.FECHA_CREACION)
                .Select(i => i.INVENTARIO_ID)
                .FirstOrDefaultAsync(ct);

            // 2) Normalización de fechas
            DateTime? fDesde = desde?.Date;
            DateTime? fHasta = hasta?.Date.AddDays(1).AddTicks(-1);

            // 3) Saldo inicial (solo si hay 'desde')
            int saldoInicial = 0;
            if (fDesde.HasValue)
            {
                var prev = await _db.KARDEX
                    .Where(k => k.PRODUCTO_ID == id && k.ELIMINADO == false && k.FECHA < fDesde.Value)
                    .Select(k => new { k.TIPO_MOVIMIENTO, k.CANTIDAD })
                    .ToListAsync(ct);

                // 👇 Nuevo mapeo de signo por tipo
                saldoInicial = prev.Sum(m =>
                    (m.TIPO_MOVIMIENTO == "SALIDA" || m.TIPO_MOVIMIENTO == "AJUSTE SALIDA") ? -m.CANTIDAD :
                    (m.TIPO_MOVIMIENTO == "ENTRADA" || m.TIPO_MOVIMIENTO == "AJUSTE ENTRADA") ? +m.CANTIDAD :
                    0
                );
            }

            // 4) Movimientos del rango
            var rangoQuery = _db.KARDEX.Where(k => k.PRODUCTO_ID == id && k.ELIMINADO == false);
            if (fDesde.HasValue) rangoQuery = rangoQuery.Where(k => k.FECHA >= fDesde.Value);
            if (fHasta.HasValue) rangoQuery = rangoQuery.Where(k => k.FECHA <= fHasta.Value);

            var movs = await rangoQuery
                .OrderBy(k => k.FECHA).ThenBy(k => k.KARDEX_ID)
                .Select(k => new { k.KARDEX_ID, k.FECHA, k.TIPO_MOVIMIENTO, k.CANTIDAD, k.COSTO_UNITARIO, k.REFERENCIA })
                .ToListAsync(ct);

            // 5) Totales + saldo acumulado
            int totalE = 0, totalS = 0, totalA = 0;
            int saldoAcum = saldoInicial;
            var filas = new List<KardexMovimientoVM>(movs.Count);

            foreach (var m in movs)
            {
                // 👇 Totales por tipo
                if (m.TIPO_MOVIMIENTO == "ENTRADA" || m.TIPO_MOVIMIENTO == "AJUSTE ENTRADA")
                    totalE += m.CANTIDAD;
                else if (m.TIPO_MOVIMIENTO == "SALIDA" || m.TIPO_MOVIMIENTO == "AJUSTE SALIDA")
                    totalS += m.CANTIDAD;
                else if (m.TIPO_MOVIMIENTO == "AJUSTE PRECIO")
                    totalA += 0; // opcional: agregar contador específico

                // 👇 Saldo acumulado
                int delta =
                    (m.TIPO_MOVIMIENTO == "SALIDA" || m.TIPO_MOVIMIENTO == "AJUSTE SALIDA") ? -m.CANTIDAD :
                    (m.TIPO_MOVIMIENTO == "ENTRADA" || m.TIPO_MOVIMIENTO == "AJUSTE ENTRADA") ? +m.CANTIDAD :
                    0;

                saldoAcum += delta;

                filas.Add(new KardexMovimientoVM
                {
                    KARDEX_ID = m.KARDEX_ID,
                    FECHA = m.FECHA,
                    TIPO_MOVIMIENTO = m.TIPO_MOVIMIENTO,
                    CANTIDAD = m.CANTIDAD,
                    COSTO_UNITARIO = m.COSTO_UNITARIO,
                    REFERENCIA = m.REFERENCIA,
                    SALDO_ACUMULADO = saldoAcum
                });
            }

            return new KardexProductoVM
            {
                PRODUCTO_ID = prod.PRODUCTO_ID,
                ProductoNombre = prod.PRODUCTO_NOMBRE,
                ImagenUrl = prod.IMAGEN_PRODUCTO,
                INVENTARIO_ID = invId,
                Filtros = new KardexProductoFilterVM { PRODUCTO_ID = id, Desde = desde, Hasta = hasta },
                SaldoInicial = saldoInicial,
                Movimientos = filas,
                TotalEntradas = totalE,
                TotalSalidas = totalS,
                TotalAjustes = totalA
            };
        }

        // ============================================================
        // BLOQUE 2: Acción de exportación CSV
        // RUTA: GET /Kardex/ProductoCsv/{id}?desde=yyyy-MM-dd&hasta=yyyy-MM-dd
        // Descripción:
        //   - Exporta el Kardex filtrado a CSV (UTF-8 con BOM) para abrir en Excel.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ProductoCsv(string id, DateTime? desde, DateTime? hasta, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // Reusar la misma construcción del VM (evita divergencias)
            var vm = await BuildKardexProductoVmAsync(id, desde, hasta, ct);

            // Armar CSV (separador coma; si prefieres ; cámbialo)
            // Encabezados:
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ProductoID,ProductoNombre,Desde,Hasta,SaldoInicial,TotalEntradas,TotalSalidas,TotalAjustes,SaldoFinal");
            sb.AppendLine(string.Join(",",
                EscapeCsv(vm.PRODUCTO_ID),
                EscapeCsv(vm.ProductoNombre),
                EscapeCsv(vm.Filtros.Desde?.ToString("yyyy-MM-dd") ?? ""),
                EscapeCsv(vm.Filtros.Hasta?.ToString("yyyy-MM-dd") ?? ""),
                vm.SaldoInicial.ToString(),
                vm.TotalEntradas.ToString(),
                vm.TotalSalidas.ToString(),
                vm.TotalAjustes.ToString(),
                vm.SaldoFinal.ToString()
            ));

            sb.AppendLine(); // línea en blanco
            sb.AppendLine("KardexID,Fecha,TipoMovimiento,Cantidad,CostoUnitario,Referencia,SaldoAcumulado");

            foreach (var m in vm.Movimientos)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(m.KARDEX_ID),
                    EscapeCsv(m.FECHA.ToString("yyyy-MM-dd HH:mm:ss")),
                    EscapeCsv(m.TIPO_MOVIMIENTO),
                    m.CANTIDAD.ToString(),
                    (m.COSTO_UNITARIO?.ToString("0.##") ?? ""), // no forzamos 2 decimales
                    EscapeCsv(m.REFERENCIA ?? ""),
                    m.SALDO_ACUMULADO.ToString()
                ));
            }

            // Incluir BOM UTF-8 para compatibilidad con Excel
            var utf8WithBom = new System.Text.UTF8Encoding(true);
            var bytes = utf8WithBom.GetBytes(sb.ToString());

            var fileName = $"Kardex_{vm.PRODUCTO_ID}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);

            // --------- función local para comillas/escapes ----------
            static string EscapeCsv(string? s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                // Si contiene comas, comillas o saltos de línea, encerramos en comillas y duplicamos comillas internas.
                bool mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
                if (mustQuote) return $"\"{s.Replace("\"", "\"\"")}\"";
                return s;
            }
        }

        // ============================================================
        // GET: /Kardex
        // Lista general del KARDEX con filtros y paginación (EF Core).
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] KardexIndexVM vm, CancellationToken ct)
        {
            // 1) Base: KARDEX + PRODUCTO (solo no eliminados)
            var q = from k in _db.KARDEX
                    join p in _db.PRODUCTO on k.PRODUCTO_ID equals p.PRODUCTO_ID
                    where k.ELIMINADO == false
                    select new { k, p };

            // 2) Filtros
            if (!string.IsNullOrWhiteSpace(vm.SearchProducto))
            {
                var s = vm.SearchProducto.Trim();
                q = q.Where(x => EF.Functions.Like(x.p.PRODUCTO_NOMBRE, $"%{s}%")
                              || EF.Functions.Like(x.k.PRODUCTO_ID, $"%{s}%"));
            }

            if (!string.IsNullOrWhiteSpace(vm.TipoMovimiento))
                q = q.Where(x => x.k.TIPO_MOVIMIENTO == vm.TipoMovimiento);

            if (vm.Desde.HasValue)
            {
                var ini = vm.Desde.Value.Date;
                q = q.Where(x => x.k.FECHA >= ini);
            }
            if (vm.Hasta.HasValue)
            {
                var fin = vm.Hasta.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(x => x.k.FECHA <= fin);
            }

            if (!string.IsNullOrWhiteSpace(vm.Referencia))
            {
                var r = vm.Referencia.Trim();
                q = q.Where(x => x.k.REFERENCIA != null && EF.Functions.Like(x.k.REFERENCIA, $"%{r}%"));
            }

            // 3) Total para paginar
            vm.TotalRows = await q.CountAsync(ct);
            vm.Page = vm.Page <= 0 ? 1 : vm.Page;
            vm.PageSize = vm.PageSize <= 0 ? 20 : vm.PageSize;

            // 4) Orden dinámico (igual patrón que Productos/Inventario)
            bool asc = string.Equals(vm.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            string sort = (vm.Sort ?? "fecha").ToLower();

            q = sort switch
            {
                "producto" => asc ? q.OrderBy(x => x.p.PRODUCTO_NOMBRE).ThenBy(x => x.k.FECHA)
                                  : q.OrderByDescending(x => x.p.PRODUCTO_NOMBRE).ThenByDescending(x => x.k.FECHA),

                "tipo" => asc ? q.OrderBy(x => x.k.TIPO_MOVIMIENTO).ThenBy(x => x.k.FECHA)
                                  : q.OrderByDescending(x => x.k.TIPO_MOVIMIENTO).ThenByDescending(x => x.k.FECHA),

                "cantidad" => asc ? q.OrderBy(x => x.k.CANTIDAD).ThenBy(x => x.k.FECHA)
                                  : q.OrderByDescending(x => x.k.CANTIDAD).ThenByDescending(x => x.k.FECHA),

                "costo" => asc ? q.OrderBy(x => x.k.COSTO_UNITARIO).ThenBy(x => x.k.FECHA)
                                  : q.OrderByDescending(x => x.k.COSTO_UNITARIO).ThenByDescending(x => x.k.FECHA),

                "ref" => asc ? q.OrderBy(x => x.k.REFERENCIA).ThenBy(x => x.k.FECHA)
                                  : q.OrderByDescending(x => x.k.REFERENCIA).ThenByDescending(x => x.k.FECHA),

                "id" => asc ? q.OrderBy(x => x.k.KARDEX_ID)
                                  : q.OrderByDescending(x => x.k.KARDEX_ID),

                _ /* fecha */ => asc ? q.OrderBy(x => x.k.FECHA).ThenBy(x => x.k.KARDEX_ID)
                                     : q.OrderByDescending(x => x.k.FECHA).ThenByDescending(x => x.k.KARDEX_ID),
            };

            // 5) Página solicitada
            vm.Items = await q
                .Skip((vm.Page - 1) * vm.PageSize)
                .Take(vm.PageSize)
                .Select(x => new KardexListItemVM
                {
                    KARDEX_ID = x.k.KARDEX_ID,
                    FECHA = x.k.FECHA,
                    PRODUCTO_ID = x.k.PRODUCTO_ID,
                    ProductoNombre = x.p.PRODUCTO_NOMBRE,
                    TIPO_MOVIMIENTO = x.k.TIPO_MOVIMIENTO,
                    CANTIDAD = x.k.CANTIDAD,
                    COSTO_UNITARIO = x.k.COSTO_UNITARIO,
                    REFERENCIA = x.k.REFERENCIA
                })
                .ToListAsync(ct);

            return View(vm);
        }

    }
}
