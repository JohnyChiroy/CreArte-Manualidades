using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Bitacora;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CreArte.Controllers
{
    public class VentasController : Controller
    {
        private readonly CreArteDbContext _db;
        private readonly IBitacoraService _bitacora;

        public VentasController(CreArteDbContext db, IBitacoraService bitacora)
        {
            _db = db;
            _bitacora = bitacora;
        }

        // ===============================
        // Helpers comunes (mismo patrón que Pedidos)
        // ===============================
        private string UserName() => User?.Identity?.Name ?? "sistema";
        private static int RoundToInt(decimal value) => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

        // VE00000001 secuencial (ajusta ancho según tu columna en VENTA)
        private async Task<string> SiguienteVentaIdAsync()
        {
            const string prefijo = "VE";
            const int ancho = 8;

            var ids = await _db.VENTA.Select(v => v.VENTA_ID).Where(id => id.StartsWith(prefijo)).ToListAsync();
            int maxNum = 0;
            var rx = new System.Text.RegularExpressions.Regex(@"^" + prefijo + @"(?<n>\d+)$");
            foreach (var id in ids)
            {
                var m = rx.Match(id ?? "");
                if (m.Success && int.TryParse(m.Groups["n"].Value, out int n) && n > maxNum) maxNum = n;
            }
            return prefijo + (maxNum + 1).ToString(new string('0', ancho));
        }

        private static string NewDetalleVentaId() => Guid.NewGuid().ToString("N").Substring(0, 10);
        private static string NewKardexId() => Guid.NewGuid().ToString("N").Substring(0, 10);

        private async Task<string?> GetSesionCajaActivaAsync(string usuarioId)
        {
            return await _db.CAJA_SESION
                .Where(c => c.USUARIO_APERTURA_ID == usuarioId && c.ESTADO == true)
                .OrderByDescending(c => c.FECHA_APERTURA)
                .Select(c => c.SESION_ID)
                .FirstOrDefaultAsync();
        }

        // MC00000001 secuencial (ajusta ancho/longitud a tu columna en MOVIMIENTO_CAJA)
        private async Task<string> SiguienteMovimientoCajaIdAsync()
        {
            const string prefijo = "MC";
            const int ancho = 8;
            var ids = await _db.MOVIMIENTO_CAJA.Select(x => x.MOVIMIENTO_ID).Where(id => id.StartsWith(prefijo)).ToListAsync();
            int maxNum = 0;
            var rx = new System.Text.RegularExpressions.Regex(@"^" + prefijo + @"(?<n>\d+)$");
            foreach (var id in ids)
            {
                var m = rx.Match(id ?? "");
                if (m.Success && int.TryParse(m.Groups["n"].Value, out int n) && n > maxNum) maxNum = n;
            }
            return prefijo + (maxNum + 1).ToString(new string('0', ancho));
        }

        // ===========================================================
        // GET: /Ventas
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] VentaIndexVM vm)
        {
            var q =
                from v in _db.VENTA.AsNoTracking()
                join c in _db.CLIENTE on v.CLIENTE_ID equals c.CLIENTE_ID
                join p in _db.PERSONA on c.CLIENTE_ID equals p.PERSONA_ID
                join u in _db.USUARIO on v.USUARIO_ID equals u.USUARIO_ID
                select new VentaIndexItemVM
                {
                    VentaId = v.VENTA_ID,
                    Fecha = v.FECHA,
                    ClienteNombre = (
                        (p.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (p.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (p.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (p.PERSONA_SEGUNDOAPELLIDO ?? "")
                    ).Trim(),
                    UsuarioNombre = u.USUARIO_NOMBRE ?? u.USUARIO_ID,
                    Total = v.TOTAL,
                    Estado = v.ESTADO
                };

            // --- Filtros ---
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim();
                q = q.Where(x =>
                    x.VentaId.Contains(s) ||
                    x.ClienteNombre.Contains(s) ||
                    x.UsuarioNombre.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(vm.Cliente))
                q = q.Where(x => x.ClienteNombre.Contains(vm.Cliente));

            if (!string.IsNullOrWhiteSpace(vm.Usuario))
                q = q.Where(x => x.UsuarioNombre.Contains(vm.Usuario));

            if (vm.Desde.HasValue)
                q = q.Where(x => x.Fecha >= vm.Desde.Value);

            if (vm.Hasta.HasValue)
            {
                var hasta = vm.Hasta.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(x => x.Fecha <= hasta);
            }

            if (vm.TotalMin.HasValue)
                q = q.Where(x => x.Total >= vm.TotalMin.Value);
            if (vm.TotalMax.HasValue)
                q = q.Where(x => x.Total <= vm.TotalMax.Value);

            // --- Orden ---
            bool asc = string.Equals(vm.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            switch ((vm.Sort ?? "fecha").ToLower())
            {
                case "cliente": q = asc ? q.OrderBy(x => x.ClienteNombre) : q.OrderByDescending(x => x.ClienteNombre); break;
                case "usuario": q = asc ? q.OrderBy(x => x.UsuarioNombre) : q.OrderByDescending(x => x.UsuarioNombre); break;
                case "total": q = asc ? q.OrderBy(x => x.Total) : q.OrderByDescending(x => x.Total); break;
                default: q = asc ? q.OrderBy(x => x.Fecha) : q.OrderByDescending(x => x.Fecha); break;
            }

            // --- Paginación ---
            vm.TotalItems = await q.CountAsync();
            if (vm.PageSize <= 0) vm.PageSize = 10;
            vm.TotalPages = (int)Math.Ceiling((double)vm.TotalItems / vm.PageSize);
            if (vm.Page <= 0) vm.Page = 1;
            if (vm.TotalPages > 0 && vm.Page > vm.TotalPages) vm.Page = vm.TotalPages;

            vm.Items = await q
                .Skip((vm.Page - 1) * vm.PageSize)
                .Take(vm.PageSize)
                .ToListAsync();

            return View(vm);
        }

        // ==========================================
        // GET: /Ventas/Details/{id}
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["err"] = "ID de venta no especificado.";
                return RedirectToAction(nameof(Index));
            }

            var venta = await (from v in _db.VENTA
                               join c in _db.CLIENTE on v.CLIENTE_ID equals c.CLIENTE_ID
                               join p in _db.PERSONA on c.CLIENTE_ID equals p.PERSONA_ID
                               join u in _db.USUARIO on v.USUARIO_ID equals u.USUARIO_ID
                               where v.VENTA_ID == id
                               select new VentaDetailsVM
                               {
                                   VentaId = v.VENTA_ID,
                                   Fecha = v.FECHA,
                                   ClienteNombre = (
                                       (p.PERSONA_PRIMERNOMBRE ?? "") + " " +
                                       (p.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                                       (p.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                                       (p.PERSONA_SEGUNDOAPELLIDO ?? "")
                                   ).Trim(),
                                   UsuarioNombre = u.USUARIO_NOMBRE ?? u.USUARIO_ID,
                                   Total = v.TOTAL,
                                   Estado = v.ESTADO
                               }).FirstOrDefaultAsync();

            if (venta == null)
            {
                TempData["err"] = "No se encontró la venta especificada.";
                return RedirectToAction(nameof(Index));
            }

            venta.Lineas = await (from d in _db.DETALLE_VENTA
                                  join i in _db.INVENTARIO on d.INVENTARIO_ID equals i.INVENTARIO_ID
                                  join prod in _db.PRODUCTO on d.PRODUCTO_ID equals prod.PRODUCTO_ID
                                  where d.VENTA_ID == id
                                  select new VentaLineaVM
                                  {
                                      ProductoId = d.PRODUCTO_ID,
                                      ProductoNombre = prod.PRODUCTO_NOMBRE ?? prod.PRODUCTO_ID,
                                      ImagenProducto = prod.IMAGEN_PRODUCTO,
                                      Cantidad = d.CANTIDAD,
                                      PrecioUnitario = d.PRECIO_UNITARIO
                                  }).ToListAsync();

            return View(venta);
        }

        // ===========================================================
        // GET: /Ventas/Create
        // ===========================================================
        [HttpGet]   // ← Importante
        public async Task<IActionResult> Create()
        {
            // 1) Precio vigente por producto
            var preciosVigentes = await _db.PRECIO_HISTORICO
                .GroupBy(ph => ph.PRODUCTO_ID)
                .Select(g => g
                    .OrderByDescending(x => x.HASTA == null)
                    .ThenByDescending(x => x.DESDE)
                    .Select(x => new { x.PRODUCTO_ID, x.PRECIO })
                    .FirstOrDefault()
                )
                .ToListAsync();

            var precioIdx = preciosVigentes
                .Where(x => x != null)
                .ToDictionary(x => x.PRODUCTO_ID, x => x.PRECIO);

            // 2) Catálogo por INVENTARIO
            var inventarioCatalogo = await (from inv in _db.INVENTARIO
                                            join p in _db.PRODUCTO on inv.PRODUCTO_ID equals p.PRODUCTO_ID
                                            where inv.ESTADO == true && p.ESTADO == true
                                            select new
                                            {
                                                inventarioId = inv.INVENTARIO_ID,
                                                productoId = p.PRODUCTO_ID,
                                                nombre = p.PRODUCTO_NOMBRE,
                                                imagen = p.IMAGEN_PRODUCTO ?? "",
                                                stock = inv.STOCK_ACTUAL,
                                                precioVenta = precioIdx.ContainsKey(p.PRODUCTO_ID) ? precioIdx[p.PRODUCTO_ID] : 0m
                                            })
                                            .ToListAsync();

            ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(inventarioCatalogo);

            // 3) Combo de clientes
            var clientesCombo = await (from c in _db.CLIENTE
                                       join per in _db.PERSONA on c.CLIENTE_ID equals per.PERSONA_ID
                                       orderby per.PERSONA_PRIMERNOMBRE, per.PERSONA_PRIMERAPELLIDO
                                       select new SelectListItem
                                       {
                                           Value = c.CLIENTE_ID,
                                           Text = per.PERSONA_PRIMERNOMBRE + " " + per.PERSONA_PRIMERAPELLIDO
                                       }).ToListAsync();

            // 4) Combo de usuarios
            ViewBag.UsuariosCombo = await _db.USUARIO
                .Where(u => u.ESTADO == true)
                .Select(u => new SelectListItem { Value = u.USUARIO_ID, Text = u.USUARIO_NOMBRE })
                .ToListAsync();

            // 5) VM inicial
            var vm = new VentaCreateEditVM { ClientesCombo = clientesCombo };
            return View(vm);
        }

        // ======================================================
        // POST: /Ventas/Create
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VentaCreateEditVM vm, CancellationToken ct)
        {
            // -------- Validaciones explícitas (además de [Required]) --------
            if (string.IsNullOrWhiteSpace(vm.ClienteId))
                ModelState.AddModelError(nameof(vm.ClienteId), "Seleccione un cliente.");

            if (string.IsNullOrWhiteSpace(vm.UsuarioId))
                ModelState.AddModelError(nameof(vm.UsuarioId), "Seleccione el usuario (vendedor).");

            if (vm.Detalles == null || vm.Detalles.Count == 0)
                ModelState.AddModelError("", "Agregue al menos un producto a la venta.");

            if (vm.Detalles != null)
            {
                for (int i = 0; i < vm.Detalles.Count; i++)
                {
                    var d = vm.Detalles[i];
                    if (string.IsNullOrWhiteSpace(d.InventarioId))
                        ModelState.AddModelError($"Detalles[{i}].InventarioId", "Falta InventarioId.");
                    if (string.IsNullOrWhiteSpace(d.ProductoId))
                        ModelState.AddModelError($"Detalles[{i}].ProductoId", "Falta ProductoId.");
                    if (d.Cantidad <= 0)
                        ModelState.AddModelError($"Detalles[{i}].Cantidad", "Cantidad debe ser mayor a cero.");
                    if (d.PrecioUnitario < 0)
                        ModelState.AddModelError($"Detalles[{i}].PrecioUnitario", "Precio inválido.");
                }
            }

            if (!ModelState.IsValid)
                return await ReturnCreateViewAsync(vm, ct);  // ← sin redirect

            await using var trx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // -------- Cabecera VENTA --------
                var venta = new VENTA
                {
                    VENTA_ID = await SiguienteVentaIdAsync(),   // p.ej. "VE00000001"
                    CLIENTE_ID = vm.ClienteId!,
                    USUARIO_ID = vm.UsuarioId!,                 // asumiendo NOT NULL en BD
                    FECHA = DateTime.Now,
                    TOTAL = vm.Total,                           // puedes recalcular desde detalles si prefieres
                    USUARIO_CREACION = UserName(),
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                };
                _db.VENTA.Add(venta);
                await _db.SaveChangesAsync(ct);

                // -------- Detalles + Inventario + Kardex --------
                foreach (var d in vm.Detalles!)
                {
                    // Inventario existente
                    var inv = await _db.INVENTARIO
                        .FirstOrDefaultAsync(i => i.INVENTARIO_ID == d.InventarioId && i.PRODUCTO_ID == d.ProductoId, ct);

                    if (inv == null)
                    {
                        await trx.RollbackAsync(ct);
                        ModelState.AddModelError("", $"No se encontró inventario para {d.NombreProducto}.");
                        return await ReturnCreateViewAsync(vm, ct);
                    }

                    var cant = RoundToInt(d.Cantidad);
                    if (inv.STOCK_ACTUAL < cant)
                    {
                        await trx.RollbackAsync(ct);
                        ModelState.AddModelError("", $"Stock insuficiente para {d.NombreProducto} (Stock: {inv.STOCK_ACTUAL}).");
                        return await ReturnCreateViewAsync(vm, ct);
                    }

                    var det = new DETALLE_VENTA
                    {
                        DETALLE_VENTA_ID = NewDetalleVentaId(),
                        VENTA_ID = venta.VENTA_ID,
                        INVENTARIO_ID = d.InventarioId!,
                        PRODUCTO_ID = d.ProductoId!,
                        CANTIDAD = cant,
                        PRECIO_UNITARIO = d.PrecioUnitario,
                        SUBTOTAL = Math.Round(cant * d.PrecioUnitario, 2), // si SUBTOTAL es computed en BD, quítalo
                        USUARIO_CREACION = UserName(),
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.DETALLE_VENTA.Add(det);

                    inv.STOCK_ACTUAL -= cant;
                    inv.USUARIO_MODIFICACION = UserName();
                    inv.FECHA_MODIFICACION = DateTime.Now;

                    // KARDEX (SALIDA)
                    var kardex = new KARDEX
                    {
                        KARDEX_ID = NewKardexId(),              
                        PRODUCTO_ID = d.ProductoId!,
                        FECHA = DateTime.Now,
                        TIPO_MOVIMIENTO = "SALIDA",             // respeta tu CK_KARDEX_TIPO_MOV
                        CANTIDAD = cant,                        // respeta CK_KARDEX_CANTIDAD_POSITIVA
                        COSTO_UNITARIO = d.PrecioUnitario,      // ajusta si usas costo promedio/PEPS
                        REFERENCIA = venta.VENTA_ID,
                        USUARIO_CREACION = UserName(),
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.KARDEX.Add(kardex);
                }

                await _db.SaveChangesAsync(ct);

                // -------- CAJA (INGRESO si hay sesión activa) --------
                var sesionActiva = await GetSesionCajaActivaAsync(vm.UsuarioId!);
                if (!string.IsNullOrWhiteSpace(sesionActiva))
                {
                    var mov = new MOVIMIENTO_CAJA
                    {
                        MOVIMIENTO_ID = await SiguienteMovimientoCajaIdAsync(), // p.ej. "MC00000001"
                        SESION_ID = sesionActiva,
                        TIPO = "INGRESO",                 // usa literal válido según tu tabla (INGRESO/EGRESO/…)
                        MONTO = vm.Total,
                        REFERENCIA = venta.VENTA_ID,
                        FECHA = DateTime.Now,
                        USUARIO_CREACION = UserName(),
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.MOVIMIENTO_CAJA.Add(mov);
                    await _db.SaveChangesAsync(ct);
                }
                else
                {
                    await _bitacora.LogAsync("CAJA_SESION", "WARN", UserName(),
                        $"Venta {venta.VENTA_ID} registrada sin sesión de caja activa.", ct);
                }

                await _bitacora.LogAsync("VENTA", "INSERT", UserName(), $"Venta {venta.VENTA_ID} creada.", ct);
                await trx.CommitAsync(ct);

                TempData["ok"] = $"Venta {venta.VENTA_ID} registrada (Q{venta.TOTAL:N2}).";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                await trx.RollbackAsync(ct);

                // -------- Diagnóstico útil --------
                var root = ex.InnerException?.Message ?? ex.Message;
                TempData["error"] = "Error al guardar la venta: " + root;

                // opcional: ver qué entidad falló
                // var entries = _db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList();

                return await ReturnCreateViewAsync(vm, ct);
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync(ct);
                TempData["error"] = "Error al guardar la venta: " + ex.Message;
                return await ReturnCreateViewAsync(vm, ct);
            }
        }

        private async Task<IActionResult> ReturnCreateViewAsync(VentaCreateEditVM vm, CancellationToken ct)
        {
            // clientes
            vm.ClientesCombo = await (from c in _db.CLIENTE
                                      join per in _db.PERSONA on c.CLIENTE_ID equals per.PERSONA_ID
                                      orderby per.PERSONA_PRIMERNOMBRE, per.PERSONA_PRIMERAPELLIDO
                                      select new SelectListItem
                                      {
                                          Value = c.CLIENTE_ID,
                                          Text = per.PERSONA_PRIMERNOMBRE + " " + per.PERSONA_PRIMERAPELLIDO
                                      }).ToListAsync(ct);

            // usuarios
            ViewBag.UsuariosCombo = await _db.USUARIO
                .Where(u => u.ESTADO == true)
                .Select(u => new SelectListItem { Value = u.USUARIO_ID, Text = u.USUARIO_NOMBRE })
                .ToListAsync(ct);

            // JSON productos (igual que en GET)
            var preciosVigentes = await _db.PRECIO_HISTORICO
                .GroupBy(ph => ph.PRODUCTO_ID)
                .Select(g => g.OrderByDescending(x => x.HASTA == null).ThenByDescending(x => x.DESDE)
                    .Select(x => new { x.PRODUCTO_ID, x.PRECIO })
                    .FirstOrDefault())
                .ToListAsync(ct);

            var precioIdx = preciosVigentes.Where(x => x != null)
                .ToDictionary(x => x.PRODUCTO_ID, x => x.PRECIO);

            var inventarioCatalogo = await (from inv in _db.INVENTARIO
                                            join p in _db.PRODUCTO on inv.PRODUCTO_ID equals p.PRODUCTO_ID
                                            where inv.ESTADO == true && p.ESTADO == true
                                            select new
                                            {
                                                inventarioId = inv.INVENTARIO_ID,
                                                productoId = p.PRODUCTO_ID,
                                                nombre = p.PRODUCTO_NOMBRE,
                                                imagen = p.IMAGEN_PRODUCTO ?? "",
                                                stock = inv.STOCK_ACTUAL,
                                                precioVenta = precioIdx.ContainsKey(p.PRODUCTO_ID) ? precioIdx[p.PRODUCTO_ID] : 0m
                                            })
                                            .ToListAsync(ct);

            ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(inventarioCatalogo);

            return View("Create", vm);
        }


        // ===========================================================
        // DELETE: /Ventas/Delete/{id}
        // ===========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var venta = await _db.VENTA.FirstOrDefaultAsync(v => v.VENTA_ID == id);
            if (venta == null)
            {
                TempData["error"] = "La venta no existe.";
                return RedirectToAction(nameof(Index));
            }

            _db.VENTA.Remove(venta);
            await _db.SaveChangesAsync();

            await _bitacora.LogAsync("VENTA", "DELETE", User.Identity?.Name ?? "Sistema", $"Eliminada venta {id}");

            TempData["ok"] = "Venta eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===========================================================
        // GET: /Ventas/Revertir/{id}
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Revertir(string id)
        {
            var venta = await _db.VENTA.FirstOrDefaultAsync(v => v.VENTA_ID == id);
            if (venta == null)
            {
                TempData["error"] = "La venta no existe.";
                return RedirectToAction(nameof(Index));
            }

            // Cargar detalles de la venta
            var detalles = await (from d in _db.DETALLE_VENTA
                                  join p in _db.PRODUCTO on d.PRODUCTO_ID equals p.PRODUCTO_ID
                                  join i in _db.INVENTARIO on d.INVENTARIO_ID equals i.INVENTARIO_ID
                                  where d.VENTA_ID == id
                                  select new VentaReversionDetalleVM
                                  {
                                      InventarioId = i.INVENTARIO_ID,
                                      ProductoId = p.PRODUCTO_ID,
                                      NombreProducto = p.PRODUCTO_NOMBRE,
                                      CantidadVendida = d.CANTIDAD,
                                      PrecioUnitario = d.PRECIO_UNITARIO
                                  })
                                  .ToListAsync();

            var vm = new VentaReversionVM
            {
                VentaId = id,
                Detalles = detalles
            };

            return View(vm);
        }

        // ===========================================================
        // POST: /Ventas/Revertir
        // ===========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revertir(VentaReversionVM vm)
        {
            if (vm.Detalles == null || vm.Detalles.Count == 0)
            {
                TempData["error"] = "No hay productos seleccionados.";
                return RedirectToAction(nameof(Index));
            }

            using var trx = await _db.Database.BeginTransactionAsync();

            try
            {
                decimal totalReembolso = 0m;

                foreach (var d in vm.Detalles.Where(x => x.CantidadDevuelta > 0))
                {
                    // Recalcular monto del reembolso
                    totalReembolso += d.CantidadDevuelta * d.PrecioUnitario;

                    // Actualizar Inventario (suma de unidades)
                    var inv = await _db.INVENTARIO.FirstOrDefaultAsync(i => i.INVENTARIO_ID == d.InventarioId);
                    if (inv != null)
                    {
                        inv.STOCK_ACTUAL += d.CantidadDevuelta;
                        inv.USUARIO_MODIFICACION = vm.UsuarioId;
                        inv.FECHA_MODIFICACION = DateTime.Now;
                        _db.INVENTARIO.Update(inv);
                    }

                    // KARDEX → ENTRADA
                    var kardex = new KARDEX
                    {
                        KARDEX_ID = "KDX" + Guid.NewGuid().ToString("N")[..7],
                        PRODUCTO_ID = d.ProductoId,
                        FECHA = DateTime.Now,
                        TIPO_MOVIMIENTO = "ENTRADA",
                        CANTIDAD = d.CantidadDevuelta,
                        COSTO_UNITARIO = d.PrecioUnitario,
                        REFERENCIA = $"DEV:{vm.VentaId}",
                        USUARIO_CREACION = vm.UsuarioId,
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.KARDEX.Add(kardex);
                }

                // Crear RECIBO de tipo REEMBOLSO
                if (totalReembolso > 0)
                {
                    var recibo = new RECIBO
                    {
                        RECIBO_ID = "RMB" + Guid.NewGuid().ToString("N")[..7],
                        VENTA_ID = vm.VentaId,
                        METODO_PAGO_ID = "REEMBOLSO",
                        MONTO = totalReembolso,
                        FECHA = DateTime.Now,
                        USUARIO_CREACION = vm.UsuarioId,
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.RECIBO.Add(recibo);

                    // Movimiento de caja (EGRESO)
                    var movCaja = new MOVIMIENTO_CAJA
                    {
                        MOVIMIENTO_ID = "MCJ" + Guid.NewGuid().ToString("N")[..7],
                        TIPO = "EGRESO",
                        MONTO = totalReembolso,
                        REFERENCIA = $"DEV:{vm.VentaId}",
                        FECHA = DateTime.Now,
                        USUARIO_CREACION = vm.UsuarioId,
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.MOVIMIENTO_CAJA.Add(movCaja);
                }

                await _db.SaveChangesAsync();

                // Registrar en Bitácora
                string tipoTxt = vm.TipoReversion == "ANULACION" ? "Anulación" : "Devolución";
                await _bitacora.LogAsync("VENTA", "REVERT", vm.UsuarioId,
                    $"{tipoTxt} de venta {vm.VentaId}. Monto reembolsado: Q{totalReembolso:F2}. Motivo: {vm.Motivo}");

                await trx.CommitAsync();
                TempData["ok"] = $"Venta {vm.VentaId} revertida correctamente. Reembolso: Q{totalReembolso:F2}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                TempData["error"] = "Error al revertir la venta: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
