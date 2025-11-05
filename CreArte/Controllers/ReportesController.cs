using CreArte.Data;          
using CreArte.ModelsPartial;        
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Rotativa.AspNetCore; 
using Rotativa.AspNetCore.Options;

namespace CreArte.Controllers
{
    public class ReportesController : Controller
    {
        private readonly CreArteDbContext _db; // ← ajusta al nombre real de tu DbContext

        // Inyectamos el DbContext
        public ReportesController(CreArteDbContext db)
        {
            _db = db;
        }

        // =====================================================================
        // GET: /Reportes
        // =====================================================================
        [HttpGet]
        public IActionResult Index() => View();

        // =====================================================================
        // GET: /Reportes/Inventario
        // =====================================================================
        [HttpGet]
        public IActionResult Inventario()
        {
            // 1) Top 12 productos con mayor riesgo (ordenamos por (StockActual-StockMinimo) asc)
            var stockQry = (from inv in _db.INVENTARIO
                            join p in _db.PRODUCTO on inv.PRODUCTO_ID equals p.PRODUCTO_ID
                            orderby (inv.STOCK_ACTUAL - inv.STOCK_MINIMO) ascending, p.PRODUCTO_NOMBRE
                            select new
                            {
                                p.PRODUCTO_NOMBRE,
                                inv.STOCK_ACTUAL,
                                inv.STOCK_MINIMO
                            })
                            .Take(12); // evita gráficos ilegibles si hay muchos

            var labelsProductos = stockQry.Select(x => x.PRODUCTO_NOMBRE).ToList();
            var stockActual = stockQry.Select(x => (decimal)x.STOCK_ACTUAL).ToList();
            var stockMinimo = stockQry.Select(x => (decimal)x.STOCK_MINIMO).ToList();

            // 2) Rotación últimos 6 meses (Entradas vs Salidas en KARDEX)
            //    Definimos ventana temporal: desde el primer día del mes -5
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5);  // incluye 6 meses (mes actual y 5 anteriores)

            // 2.1) Salidas (KARDEX.TIPO_MOVIMIENTO = 'SALIDA')
            var salidasPorMes = _db.KARDEX
                .Where(k => k.FECHA >= desde && (k.TIPO_MOVIMIENTO == "SALIDA"))
                .GroupBy(k => new { k.FECHA.Year, k.FECHA.Month })
                .Select(g => new
                {
                    Anio = g.Key.Year,
                    Mes = g.Key.Month,
                    Cant = g.Sum(x => (decimal)x.CANTIDAD)
                })
                .ToList();

            // 2.2) Entradas (KARDEX.TIPO_MOVIMIENTO = 'ENTRADA')
            var entradasPorMes = _db.KARDEX
                .Where(k => k.FECHA >= desde && (k.TIPO_MOVIMIENTO == "ENTRADA"))
                .GroupBy(k => new { k.FECHA.Year, k.FECHA.Month })
                .Select(g => new
                {
                    Anio = g.Key.Year,
                    Mes = g.Key.Month,
                    Cant = g.Sum(x => (decimal)x.CANTIDAD)
                })
                .ToList();

            // 2.3) Eje X: lista ordenada de 6 meses (ej. Ene, Feb, …)
            var meses = Enumerable.Range(0, 6)
                .Select(i => desde.AddMonths(i))
                .ToList();

            var labelsMes = meses
                .Select(d => d.ToString("MMM")) // Ene, Feb, Mar…
                .ToList();

            // 2.4) Construimos series alineadas a labelsMes
            List<decimal> serieSalidas = new();
            List<decimal> serieEntradas = new();

            foreach (var d in meses)
            {
                var s = salidasPorMes.FirstOrDefault(x => x.Anio == d.Year && x.Mes == d.Month)?.Cant ?? 0m;
                var e = entradasPorMes.FirstOrDefault(x => x.Anio == d.Year && x.Mes == d.Month)?.Cant ?? 0m;
                serieSalidas.Add(s);
                serieEntradas.Add(e);
            }

            var vm = new InventarioReportVM
            {
                LabelsProductos = labelsProductos,
                StockActual = stockActual,
                StockMinimo = stockMinimo,

                LabelsMes = labelsMes,
                SeriesRotacion = new List<Serie>
                {
                    new Serie{ Label = "Salidas",  Color = "#BF265E", Data = serieSalidas },
                    new Serie{ Label = "Entradas", Color = "#242259", Data = serieEntradas }
                },

                // KPIs
                ProductosBajoMinimo = _db.INVENTARIO.Count(i => i.STOCK_ACTUAL < i.STOCK_MINIMO),
                ProductosEnQuiebre = _db.INVENTARIO.Count(i => i.STOCK_ACTUAL <= 0),
                TotalProductos = _db.INVENTARIO.Select(i => i.PRODUCTO_ID).Distinct().Count()
            };

            return PartialView("_InventarioCharts", vm);
        }

