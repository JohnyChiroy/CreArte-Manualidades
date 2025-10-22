using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CreArte.Controllers
{
    public class InventarioController : Controller
    {
        private readonly CreArteDbContext _db;

        public InventarioController(CreArteDbContext db)
        {
            _db = db;
        }

        // -------------------------------------------------------
        // GET: /Inventario
        // Despliega inventario con filtros básicos
        // -------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] InventarioIndexVM vm)
        {
            // 1) Base query con JOIN (sin proyectar aún)
            var q = from inv in _db.INVENTARIO
                    join prod in _db.PRODUCTO on inv.PRODUCTO_ID equals prod.PRODUCTO_ID
                    where inv.ELIMINADO == false
                    select new { inv, prod };

            // 2) Filtros
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var term = vm.Search.Trim();
                q = q.Where(x =>
                    x.prod.PRODUCTO_NOMBRE.Contains(term) ||
                    x.inv.PRODUCTO_ID.Contains(term) ||
                    x.inv.INVENTARIO_ID.Contains(term));
            }

            if (vm.SoloActivos)
                q = q.Where(x => x.inv.ESTADO);

            if (vm.SoloStockBajo)
                q = q.Where(x => x.inv.STOCK_ACTUAL <= x.inv.STOCK_MINIMO);

            if (vm.VenceAntesDe.HasValue)
            {
                // FECHA_VENCIMIENTO es DateOnly? en la entidad -> comparamos como DateOnly
                var limite = DateOnly.FromDateTime(vm.VenceAntesDe.Value.Date);
                q = q.Where(x => x.inv.FECHA_VENCIMIENTO != null && x.inv.FECHA_VENCIMIENTO <= limite);
            }

            // 3) Orden (se hace ANTES del Skip/Take)
            bool asc = string.Equals(vm.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            switch (vm.Sort?.ToLowerInvariant())
            {
                case "id":
                    q = asc ? q.OrderBy(x => x.inv.INVENTARIO_ID) : q.OrderByDescending(x => x.inv.INVENTARIO_ID);
                    break;
                case "producto":
                    q = asc ? q.OrderBy(x => x.prod.PRODUCTO_NOMBRE).ThenBy(x => x.inv.PRODUCTO_ID)
                            : q.OrderByDescending(x => x.prod.PRODUCTO_NOMBRE).ThenByDescending(x => x.inv.PRODUCTO_ID);
                    break;
                case "stock":
                    q = asc ? q.OrderBy(x => x.inv.STOCK_ACTUAL) : q.OrderByDescending(x => x.inv.STOCK_ACTUAL);
                    break;
                case "minimo":
                    q = asc ? q.OrderBy(x => x.inv.STOCK_MINIMO) : q.OrderByDescending(x => x.inv.STOCK_MINIMO);
                    break;
                case "costo":
                    q = asc ? q.OrderBy(x => x.inv.COSTO_UNITARIO) : q.OrderByDescending(x => x.inv.COSTO_UNITARIO);
                    break;
                case "vence":
                    // Ojo: FECHA_VENCIMIENTO es DateOnly? -> ordena nulos al final/inicio según convenga
                    q = asc
                        ? q.OrderBy(x => x.inv.FECHA_VENCIMIENTO == null).ThenBy(x => x.inv.FECHA_VENCIMIENTO)
                        : q.OrderByDescending(x => x.inv.FECHA_VENCIMIENTO).ThenBy(x => x.inv.FECHA_VENCIMIENTO == null);
                    break;
                case "estado":
                    q = asc ? q.OrderBy(x => x.inv.ESTADO) : q.OrderByDescending(x => x.inv.ESTADO);
                    break;
                default:
                    // Por defecto ordenamos por nombre de producto asc
                    q = q.OrderBy(x => x.prod.PRODUCTO_NOMBRE).ThenBy(x => x.inv.PRODUCTO_ID);
                    break;
            }

            // 4) Total para paginación
            vm.TotalItems = await q.CountAsync();
            if (vm.Page < 1) vm.Page = 1;
            if (vm.PageSize <= 0) vm.PageSize = 20;
            var totalPages = vm.TotalPages;
            if (vm.Page > totalPages && totalPages > 0) vm.Page = totalPages;

            // 5) Página solicitada + proyección al VM (conversión DateOnly? -> DateTime?)
            vm.Items = await q
                .Skip((vm.Page - 1) * vm.PageSize)
                .Take(vm.PageSize)
                .Select(x => new InventarioListItemVM
                {
                    INVENTARIO_ID = x.inv.INVENTARIO_ID,
                    PRODUCTO_ID = x.inv.PRODUCTO_ID,
                    ProductoNombre = x.prod.PRODUCTO_NOMBRE,
                    ImagenUrl = x.prod.IMAGEN_PRODUCTO,
                    STOCK_ACTUAL = x.inv.STOCK_ACTUAL,
                    STOCK_MINIMO = x.inv.STOCK_MINIMO,
                    COSTO_UNITARIO = x.inv.COSTO_UNITARIO,
                    FECHA_VENCIMIENTO = x.inv.FECHA_VENCIMIENTO == null
                                        ? (DateTime?)null
                                        : x.inv.FECHA_VENCIMIENTO.Value.ToDateTime(TimeOnly.MinValue),
                    ESTADO = x.inv.ESTADO
                })
                .ToListAsync();

            // 6) Devolver la vista con el VM completo (filtros/orden/paginación/resultados)
            return View(vm);
        }


        // -------------------------------------------------------
        // GET: /Inventario/Details/{id}
        // Muestra ficha de inventario con acciones rápidas
        // -------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var data = await (from inv in _db.INVENTARIO
                              join p in _db.PRODUCTO on inv.PRODUCTO_ID equals p.PRODUCTO_ID
                              where inv.INVENTARIO_ID == id && inv.ELIMINADO == false
                              select new InventarioDetailsVM
                              {
                                  INVENTARIO_ID = inv.INVENTARIO_ID,
                                  PRODUCTO_ID = inv.PRODUCTO_ID,
                                  ProductoNombre = p.PRODUCTO_NOMBRE,
                                  ImagenUrl = p.IMAGEN_PRODUCTO,
                                  STOCK_ACTUAL = inv.STOCK_ACTUAL,
                                  STOCK_MINIMO = inv.STOCK_MINIMO,
                                  COSTO_UNITARIO = inv.COSTO_UNITARIO,

                                  // conversión DateOnly? -> DateTime?
                                  FECHA_VENCIMIENTO = inv.FECHA_VENCIMIENTO == null
                                      ? (DateTime?)null
                                      : inv.FECHA_VENCIMIENTO.Value.ToDateTime(TimeOnly.MinValue),

                                  ESTADO = inv.ESTADO
                              }).FirstOrDefaultAsync();

            if (data == null) return NotFound();
            return View(data);
        }

        // -------------------------------------------------------
        // GET: /Inventario/AjusteEntrada?productoId=PR0000001
        // Si es AJAX -> PartialView (para modal). Si no -> View normal.
        // -------------------------------------------------------
        [HttpGet]
        public IActionResult AjusteEntrada(string productoId)
        {
            var vm = new InventarioAjusteVM
            {
                PRODUCTO_ID = productoId,
                TIPO_MOVIMIENTO = "ENTRADA"
            };

            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            if (isAjax)
            {
                ViewData["Modal"] = true;
                return PartialView("AjusteEntrada", vm);
            }

            return View(vm);
        }

        // -------------------------------------------------------
        // POST: /Inventario/AjusteEntrada
        // Inserta KARDEX (AJUSTE ENTRADA) + actualiza INVENTARIO
        // Si es AJAX -> JSON { ok, message, redirect }.
        // Si no -> Redirect a Details.
        // -------------------------------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AjusteEntrada(InventarioAjusteVM vm, CancellationToken ct)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    ViewData["Modal"] = true;
                    return PartialView("AjusteEntrada", vm); // <-- HTML con mensajes
                }
                return View(vm);
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var usuario = User?.Identity?.Name ?? "sistema";
                var ahora = DateTime.Now;

                var inv = await _db.INVENTARIO
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && i.ELIMINADO == false, ct);

                if (inv == null)
                {
                    inv = new INVENTARIO
                    {
                        INVENTARIO_ID = Guid.NewGuid().ToString("N")[..10],
                        PRODUCTO_ID = vm.PRODUCTO_ID,
                        STOCK_ACTUAL = 0,
                        STOCK_MINIMO = 0,
                        COSTO_UNITARIO = 0,
                        ESTADO = true,
                        ELIMINADO = false,
                        USUARIO_CREACION = usuario,
                        FECHA_CREACION = ahora
                    };
                    _db.INVENTARIO.Add(inv);
                }

                inv.STOCK_ACTUAL += vm.CANTIDAD;
                if (vm.COSTO_UNITARIO.HasValue)
                    inv.COSTO_UNITARIO = vm.COSTO_UNITARIO.Value;

                inv.USUARIO_MODIFICACION = usuario;
                inv.FECHA_MODIFICACION = ahora;

                _db.KARDEX.Add(new KARDEX
                {
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10],
                    PRODUCTO_ID = vm.PRODUCTO_ID,
                    FECHA = ahora,
                    TIPO_MOVIMIENTO = "AJUSTE ENTRADA",
                    CANTIDAD = vm.CANTIDAD,
                    COSTO_UNITARIO = vm.COSTO_UNITARIO,
                    REFERENCIA = string.IsNullOrWhiteSpace(vm.Razon) ? null : vm.Razon.Trim(),
                    USUARIO_CREACION = usuario,
                    FECHA_CREACION = ahora,
                    ESTADO = true,
                    ELIMINADO = false
                });

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var redirectUrl = Url.Action("Details", "Inventario", new { id = inv.INVENTARIO_ID });

                if (isAjax) return Json(new { ok = true, message = "Entrada registrada y stock actualizado.", redirect = redirectUrl });

                TempData["ok"] = "Entrada registrada y stock actualizado.";
                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                if (isAjax) return BadRequest(new { ok = false, message = ex.Message });
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }


        // -------------------------------------------------------
        // GET: /Inventario/AjusteSalida?productoId=PR0000001
        // Si es AJAX -> PartialView (para modal). Si no -> View normal.
        // -------------------------------------------------------
        [HttpGet]
        public IActionResult AjusteSalida(string productoId)
        {
            var vm = new InventarioAjusteVM
            {
                PRODUCTO_ID = productoId,
                TIPO_MOVIMIENTO = "SALIDA"
            };

            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            if (isAjax)
            {
                ViewData["Modal"] = true;
                return PartialView("AjusteSalida", vm);
            }

            return View(vm);
        }

        // -------------------------------------------------------
        // POST: /Inventario/AjusteSalida
        // Inserta KARDEX (AJUSTE SALIDA) + actualiza INVENTARIO
        // Si es AJAX -> JSON { ok, message, redirect }.
        // Si no -> Redirect a Details.
        // -------------------------------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AjusteSalida(InventarioAjusteVM vm, CancellationToken ct)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            ModelState.Remove(nameof(vm.COSTO_UNITARIO));
            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    ViewData["Modal"] = true;
                    return PartialView("AjusteSalida", vm);
                }
                return View(vm);
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var usuario = User?.Identity?.Name ?? "sistema";
                var ahora = DateTime.Now;

                var inv = await _db.INVENTARIO
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && i.ELIMINADO == false, ct);

                if (inv == null)
                    throw new InvalidOperationException("No existe inventario para este producto.");

                if (inv.STOCK_ACTUAL < vm.CANTIDAD)
                    throw new InvalidOperationException("Stock insuficiente para realizar la salida.");

                inv.STOCK_ACTUAL -= vm.CANTIDAD;
                inv.USUARIO_MODIFICACION = usuario;
                inv.FECHA_MODIFICACION = ahora;

                _db.KARDEX.Add(new KARDEX
                {
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10],
                    PRODUCTO_ID = vm.PRODUCTO_ID,
                    FECHA = ahora,
                    TIPO_MOVIMIENTO = "AJUSTE SALIDA",
                    CANTIDAD = vm.CANTIDAD,
                    COSTO_UNITARIO = inv.COSTO_UNITARIO, // referencia del costo vigente
                    REFERENCIA = string.IsNullOrWhiteSpace(vm.Razon) ? null : vm.Razon.Trim(),
                    USUARIO_CREACION = usuario,
                    FECHA_CREACION = ahora,
                    ESTADO = true,
                    ELIMINADO = false
                });

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var redirectUrl = Url.Action("Details", "Inventario", new { id = inv.INVENTARIO_ID });

                if (isAjax) return Json(new { ok = true, message = "Salida registrada y stock actualizado.", redirect = redirectUrl });

                TempData["ok"] = "Salida registrada y stock actualizado.";
                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                if (isAjax) return BadRequest(new { ok = false, message = ex.Message });
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        // =======================================================
        // GET: /Inventario/AjustePrecio?productoId=PR00000001
        // Si es AJAX -> PartialView (para modal). Si no -> View normal.
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> AjustePrecio(string productoId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(productoId)) return NotFound();

            var inv = await _db.INVENTARIO
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.PRODUCTO_ID == productoId && i.ELIMINADO == false, ct);

            var vm = new InventarioAjustePrecioVM
            {
                PRODUCTO_ID = productoId,
                CostoActual = inv?.COSTO_UNITARIO ?? 0
            };

            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            if (isAjax)
            {
                ViewData["Modal"] = true;
                return PartialView("AjustePrecio", vm);
            }

            return View(vm);
        }

        // =======================================================
        // POST: /Inventario/AjustePrecio
        // Actualiza costo + KARDEX(AJUSTE PRECIO) + PRECIO_HISTORICO
        // Si es AJAX -> JSON { ok, message, redirect }.
        // Si no -> Redirect a Details.
        // =======================================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustePrecio(InventarioAjustePrecioVM vm, CancellationToken ct)
        {
            bool isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            ModelState.Remove(nameof(InventarioAjustePrecioVM.CostoActual));
            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    // si quieres, recarga CostoActual aquí para el re-render
                    var invNow = await _db.INVENTARIO.AsNoTracking()
                        .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && !i.ELIMINADO, ct);
                    vm.CostoActual = invNow?.COSTO_UNITARIO ?? 0m;

                    ViewData["Modal"] = true;
                    return PartialView("AjustePrecio", vm);
                }

                var invNow2 = await _db.INVENTARIO.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && !i.ELIMINADO, ct);
                vm.CostoActual = invNow2?.COSTO_UNITARIO ?? 0m;
                return View(vm);
            }


            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var usuario = User?.Identity?.Name ?? "sistema";
                var ahora = DateTime.Now;

                var inv = await _db.INVENTARIO
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && i.ELIMINADO == false, ct);

                if (inv == null)
                {
                    inv = new INVENTARIO
                    {
                        INVENTARIO_ID = Guid.NewGuid().ToString("N")[..10],
                        PRODUCTO_ID = vm.PRODUCTO_ID,
                        STOCK_ACTUAL = 0,
                        STOCK_MINIMO = 0,
                        COSTO_UNITARIO = 0,
                        ESTADO = true,
                        ELIMINADO = false,
                        USUARIO_CREACION = usuario,
                        FECHA_CREACION = ahora
                    };
                    _db.INVENTARIO.Add(inv);
                }

                var costoAnterior = inv.COSTO_UNITARIO;
                var costoNuevo = vm.NuevoCostoUnitario;

                // Cambiar costo (no mueve stock)
                inv.COSTO_UNITARIO = costoNuevo;
                inv.USUARIO_MODIFICACION = usuario;
                inv.FECHA_MODIFICACION = ahora;

                // KARDEX: AJUSTE PRECIO (CANTIDAD = 0)
                _db.KARDEX.Add(new KARDEX
                {
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10],
                    PRODUCTO_ID = vm.PRODUCTO_ID,
                    FECHA = ahora,
                    TIPO_MOVIMIENTO = "AJUSTE PRECIO",
                    CANTIDAD = 0,
                    COSTO_UNITARIO = costoNuevo,
                    REFERENCIA = string.IsNullOrWhiteSpace(vm.Razon) ? null : vm.Razon.Trim(),
                    USUARIO_CREACION = usuario,
                    FECHA_CREACION = ahora,
                    ESTADO = true,
                    ELIMINADO = false
                });

                // PRECIO_HISTORICO (solo si cambió el precio)
                if (decimal.Compare(costoNuevo, costoAnterior) != 0)
                {
                    var vigente = await _db.PRECIO_HISTORICO
                        .Where(ph => ph.PRODUCTO_ID == vm.PRODUCTO_ID && ph.ELIMINADO == false && ph.HASTA == null)
                        .OrderByDescending(ph => ph.DESDE)
                        .FirstOrDefaultAsync(ct);

                    if (vigente != null)
                    {
                        vigente.HASTA = ahora; // o ahora.AddTicks(-1)
                        vigente.USUARIO_MODIFICACION = usuario;
                        vigente.FECHA_MODIFICACION = ahora;
                    }

                    _db.PRECIO_HISTORICO.Add(new PRECIO_HISTORICO
                    {
                        PRECIO_ID = Guid.NewGuid().ToString("N")[..10],
                        PRODUCTO_ID = vm.PRODUCTO_ID,
                        PRECIO = costoNuevo,
                        DESDE = ahora,
                        HASTA = null,
                        USUARIO_CREACION = usuario,
                        FECHA_CREACION = ahora,
                        ESTADO = true,
                        ELIMINADO = false
                    });
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var redirectUrl = Url.Action("Details", "Inventario", new { id = inv.INVENTARIO_ID });

                if (isAjax) return Json(new { ok = true, message = "Ajuste de precio realizado correctamente.", redirect = redirectUrl });

                TempData["ok"] = "Ajuste de precio realizado correctamente.";
                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);

                if (isAjax) return BadRequest(new { ok = false, message = ex.Message });

                var invNow = await _db.INVENTARIO
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && i.ELIMINADO == false, ct);
                vm.CostoActual = invNow?.COSTO_UNITARIO ?? 0m;

                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        //Para ver historial de precios
        [HttpGet]
        public async Task<IActionResult> PrecioHistorico(string productoId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(productoId)) return NotFound();

            // Traer INVENTARIO_ID (si hay inventario creado)
            var invId = await _db.INVENTARIO
                .Where(i => i.PRODUCTO_ID == productoId && i.ELIMINADO == false)
                .Select(i => i.INVENTARIO_ID)
                .FirstOrDefaultAsync(ct);

            var precios = await _db.PRECIO_HISTORICO
                .Where(ph => ph.PRODUCTO_ID == productoId && ph.ELIMINADO == false)
                .OrderByDescending(ph => ph.DESDE)
                .ToListAsync(ct);

            var vm = new PrecioHistoricoVM
            {
                PRODUCTO_ID = productoId,
                ProductoNombre = await _db.PRODUCTO
                    .Where(p => p.PRODUCTO_ID == productoId)
                    .Select(p => p.PRODUCTO_NOMBRE)
                    .FirstOrDefaultAsync(ct) ?? string.Empty,
                INVENTARIO_ID = invId,   
                Items = precios.Select(ph => new PrecioHistoricoItemVM
                {
                    PRECIO_ID = ph.PRECIO_ID,
                    PRECIO = ph.PRECIO,
                    DESDE = ph.DESDE,
                    HASTA = ph.HASTA,
                    USUARIO = ph.USUARIO_CREACION
                }).ToList()
            };

            return View(vm);
        }
    }
}