        // =====================================================================
        // GET: /Reportes/Compras  (datos REALES + fix DateOnly)
        // =====================================================================
        [HttpGet]
        public IActionResult Compras()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5); // últimos 6 meses

            // 1) Gasto mensual = SUM(DETALLE_COMPRA.SUBTOTAL) por mes de COMPRA.FECHA_COMPRA (en SQL)
            var gastoMesTmp = (from c in _db.COMPRA
                               join dc in _db.DETALLE_COMPRA on c.COMPRA_ID equals dc.COMPRA_ID
                               where c.FECHA_COMPRA >= desde
                               group dc by new { c.FECHA_COMPRA.Year, c.FECHA_COMPRA.Month } into g
                               select new
                               {
                                   Anio = g.Key.Year,
                                   Mes = g.Key.Month,
                                   Q = g.Sum(x => (decimal)x.SUBTOTAL)
                               })
                               .AsNoTracking()
                               .ToList();

            var meses = Enumerable.Range(0, 6).Select(i => desde.AddMonths(i)).ToList();
            var labelsMes = meses.Select(d => d.ToString("MMM")).ToList();

            List<decimal> gastoPorMes = new();
            foreach (var d in meses)
                gastoPorMes.Add(gastoMesTmp.FirstOrDefault(x => x.Anio == d.Year && x.Mes == d.Month)?.Q ?? 0m);

            // 2) Top Proveedores por MONTO (en SQL)
            var provMonto = (from c in _db.COMPRA
                             join dc in _db.DETALLE_COMPRA on c.COMPRA_ID equals dc.COMPRA_ID
                             where c.FECHA_COMPRA >= desde
                             group dc by c.PROVEEDOR_ID into g
                             select new
                             {
                                 PROVEEDOR_ID = g.Key,
                                 MONTO = g.Sum(x => (decimal)x.SUBTOTAL),
                                 CANT = g.Sum(x => (decimal)x.CANTIDAD)
                             })
                             .OrderByDescending(x => x.MONTO)
                             .Take(5)
                             .AsNoTracking()
                             .ToList();

            // 2b) Top Proveedores por CANTIDAD (en SQL)
            var provCant = (from c in _db.COMPRA
                            join dc in _db.DETALLE_COMPRA on c.COMPRA_ID equals dc.COMPRA_ID
                            where c.FECHA_COMPRA >= desde
                            group dc by c.PROVEEDOR_ID into g
                            select new
                            {
                                PROVEEDOR_ID = g.Key,
                                MONTO = g.Sum(x => (decimal)x.SUBTOTAL),
                                CANT = g.Sum(x => (decimal)x.CANTIDAD)
                            })
                            .OrderByDescending(x => x.CANT)
                            .Take(5)
                            .AsNoTracking()
                            .ToList();

            // 2c) Diccionario de nombres de proveedor (empresa o persona)
            var proveedoresDic = (from pr in _db.PROVEEDOR
                                  join pe in _db.PERSONA on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                                  select new
                                  {
                                      pr.PROVEEDOR_ID,
                                      NOMBRE = pr.EMPRESA != null && pr.EMPRESA != ""
                                          ? pr.EMPRESA
                                          : ((pe.PERSONA_PRIMERNOMBRE ?? "") + " " + (pe.PERSONA_PRIMERAPELLIDO ?? ""))
                                  })
                                  .AsNoTracking()
                                  .ToDictionary(x => x.PROVEEDOR_ID, x => x.NOMBRE);

            var topMonto = provMonto.Select(x => new CategoriaValor
            {
                Categoria = proveedoresDic.TryGetValue(x.PROVEEDOR_ID, out var nom) ? nom : x.PROVEEDOR_ID,
                Valor = x.MONTO
            }).ToList();

            var topCant = provCant.Select(x => new CategoriaValor
            {
                Categoria = proveedoresDic.TryGetValue(x.PROVEEDOR_ID, out var nom) ? nom : x.PROVEEDOR_ID,
                Valor = x.CANT
            }).ToList();

            // 3) Lead time promedio 
            var leadTimesDias = _db.COMPRA
                .Where(c => c.FECHA_COMPRA >= desde && c.FECHA_ENTREGA_COMPRA != null)
                .Select(c => new
                {
                    c.FECHA_COMPRA,                
                    c.FECHA_ENTREGA_COMPRA         
                })
                .AsNoTracking()
                .ToList()                          
                .Select(x =>
                {
                    // Convertimos el DateOnly a DateTime a medianoche
                    var entregaDt = x.FECHA_ENTREGA_COMPRA!.Value.ToDateTime(TimeOnly.MinValue);
                    // Diferencia en días (double)
                    return (entregaDt - x.FECHA_COMPRA).TotalDays;
                })
                .Where(d => d >= 0)                // opcional: descartamos negativos si los hubiera
                .ToList();

            double leadTimeProm = leadTimesDias.Count > 0 ? Math.Round(leadTimesDias.Average(), 1) : 0d;

            var vm = new ComprasReportVM
            {
                LabelsMes = labelsMes,
                GastoPorMes = gastoPorMes,
                TopProveedoresPorMonto = topMonto,
                TopProveedoresPorCantidad = topCant,
                LeadTimePromedioDias = leadTimeProm
            };

            return PartialView("_ComprasCharts", vm);
        }

        // =====================================================================
        // GET: /Reportes/Ventas  (DATOS REALES y columnas correctas)
        // =====================================================================
        [HttpGet]
        public IActionResult Ventas()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5); // últimos 6 meses

            // 1) Ventas totales por mes (VENTA.FECHA, VENTA.TOTAL)
            var ventasMesSql = _db.VENTA
                .Where(v => v.FECHA >= desde)
                .GroupBy(v => new { v.FECHA.Year, v.FECHA.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(x => (decimal)x.TOTAL)
                })
                .AsNoTracking()
                .ToList();

            var meses = Enumerable.Range(0, 6).Select(i => desde.AddMonths(i)).ToList();
            var labelsMes = meses.Select(m => m.ToString("MMM")).ToList();

            List<decimal> dataMes = new();
            foreach (var m in meses)
                dataMes.Add(ventasMesSql.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0m);

            // 2) Top 5 productos más vendidos (cantidad)
            //    Necesitamos filtrar por fecha de la venta, por eso unimos DETALLE_VENTA con VENTA.
            var topProdSql = (from dv in _db.DETALLE_VENTA
                              join v in _db.VENTA on dv.VENTA_ID equals v.VENTA_ID
                              join p in _db.PRODUCTO on dv.PRODUCTO_ID equals p.PRODUCTO_ID
                              where v.FECHA >= desde
                              group dv by new { dv.PRODUCTO_ID, p.PRODUCTO_NOMBRE } into g
                              select new
                              {
                                  g.Key.PRODUCTO_NOMBRE,
                                  Cantidad = g.Sum(x => (decimal)x.CANTIDAD)
                              })
                             .OrderByDescending(x => x.Cantidad)
                             .Take(5)
                             .AsNoTracking()
                             .ToList();

            // 3) Pie: Ventas por CATEGORIA (SUM SUBTOTAL)
            //    DETALLE_VENTA → PRODUCTO → SUBCATEGORIA → CATEGORIA, y filtramos por fecha de VENTA.
            var ventasPorCatSql = (from dv in _db.DETALLE_VENTA
                                   join v in _db.VENTA on dv.VENTA_ID equals v.VENTA_ID
                                   join p in _db.PRODUCTO on dv.PRODUCTO_ID equals p.PRODUCTO_ID
                                   join sc in _db.SUBCATEGORIA on p.SUBCATEGORIA_ID equals sc.SUBCATEGORIA_ID
                                   join c in _db.CATEGORIA on sc.CATEGORIA_ID equals c.CATEGORIA_ID
                                   where v.FECHA >= desde
                                   group dv by new { c.CATEGORIA_NOMBRE } into g
                                   select new
                                   {
                                       Categoria = g.Key.CATEGORIA_NOMBRE,
                                       TotalQ = g.Sum(x => (decimal)(x.CANTIDAD * x.PRECIO_UNITARIO))
                                   })
                                   .OrderByDescending(x => x.TotalQ)
                                   .AsNoTracking()
                                   .ToList();

            var vm = new VentasReportVM
            {
                LabelsPeriodo = labelsMes,
                SeriesPeriodo = new List<Serie>
        {
            new Serie { Label = "Total mensual (Q)", Color = "#BF265E", Data = dataMes }
        },
                TopProductos = topProdSql.Select(x => new CategoriaValor
                {
                    Categoria = x.PRODUCTO_NOMBRE,
                    Valor = x.Cantidad
                }).ToList(),
                // Reusamos MetodosPago para el pie, pero con las categorías (etiqueta/valor)
                MetodosPago = ventasPorCatSql.Select(x => new EtiquetaValor
                {
                    Etiqueta = x.Categoria,
                    Valor = x.TotalQ
                }).ToList(),
                TotalPeriodo = dataMes.Sum(),
                TicketsPeriodo = _db.VENTA.Count(v => v.FECHA >= desde)
            };

            return PartialView("_VentasCharts", vm);
        }

        // =====================================================================
        // GET: /Reportes/Pedidos  (DATOS REALES y columnas correctas)
        // =====================================================================
        [HttpGet]
        public IActionResult Pedidos()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5); // últimos 6 meses

            // 1) Pedidos por estado (nombre legible)
            var estadosDic = _db.ESTADO_PEDIDO
                .AsNoTracking()
                .ToDictionary(x => x.ESTADO_PEDIDO_ID, x => x.ESTADO_PEDIDO_NOMBRE);

            var pedPorEstadoSql = _db.PEDIDO
                .GroupBy(p => p.ESTADO_PEDIDO_ID)
                .Select(g => new
                {
                    EstadoId = g.Key,
                    Total = g.Count()
                })
                .AsNoTracking()
                .ToList();

            var pedPorEstado = pedPorEstadoSql.Select(x => new EtiquetaValor
            {
                Etiqueta = estadosDic.TryGetValue(x.EstadoId, out var nombre) ? nombre : x.EstadoId,
                Valor = x.Total
            }).ToList();

            // 2) Finalizados vs Pendientes por mes (últimos 6)
            var pedidosMesSql = _db.PEDIDO
                .Where(p => p.FECHA_PEDIDO >= desde)
                .GroupBy(p => new { p.FECHA_PEDIDO.Year, p.FECHA_PEDIDO.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Count(),
                    Finalizados = g.Count(p => p.ESTADO_PEDIDO_ID != null) // valor provisional; refinamos abajo
                })
                .AsNoTracking()
                .ToList();

            // Ajuste: usar realmente el nombre 'FINALIZADO'
            // Traemos por separado la cuenta de finalizados por mes usando el diccionario de estados.
            var finalizadosMesSql = _db.PEDIDO
                .Where(p => p.FECHA_PEDIDO >= desde && p.ESTADO_PEDIDO_ID != null)
                .Select(p => new { p.FECHA_PEDIDO.Year, p.FECHA_PEDIDO.Month, p.ESTADO_PEDIDO_ID })
                .AsEnumerable() // hacemos el mapeo a nombre en memoria
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Finalizados = g.Count(x => estadosDic.TryGetValue(x.ESTADO_PEDIDO_ID!, out var nom) && nom == "ENTREGADO")
                })
                .ToList();

            var meses = Enumerable.Range(0, 6).Select(i => desde.AddMonths(i)).ToList();
            var labelsMes = meses.Select(m => m.ToString("MMM")).ToList();

            List<decimal> dataFinalizados = new();
            List<decimal> dataPendientes = new();

            foreach (var m in meses)
            {
                var totalMes = pedidosMesSql.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0;
                var finalMes = finalizadosMesSql.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Finalizados ?? 0;
                var pendMes = totalMes - finalMes;

                dataFinalizados.Add(finalMes);
                dataPendientes.Add(pendMes);
            }

            // 3) Radar: % finalización por tipo de cliente
            var tiposDic = _db.TIPO_CLIENTE
                .AsNoTracking()
                .ToDictionary(x => x.TIPO_CLIENTE_ID, x => x.TIPO_CLIENTE_NOMBRE);

            // Contamos total y finalizados por tipo de cliente (últimos 6 meses)
            var porTipoCliente = (from p in _db.PEDIDO
                                  join c in _db.CLIENTE on p.CLIENTE_ID equals c.CLIENTE_ID
                                  where p.FECHA_PEDIDO >= desde
                                  select new { p.ESTADO_PEDIDO_ID, c.TIPO_CLIENTE_ID })
                                 .AsEnumerable()
                                 .GroupBy(x => x.TIPO_CLIENTE_ID)
                                 .Select(g => new
                                 {
                                     TipoId = g.Key,
                                     Total = g.Count(),
                                     Finalizados = g.Count(x => x.ESTADO_PEDIDO_ID != null
                                         && estadosDic.TryGetValue(x.ESTADO_PEDIDO_ID, out var nom)
                                         && nom == "ENTREGADO")
                                 })
                                 .ToList();

            var radar = porTipoCliente.Select(x => new EtiquetaValor
            {
                Etiqueta = tiposDic.TryGetValue(x.TipoId, out var nom) ? nom : x.TipoId,
                Valor = x.Total > 0 ? Math.Round((decimal)x.Finalizados / x.Total * 100m, 1) : 0m
            }).ToList();

            var vm = new PedidosReportVM
            {
                PedidosPorEstado = pedPorEstado,
                LabelsPeriodo = labelsMes,
                Cumplidos = dataFinalizados, // usamos “Cumplidos” = FINALIZADO
                Atrasados = dataPendientes   // aquí “Pendientes” (no finalizados)
            };

            ViewBag.RadarClientes = radar; // lo consumimos en el partial

            return PartialView("_PedidosCharts", vm);
        }

        // =====================================================================
        // GET: Usuarios
        // =====================================================================
        [HttpGet]
        public IActionResult Usuarios()
        {
            var hoy = DateTime.Today;
            var desdeDateTime = hoy.AddDays(-13).Date;  // 14 días (hoy-13 .. hoy), tipo DateTime

            // 1) Usuarios por ROL
            var rolesDic = _db.ROL
                .AsNoTracking()
                .ToDictionary(r => r.ROL_ID, r => r.ROL_NOMBRE);

            var usuariosPorRol = _db.USUARIO
                .AsNoTracking()
                .GroupBy(u => u.ROL_ID)
                .Select(g => new { RolId = g.Key, Cnt = g.Count() })
                .ToList()
                .Select(x => new CategoriaValor
                {
                    Categoria = rolesDic.TryGetValue(x.RolId, out var nom) ? nom : x.RolId,
                    Valor = x.Cnt
                })
                .OrderByDescending(x => x.Valor)
                .ToList();

            // 2) Actividad diaria (proxy: altas por día usando USUARIO_FECHAREGISTRO = DateTime)
            var altasRaw = _db.USUARIO
                .AsNoTracking()
                .Where(u => u.USUARIO_FECHAREGISTRO >= desdeDateTime)
                .Select(u => u.USUARIO_FECHAREGISTRO)     // IQueryable<DateTime>
                .ToList();                                 // List<DateTime>

            // Construir 14 días continuos (DateTime.Date) y contar por día
            var ventana = Enumerable.Range(0, 14)
                .Select(i => desdeDateTime.AddDays(i).Date)
                .ToList();

            var labelsDia = ventana
                .Select(d => d.ToString("dd MMM"))
                .ToList();

            var loginsPorDia = ventana
                .Select(d => (decimal)altasRaw.Count(x => x.Date == d))
                .ToList();

            // 3) KPIs
            var intentosFallidos = 0; // no hay HISTORIAL_LOGIN
            var usuariosBloqueados = _db.USUARIO.AsNoTracking().Count(u => u.ESTADO == false);

            var vm = new UsuariosReportVM
            {
                UsuariosPorRol = usuariosPorRol,
                LabelsDia = labelsDia,
                LoginsPorDia = loginsPorDia,
                IntentosFallidosPeriodo = intentosFallidos,
                UsuariosBloqueadosPeriodo = usuariosBloqueados
            };

            return PartialView("_UsuariosCharts", vm);
        }

        // =====================================================================
        // GET: Empleados  
        // =====================================================================
        [HttpGet]
        public IActionResult Empleados()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desdeDateTime = primerDiaMesActual.AddMonths(-5);     // 6 meses (DateTime)
            var desde = DateOnly.FromDateTime(desdeDateTime);         // equivalente en DateOnly

            // Diccionarios auxiliares
            var areasDic = _db.AREA.AsNoTracking().ToDictionary(a => a.AREA_ID, a => a.AREA_NOMBRE);
            var puestosDic = _db.PUESTO.AsNoTracking().ToDictionary(p => p.PUESTO_ID, p => p.PUESTO_NOMBRE);

            // Empleados por Área
            var empAreaRaw = (from e in _db.EMPLEADO.AsNoTracking()
                              join p in _db.PUESTO.AsNoTracking() on e.PUESTO_ID equals p.PUESTO_ID
                              group e by p.AREA_ID into g
                              select new { AreaId = g.Key, Cnt = g.Count() })
                             .ToList();

            var empleadosPorArea = empAreaRaw
                .Select(x => new CategoriaValor
                {
                    Categoria = areasDic.TryGetValue(x.AreaId, out var nom) ? nom : x.AreaId,
                    Valor = x.Cnt
                })
                .OrderByDescending(x => x.Valor)
                .ToList();

            // Empleados por Puesto (Top 8)
            var empPuestoRaw = _db.EMPLEADO
                .AsNoTracking()
                .GroupBy(e => e.PUESTO_ID)
                .Select(g => new { PuestoId = g.Key, Cnt = g.Count() })
                .ToList();

            var empleadosPorPuesto = empPuestoRaw
                .Select(x => new CategoriaValor
                {
                    Categoria = puestosDic.TryGetValue(x.PuestoId, out var nom) ? nom : x.PuestoId,
                    Valor = x.Cnt
                })
                .OrderByDescending(x => x.Valor)
                .Take(8)
                .ToList();

            // Asistencias por mes (proxy con ingresos del período)
            // FILTRO: DateOnly vs DateOnly -> SIN problemas de tipos
            var ingresos = _db.EMPLEADO
                .AsNoTracking()
                .Where(e => e.EMPLEADO_FECHAINGRESO >= desde)
                .Select(e => e.EMPLEADO_FECHAINGRESO)   // IQueryable<DateOnly>
                .ToList();                               // List<DateOnly>

            var mesesVentana = Enumerable.Range(0, 6)
                .Select(i => desdeDateTime.AddMonths(i))
                .ToList();

            var labelsMes = mesesVentana.Select(m => m.ToString("MMM")).ToList();

            var asistenciasPorMes = new List<decimal>();
            foreach (var m in mesesVentana)
            {
                int y = m.Year, mon = m.Month;
                int cnt = ingresos.Count(d => d.Year == y && d.Month == mon);
                asistenciasPorMes.Add(cnt);
            }

            // Altas del período = ingresos contados; Bajas = 0 (si no tienes campo de baja)
            int altasPeriodo = ingresos.Count;
            int bajasPeriodo = 0;

            var vm = new EmpleadosReportVM
            {
                EmpleadosPorArea = empleadosPorArea,
                EmpleadosPorPuesto = empleadosPorPuesto,
                LabelsMes = labelsMes,
                AsistenciasPorMes = asistenciasPorMes,
                AltasPeriodo = altasPeriodo,
                BajasPeriodo = bajasPeriodo
            };

            return PartialView("_EmpleadosCharts", vm);
        }

        // =====================================================================
        // GET: Clientes
        // =====================================================================
        [HttpGet]
        public IActionResult Clientes()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desdeDateTime = primerDiaMesActual.AddMonths(-5); // DateTime

            // Segmentación por TIPO_CLIENTE
            var tiposDic = _db.TIPO_CLIENTE.AsNoTracking().ToDictionary(t => t.TIPO_CLIENTE_ID, t => t.TIPO_CLIENTE_NOMBRE);

            var segmentacion = _db.CLIENTE
                .AsNoTracking()
                .GroupBy(c => c.TIPO_CLIENTE_ID)
                .Select(g => new { TipoId = g.Key, Cnt = g.Count() })
                .ToList()
                .Select(x => new EtiquetaValor
                {
                    Etiqueta = tiposDic.TryGetValue(x.TipoId, out var nom) ? nom : x.TipoId,
                    Valor = x.Cnt
                })
                .ToList();

            // Recurrentes por mes (2+ ventas en el mes) — todo con DateTime
            var ventasMesCli = _db.VENTA
                .AsNoTracking()
                .Where(v => v.FECHA >= desdeDateTime)
                .Select(v => new { v.FECHA.Year, v.FECHA.Month, v.CLIENTE_ID, v.VENTA_ID })
                .ToList()
                .GroupBy(v => new { v.Year, v.Month, v.CLIENTE_ID })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.CLIENTE_ID, Cnt = g.Count() })
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Recurrentes = g.Count(x => x.Cnt >= 2) })
                .ToList();

            var mesesVentana = Enumerable.Range(0, 6).Select(i => desdeDateTime.AddMonths(i)).ToList();
            var labelsMes = mesesVentana.Select(m => m.ToString("MMM")).ToList();

            var clientesRecurrentesPorMes = mesesVentana
                .Select(m => (decimal)(ventasMesCli.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Recurrentes ?? 0))
                .ToList();

            // Top clientes por monto (Q)
            var topCliSql = _db.VENTA
                .AsNoTracking()
                .GroupBy(v => v.CLIENTE_ID)
                .Select(g => new { ClienteId = g.Key, TotalQ = g.Sum(x => (decimal)x.TOTAL) })
                .OrderByDescending(x => x.TotalQ)
                .Take(5)
                .ToList();

            var cliNomDic = (from c in _db.CLIENTE.AsNoTracking()
                             join pe in _db.PERSONA.AsNoTracking() on c.CLIENTE_ID equals pe.PERSONA_ID
                             select new
                             {
                                 c.CLIENTE_ID,
                                 NOMBRE = (pe.PERSONA_PRIMERNOMBRE ?? "") + " " + (pe.PERSONA_PRIMERAPELLIDO ?? "")
                             })
                            .ToDictionary(x => x.CLIENTE_ID, x => x.NOMBRE);

            var topClientesPorMonto = topCliSql.Select(x => new CategoriaValor
            {
                Categoria = cliNomDic.TryGetValue(x.ClienteId, out var nom) ? nom : x.ClienteId,
                Valor = x.TotalQ
            }).ToList();

            var vm = new ClientesReportVM
            {
                Segmentacion = segmentacion,
                LabelsMes = labelsMes,
                ClientesRecurrentesPorMes = clientesRecurrentesPorMes,
                TopClientesPorMonto = topClientesPorMonto
            };

            return PartialView("_ClientesCharts", vm);
        }

        // ===============================================================
        // GET /Reportes/Export?modulo=Usuarios|Empleados|Clientes
        // Genera un PDF del reporte seleccionado, reutilizando la misma
        // lógica de armado de ViewModel que ya usamos en los parciales.
        // ===============================================================
        [HttpGet]
        public IActionResult Export(string modulo)
        {
            if (string.IsNullOrWhiteSpace(modulo)) modulo = "Usuarios";
            modulo = modulo.Trim();

            switch (modulo)
            {
                case "Usuarios":
                    {
                        // Reutilizamos la misma lógica que en GET /Reportes/Usuarios
                        var vm = BuildUsuariosVM();
                        return new ViewAsPdf("PdfUsuarios", vm)
                        {
                            FileName = $"Reporte_Usuarios_{DateTime.Now:yyyyMMddHHmm}.pdf",
                            PageSize = Size.A4,
                            PageOrientation = Orientation.Landscape,
                            CustomSwitches = "--enable-javascript --javascript-delay 1200 --no-stop-slow-scripts --viewport-size 1366x768"
                        };
                    }
                case "Empleados":
                    {
                        var vm = BuildEmpleadosVM();
                        return new ViewAsPdf("PdfEmpleados", vm)
                        {
                            FileName = $"Reporte_Empleados_{DateTime.Now:yyyyMMddHHmm}.pdf",
                            PageSize = Size.A4,
                            PageOrientation = Orientation.Landscape,
                            CustomSwitches = "--enable-javascript --javascript-delay 1200 --no-stop-slow-scripts --viewport-size 1366x768"
                        };
                    }
                case "Clientes":
                    {
                        var vm = BuildClientesVM();
                        return new ViewAsPdf("PdfClientes", vm)
                        {
                            FileName = $"Reporte_Clientes_{DateTime.Now:yyyyMMddHHmm}.pdf",
                            PageSize = Size.A4,
                            PageOrientation = Orientation.Landscape,
                            CustomSwitches = "--enable-javascript --javascript-delay 1200 --no-stop-slow-scripts --viewport-size 1366x768"
                        };
                    }
                case "Ventas":
                    {
                        var vm = BuildVentasVM();
                        return new ViewAsPdf("PdfVentas", vm)
                        {
                            FileName = $"Reporte_Ventas_{DateTime.Now:yyyyMMddHHmm}.pdf",
                            PageSize = Size.A4,
                            PageOrientation = Orientation.Landscape,
                            CustomSwitches = "--enable-javascript --javascript-delay 1200 --no-stop-slow-scripts --viewport-size 1366x768"

                        };
                    }
                case "Compras": 
                    { 
                        var vm = BuildComprasVM(); 
                        return new ViewAsPdf("PdfCompras", vm) 
                        { 
                            FileName = $"Reporte_Compras_{DateTime.Now:yyyyMMddHHmm}.pdf",
                            PageSize = Size.A4,
                            PageOrientation = Orientation.Landscape,
                            CustomSwitches = "--enable-javascript --javascript-delay 1200 --no-stop-slow-scripts --viewport-size 1366x768" 
                        }; 
                    }
                case "Inventario":
                    {
                        var vm = BuildInventarioVM(); // ← método que agregamos abajo
                        return new ViewAsPdf("PdfInventario", vm)
                        {
                            FileName = $"Reporte_Inventario_{DateTime.Now:yyyyMMddHHmm}.pdf",
                            PageSize = Size.A4,
                            PageOrientation = Orientation.Landscape,
                            CustomSwitches = "--enable-javascript --javascript-delay 1200 --no-stop-slow-scripts --viewport-size 1366x768"
                        };
                    }
                case "Pedidos":
                    {
                        // 1) Construimos el VM con los mismos datos reales del GET Pedidos()
                        var vm = BuildPedidosVM(); // ← método abajo

                        // 2) Devolvemos el PDF
                        return new ViewAsPdf("PdfPedidos", vm)
                        {
                            FileName = $"Reporte_Pedidos_{DateTime.Now:yyyyMMddHHmm}.pdf",
                            PageSize = Size.A4,
                            PageOrientation = Orientation.Landscape,
                            // Importante para que Chart.js se renderice con wkhtmltopdf
                            CustomSwitches = "--enable-javascript --javascript-delay 1200 --no-stop-slow-scripts --viewport-size 1366x768"
                        };
                    }

                default:
                    return BadRequest("Módulo no soportado para exportación.");
            }
        }

        /* ===============================================================
           MÉTODOS PRIVADOS: Arman los mismos ViewModels que usan los parciales
           ---------------------------------------------------------------*/
        private InventarioReportVM BuildInventarioVM()
        {
            // 1) Top 12 productos en riesgo
            var stockQry = (from inv in _db.INVENTARIO
                            join p in _db.PRODUCTO on inv.PRODUCTO_ID equals p.PRODUCTO_ID
                            orderby (inv.STOCK_ACTUAL - inv.STOCK_MINIMO) ascending, p.PRODUCTO_NOMBRE
                            select new
                            {
                                p.PRODUCTO_NOMBRE,
                                inv.STOCK_ACTUAL,
                                inv.STOCK_MINIMO
                            })
                            .Take(12);

            var labelsProductos = stockQry.Select(x => x.PRODUCTO_NOMBRE).ToList();
            var stockActual = stockQry.Select(x => (decimal)x.STOCK_ACTUAL).ToList();
            var stockMinimo = stockQry.Select(x => (decimal)x.STOCK_MINIMO).ToList();

            // 2) Rotación últimos 6 meses
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5);  // 6 meses (actual + 5 atrás)

            // 2.1) Salidas
            var salidasPorMes = _db.KARDEX
                .Where(k => k.FECHA >= desde && (k.TIPO_MOVIMIENTO == "SALIDA"))
                .GroupBy(k => new { k.FECHA.Year, k.FECHA.Month })
                .Select(g => new
                {
                    Anio = g.Key.Year,
                    Mes = g.Key.Month,
                    Cant = g.Sum(x => (decimal)x.CANTIDAD)
                })
                .ToList();

            // 2.2) Entradas
            var entradasPorMes = _db.KARDEX
                .Where(k => k.FECHA >= desde && (k.TIPO_MOVIMIENTO == "ENTRADA"))
                .GroupBy(k => new { k.FECHA.Year, k.FECHA.Month })
                .Select(g => new
                {
                    Anio = g.Key.Year,
                    Mes = g.Key.Month,
                    Cant = g.Sum(x => (decimal)x.CANTIDAD)
                })
                .ToList();

            // 2.3) Eje X y series alineadas
            var meses = Enumerable.Range(0, 6).Select(i => desde.AddMonths(i)).ToList();
            var labelsMes = meses.Select(d => d.ToString("MMM")).ToList();

            List<decimal> serieSalidas = new();
            List<decimal> serieEntradas = new();

            foreach (var d in meses)
            {
                var s = salidasPorMes.FirstOrDefault(x => x.Anio == d.Year && x.Mes == d.Month)?.Cant ?? 0m;
                var e = entradasPorMes.FirstOrDefault(x => x.Anio == d.Year && x.Mes == d.Month)?.Cant ?? 0m;
                serieSalidas.Add(s);
                serieEntradas.Add(e);
            }

            // 2.4) Series para el chart (usa tu struct Serie)
            var seriesRotacion = new List<Serie>
    {
        new Serie{ Label = "Salidas",  Color = "#BF265E", Data = serieSalidas },
        new Serie{ Label = "Entradas", Color = "#242259", Data = serieEntradas }
    };

            // KPIs
            var productosBajoMinimo = _db.INVENTARIO.Count(i => i.STOCK_ACTUAL < i.STOCK_MINIMO);
            var productosEnQuiebre = _db.INVENTARIO.Count(i => i.STOCK_ACTUAL <= 0);
            var totalProductos = _db.INVENTARIO.Select(i => i.PRODUCTO_ID).Distinct().Count();

            return new InventarioReportVM
            {
                LabelsProductos = labelsProductos,
                StockActual = stockActual,
                StockMinimo = stockMinimo,

                LabelsMes = labelsMes,
                SeriesRotacion = seriesRotacion,

                ProductosBajoMinimo = productosBajoMinimo,
                ProductosEnQuiebre = productosEnQuiebre,
                TotalProductos = totalProductos
            };
        }

        private PedidosReportVM BuildPedidosVM()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5); // últimos 6 meses

            // 1) Pedidos por estado (nombre legible)
            var estadosDic = _db.ESTADO_PEDIDO
                .AsNoTracking()
                .ToDictionary(x => x.ESTADO_PEDIDO_ID, x => x.ESTADO_PEDIDO_NOMBRE);

            var pedPorEstadoSql = _db.PEDIDO
                .GroupBy(p => p.ESTADO_PEDIDO_ID)
                .Select(g => new
                {
                    EstadoId = g.Key,
                    Total = g.Count()
                })
                .AsNoTracking()
                .ToList();

            var pedPorEstado = pedPorEstadoSql.Select(x => new EtiquetaValor
            {
                Etiqueta = estadosDic.TryGetValue(x.EstadoId, out var nombre) ? nombre : x.EstadoId,
                Valor = x.Total
            }).ToList();

            // 2) Finalizados vs Pendientes por mes (últimos 6)
            var pedidosMesSql = _db.PEDIDO
                .Where(p => p.FECHA_PEDIDO >= desde)
                .GroupBy(p => new { p.FECHA_PEDIDO.Year, p.FECHA_PEDIDO.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Count(),
                    Finalizados = g.Count(p => p.ESTADO_PEDIDO_ID != null) // provisional; refinamos abajo
                })
                .AsNoTracking()
                .ToList();

            // Ajuste: usar realmente el nombre 'ENTREGADO' (cámbialo si en tu DB se llama distinto)
            var finalizadosMesSql = _db.PEDIDO
                .Where(p => p.FECHA_PEDIDO >= desde && p.ESTADO_PEDIDO_ID != null)
                .Select(p => new { p.FECHA_PEDIDO.Year, p.FECHA_PEDIDO.Month, p.ESTADO_PEDIDO_ID })
                .AsEnumerable() // mapeo a nombre en memoria
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Finalizados = g.Count(x => estadosDic.TryGetValue(x.ESTADO_PEDIDO_ID!, out var nom) && nom == "ENTREGADO")
                })
                .ToList();

            var meses = Enumerable.Range(0, 6).Select(i => desde.AddMonths(i)).ToList();
            var labelsMes = meses.Select(m => m.ToString("MMM")).ToList();

            List<decimal> dataFinalizados = new();
            List<decimal> dataPendientes = new();

            foreach (var m in meses)
            {
                var totalMes = pedidosMesSql.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0;
                var finalMes = finalizadosMesSql.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Finalizados ?? 0;
                var pendMes = totalMes - finalMes;

                dataFinalizados.Add(finalMes);
                dataPendientes.Add(pendMes);
            }

            // 3) Radar: % finalización por tipo de cliente (últimos 6 meses)
            var tiposDic = _db.TIPO_CLIENTE
                .AsNoTracking()
                .ToDictionary(x => x.TIPO_CLIENTE_ID, x => x.TIPO_CLIENTE_NOMBRE);

            var porTipoCliente = (from p in _db.PEDIDO
                                  join c in _db.CLIENTE on p.CLIENTE_ID equals c.CLIENTE_ID
                                  where p.FECHA_PEDIDO >= desde
                                  select new { p.ESTADO_PEDIDO_ID, c.TIPO_CLIENTE_ID })
                                 .AsEnumerable()
                                 .GroupBy(x => x.TIPO_CLIENTE_ID)
                                 .Select(g => new
                                 {
                                     TipoId = g.Key,
                                     Total = g.Count(),
                                     Finalizados = g.Count(x => x.ESTADO_PEDIDO_ID != null
                                         && estadosDic.TryGetValue(x.ESTADO_PEDIDO_ID, out var nom)
                                         && nom == "ENTREGADO")   // ← cambia si tu estado final se llama distinto
                                 })
                                 .ToList();

            var radar = porTipoCliente.Select(x => new EtiquetaValor
            {
                Etiqueta = tiposDic.TryGetValue(x.TipoId, out var nom) ? nom : x.TipoId,
                Valor = x.Total > 0 ? Math.Round((decimal)x.Finalizados / x.Total * 100m, 1) : 0m
            }).ToList();

            // Pasamos el radar al PDF via ViewBag (sin tocar tu VM)
            ViewBag.RadarClientes = radar;

            // VM final (tus propiedades reales)
            var vm = new PedidosReportVM
            {
                PedidosPorEstado = pedPorEstado,
                LabelsPeriodo = labelsMes,
                Cumplidos = dataFinalizados,
                Atrasados = dataPendientes
            };

            return vm;
        }

        private UsuariosReportVM BuildUsuariosVM()
        {
            var hoy = DateTime.Today;
            var desdeDateTime = hoy.AddDays(-13).Date;

            var rolesDic = _db.ROL.AsNoTracking().ToDictionary(r => r.ROL_ID, r => r.ROL_NOMBRE);

            var usuariosPorRol = _db.USUARIO.AsNoTracking()
                .GroupBy(u => u.ROL_ID)
                .Select(g => new { RolId = g.Key, Cnt = g.Count() })
                .ToList()
                .Select(x => new CategoriaValor
                {
                    Categoria = rolesDic.TryGetValue(x.RolId, out var nom) ? nom : x.RolId,
                    Valor = x.Cnt
                })
                .OrderByDescending(x => x.Valor).ToList();

            var altasRaw = _db.USUARIO.AsNoTracking()
                .Where(u => u.USUARIO_FECHAREGISTRO >= desdeDateTime)
                .Select(u => u.USUARIO_FECHAREGISTRO)
                .ToList();

            var ventana = Enumerable.Range(0, 14).Select(i => desdeDateTime.AddDays(i).Date).ToList();
            var labelsDia = ventana.Select(d => d.ToString("dd MMM")).ToList();
            var loginsPorDia = ventana.Select(d => (decimal)altasRaw.Count(x => x.Date == d)).ToList();

            var vm = new UsuariosReportVM
            {
                UsuariosPorRol = usuariosPorRol,
                LabelsDia = labelsDia,
                LoginsPorDia = loginsPorDia,
                IntentosFallidosPeriodo = 0,
                UsuariosBloqueadosPeriodo = _db.USUARIO.AsNoTracking().Count(u => u.ESTADO == false)
            };
            return vm;
        }

        private EmpleadosReportVM BuildEmpleadosVM()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desdeDateTime = primerDiaMesActual.AddMonths(-5);
            var desde = DateOnly.FromDateTime(desdeDateTime);

            var areasDic = _db.AREA.AsNoTracking().ToDictionary(a => a.AREA_ID, a => a.AREA_NOMBRE);
            var puestosDic = _db.PUESTO.AsNoTracking().ToDictionary(p => p.PUESTO_ID, p => p.PUESTO_NOMBRE);

            var empAreaRaw = (from e in _db.EMPLEADO.AsNoTracking()
                              join p in _db.PUESTO.AsNoTracking() on e.PUESTO_ID equals p.PUESTO_ID
                              group e by p.AREA_ID into g
                              select new { AreaId = g.Key, Cnt = g.Count() }).ToList();

            var empleadosPorArea = empAreaRaw.Select(x => new CategoriaValor
            {
                Categoria = areasDic.TryGetValue(x.AreaId, out var nom) ? nom : x.AreaId,
                Valor = x.Cnt
            }).OrderByDescending(x => x.Valor).ToList();

            var empPuestoRaw = _db.EMPLEADO.AsNoTracking()
                .GroupBy(e => e.PUESTO_ID)
                .Select(g => new { PuestoId = g.Key, Cnt = g.Count() }).ToList();

            var empleadosPorPuesto = empPuestoRaw.Select(x => new CategoriaValor
            {
                Categoria = puestosDic.TryGetValue(x.PuestoId, out var nom) ? nom : x.PuestoId,
                Valor = x.Cnt
            }).OrderByDescending(x => x.Valor).Take(8).ToList();

            var ingresos = _db.EMPLEADO.AsNoTracking()
                .Where(e => e.EMPLEADO_FECHAINGRESO >= desde)
                .Select(e => e.EMPLEADO_FECHAINGRESO)
                .ToList(); // List<DateOnly>

            var mesesVentana = Enumerable.Range(0, 6).Select(i => desdeDateTime.AddMonths(i)).ToList();
            var labelsMes = mesesVentana.Select(m => m.ToString("MMM")).ToList();
            var asistenciasPorMes = mesesVentana.Select(m =>
                (decimal)ingresos.Count(d => d.Year == m.Year && d.Month == m.Month)).ToList();

            var vm = new EmpleadosReportVM
            {
                EmpleadosPorArea = empleadosPorArea,
                EmpleadosPorPuesto = empleadosPorPuesto,
                LabelsMes = labelsMes,
                AsistenciasPorMes = asistenciasPorMes,
                AltasPeriodo = ingresos.Count,
                BajasPeriodo = 0
            };
            return vm;
        }

        private ClientesReportVM BuildClientesVM()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desdeDateTime = primerDiaMesActual.AddMonths(-5);

            var tiposDic = _db.TIPO_CLIENTE.AsNoTracking().ToDictionary(t => t.TIPO_CLIENTE_ID, t => t.TIPO_CLIENTE_NOMBRE);

            var segmentacion = _db.CLIENTE.AsNoTracking()
                .GroupBy(c => c.TIPO_CLIENTE_ID)
                .Select(g => new { TipoId = g.Key, Cnt = g.Count() })
                .ToList()
                .Select(x => new EtiquetaValor
                {
                    Etiqueta = tiposDic.TryGetValue(x.TipoId, out var nom) ? nom : x.TipoId,
                    Valor = x.Cnt
                }).ToList();

            var ventasMesCli = _db.VENTA.AsNoTracking()
                .Where(v => v.FECHA >= desdeDateTime)
                .Select(v => new { v.FECHA.Year, v.FECHA.Month, v.CLIENTE_ID, v.VENTA_ID })
                .ToList()
                .GroupBy(v => new { v.Year, v.Month, v.CLIENTE_ID })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.CLIENTE_ID, Cnt = g.Count() })
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Recurrentes = g.Count(x => x.Cnt >= 2) })
                .ToList();

            var mesesVentana = Enumerable.Range(0, 6).Select(i => desdeDateTime.AddMonths(i)).ToList();
            var labelsMes = mesesVentana.Select(m => m.ToString("MMM")).ToList();
            var clientesRecurrentesPorMes = mesesVentana.Select(m =>
                (decimal)(ventasMesCli.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Recurrentes ?? 0)).ToList();

            var topCliSql = _db.VENTA.AsNoTracking()
                .GroupBy(v => v.CLIENTE_ID)
                .Select(g => new { ClienteId = g.Key, TotalQ = g.Sum(x => (decimal)x.TOTAL) })
                .OrderByDescending(x => x.TotalQ).Take(5).ToList();

            var cliNomDic = (from c in _db.CLIENTE.AsNoTracking()
                             join pe in _db.PERSONA.AsNoTracking() on c.CLIENTE_ID equals pe.PERSONA_ID
                             select new { c.CLIENTE_ID, NOMBRE = (pe.PERSONA_PRIMERNOMBRE ?? "") + " " + (pe.PERSONA_PRIMERAPELLIDO ?? "") })
                            .ToDictionary(x => x.CLIENTE_ID, x => x.NOMBRE);

            var topClientesPorMonto = topCliSql.Select(x => new CategoriaValor
            {
                Categoria = cliNomDic.TryGetValue(x.ClienteId, out var nom) ? nom : x.ClienteId,
                Valor = x.TotalQ
            }).ToList();

            var vm = new ClientesReportVM
            {
                Segmentacion = segmentacion,
                LabelsMes = labelsMes,
                ClientesRecurrentesPorMes = clientesRecurrentesPorMes,
                TopClientesPorMonto = topClientesPorMonto
            };
            return vm;
        }

        private VentasReportVM BuildVentasVM()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5); // últimos 6 meses

            // 1) Ventas totales por mes (VENTA.FECHA, VENTA.TOTAL)
            var ventasMesSql = _db.VENTA
                .Where(v => v.FECHA >= desde)
                .GroupBy(v => new { v.FECHA.Year, v.FECHA.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(x => (decimal)x.TOTAL) // ← usa tu columna real TOTAL
                })
                .AsNoTracking()
                .ToList();

            var meses = Enumerable.Range(0, 6).Select(i => desde.AddMonths(i)).ToList();
            var labelsMes = meses.Select(m => m.ToString("MMM")).ToList();

            List<decimal> dataMes = new();
            foreach (var m in meses)
                dataMes.Add(ventasMesSql.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0m);

            // 2) Top 5 productos más vendidos (cantidad) en el período
            var topProdSql = (from dv in _db.DETALLE_VENTA
                              join v in _db.VENTA on dv.VENTA_ID equals v.VENTA_ID
                              join p in _db.PRODUCTO on dv.PRODUCTO_ID equals p.PRODUCTO_ID
                              where v.FECHA >= desde
                              group dv by new { dv.PRODUCTO_ID, p.PRODUCTO_NOMBRE } into g
                              select new
                              {
                                  g.Key.PRODUCTO_NOMBRE,
                                  Cantidad = g.Sum(x => (decimal)x.CANTIDAD)
                              })
                             .OrderByDescending(x => x.Cantidad)
                             .Take(5)
                             .AsNoTracking()
                             .ToList();

            // 3) "MetodosPago" lo reutilizamos como Pie por CATEGORÍA (Q)
            var ventasPorCatSql = (from dv in _db.DETALLE_VENTA
                                   join v in _db.VENTA on dv.VENTA_ID equals v.VENTA_ID
                                   join p in _db.PRODUCTO on dv.PRODUCTO_ID equals p.PRODUCTO_ID
                                   join sc in _db.SUBCATEGORIA on p.SUBCATEGORIA_ID equals sc.SUBCATEGORIA_ID
                                   join c in _db.CATEGORIA on sc.CATEGORIA_ID equals c.CATEGORIA_ID
                                   where v.FECHA >= desde
                                   group dv by new { c.CATEGORIA_NOMBRE } into g
                                   select new
                                   {
                                       Categoria = g.Key.CATEGORIA_NOMBRE,
                                       TotalQ = g.Sum(x => (decimal)(x.CANTIDAD * x.PRECIO_UNITARIO))
                                   })
                                  .OrderByDescending(x => x.TotalQ)
                                  .AsNoTracking()
                                  .ToList();

            var vm = new VentasReportVM
            {
                LabelsPeriodo = labelsMes, // ← tu VM real
                SeriesPeriodo = new List<Serie>
        {
            new Serie { Label = "Total mensual (Q)", Color = "#BF265E", Data = dataMes }
        },
                TopProductos = topProdSql.Select(x => new CategoriaValor
                {
                    Categoria = x.PRODUCTO_NOMBRE,
                    Valor = x.Cantidad
                }).ToList(),
                MetodosPago = ventasPorCatSql.Select(x => new EtiquetaValor
                {
                    Etiqueta = x.Categoria,
                    Valor = x.TotalQ
                }).ToList(),
                TotalPeriodo = dataMes.Sum(),
                TicketsPeriodo = _db.VENTA.Count(v => v.FECHA >= desde)
            };

            return vm;
        }

        private ComprasReportVM BuildComprasVM()
        {
            var hoy = DateTime.Today;
            var primerDiaMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var desde = primerDiaMesActual.AddMonths(-5); // últimos 6 meses

            // 1) Gasto mensual = SUM(DETALLE_COMPRA.SUBTOTAL) por mes de FECHA_COMPRA
            var gastoMesTmp = (from c in _db.COMPRA
                               join dc in _db.DETALLE_COMPRA on c.COMPRA_ID equals dc.COMPRA_ID
                               where c.FECHA_COMPRA >= desde
                               group dc by new { c.FECHA_COMPRA.Year, c.FECHA_COMPRA.Month } into g
                               select new
                               {
                                   Anio = g.Key.Year,
                                   Mes = g.Key.Month,
                                   Q = g.Sum(x => (decimal)x.SUBTOTAL)
                               })
                              .AsNoTracking()
                              .ToList();

            var meses = Enumerable.Range(0, 6).Select(i => desde.AddMonths(i)).ToList();
            var labelsMes = meses.Select(d => d.ToString("MMM")).ToList();

            List<decimal> gastoPorMes = new();
            foreach (var d in meses)
                gastoPorMes.Add(gastoMesTmp.FirstOrDefault(x => x.Anio == d.Year && x.Mes == d.Month)?.Q ?? 0m);

            // 2) Top Proveedores por MONTO
            var provMonto = (from c in _db.COMPRA
                             join dc in _db.DETALLE_COMPRA on c.COMPRA_ID equals dc.COMPRA_ID
                             where c.FECHA_COMPRA >= desde
                             group dc by c.PROVEEDOR_ID into g
                             select new
                             {
                                 PROVEEDOR_ID = g.Key,
                                 MONTO = g.Sum(x => (decimal)x.SUBTOTAL),
                                 CANT = g.Sum(x => (decimal)x.CANTIDAD)
                             })
                            .OrderByDescending(x => x.MONTO)
                            .Take(5)
                            .AsNoTracking()
                            .ToList();

            // 2b) Top Proveedores por CANTIDAD
            var provCant = (from c in _db.COMPRA
                            join dc in _db.DETALLE_COMPRA on c.COMPRA_ID equals dc.COMPRA_ID
                            where c.FECHA_COMPRA >= desde
                            group dc by c.PROVEEDOR_ID into g
                            select new
                            {
                                PROVEEDOR_ID = g.Key,
                                MONTO = g.Sum(x => (decimal)x.SUBTOTAL),
                                CANT = g.Sum(x => (decimal)x.CANTIDAD)
                            })
                           .OrderByDescending(x => x.CANT)
                           .Take(5)
                           .AsNoTracking()
                           .ToList();

            // 2c) Diccionario de nombres de proveedor (empresa o persona)
            var proveedoresDic = (from pr in _db.PROVEEDOR
                                  join pe in _db.PERSONA on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                                  select new
                                  {
                                      pr.PROVEEDOR_ID,
                                      NOMBRE = pr.EMPRESA != null && pr.EMPRESA != ""
                                          ? pr.EMPRESA
                                          : ((pe.PERSONA_PRIMERNOMBRE ?? "") + " " + (pe.PERSONA_PRIMERAPELLIDO ?? ""))
                                  })
                                 .AsNoTracking()
                                 .ToDictionary(x => x.PROVEEDOR_ID, x => x.NOMBRE);

            var topMonto = provMonto.Select(x => new CategoriaValor
            {
                Categoria = proveedoresDic.TryGetValue(x.PROVEEDOR_ID, out var nom) ? nom : x.PROVEEDOR_ID,
                Valor = x.MONTO
            }).ToList();

            var topCant = provCant.Select(x => new CategoriaValor
            {
                Categoria = proveedoresDic.TryGetValue(x.PROVEEDOR_ID, out var nom) ? nom : x.PROVEEDOR_ID,
                Valor = x.CANT
            }).ToList();

            // 3) Lead time promedio (días) = FECHA_ENTREGA_COMPRA (DateOnly?) - FECHA_COMPRA (DateTime)
            var leadTimesDias = _db.COMPRA
                .Where(c => c.FECHA_COMPRA >= desde && c.FECHA_ENTREGA_COMPRA != null)
                .Select(c => new { c.FECHA_COMPRA, c.FECHA_ENTREGA_COMPRA })
                .AsNoTracking()
                .ToList()
                .Select(x =>
                {
                    // Convertimos DateOnly? a DateTime (00:00)
                    var entregaDt = x.FECHA_ENTREGA_COMPRA!.Value.ToDateTime(TimeOnly.MinValue);
                    return (entregaDt - x.FECHA_COMPRA).TotalDays;
                })
                .Where(d => d >= 0)
                .ToList();

            double leadTimeProm = leadTimesDias.Count > 0 ? Math.Round(leadTimesDias.Average(), 1) : 0d;

            // 🔚 ViewModel final (tus propiedades reales)
            var vm = new ComprasReportVM
            {
                LabelsMes = labelsMes,
                GastoPorMes = gastoPorMes,
                TopProveedoresPorMonto = topMonto,
                TopProveedoresPorCantidad = topCant,
                LeadTimePromedioDias = leadTimeProm
            };

            return vm;
        }
    }
}
