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

                                  // ✅ conversión DateOnly? -> DateTime?
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
        // Formulario para registrar ENTRADA manual
        // -------------------------------------------------------
        [HttpGet]
        public IActionResult AjusteEntrada(string productoId)
        {
            var vm = new InventarioAjusteVM
            {
                PRODUCTO_ID = productoId,
                TIPO_MOVIMIENTO = "ENTRADA"
            };
            return View(vm);
        }

        // -------------------------------------------------------
        // POST: /Inventario/AjusteEntrada
        // Inserta en KARDEX (ENTRADA) + actualiza INVENTARIO
        // -------------------------------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AjusteEntrada(InventarioAjusteVM vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var usuario = User?.Identity?.Name ?? "sistema";
                var ahora = DateTime.Now;

                // 1) Cargar o crear inventario
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

                // 2) Modificar stock y costo (opcional)
                inv.STOCK_ACTUAL += vm.CANTIDAD;
                if (vm.COSTO_UNITARIO.HasValue)
                    inv.COSTO_UNITARIO = vm.COSTO_UNITARIO.Value;

                inv.USUARIO_MODIFICACION = usuario;
                inv.FECHA_MODIFICACION = ahora;

                // 3) Registrar línea en KARDEX (**AJUSTE ENTRADA**)
                var kardex = new KARDEX
                {
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10],
                    PRODUCTO_ID = vm.PRODUCTO_ID,
                    FECHA = ahora,
                    TIPO_MOVIMIENTO = "AJUSTE ENTRADA",  // <- ANTES: "ENTRADA"
                    CANTIDAD = vm.CANTIDAD,
                    COSTO_UNITARIO = vm.COSTO_UNITARIO,  // puede ir 0 o null
                    REFERENCIA = string.IsNullOrWhiteSpace(vm.Razon) ? null : vm.Razon.Trim(), // <- guarda Razón
                    USUARIO_CREACION = usuario,
                    FECHA_CREACION = ahora,
                    ESTADO = true,
                    ELIMINADO = false
                };
                _db.KARDEX.Add(kardex);


                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["ok"] = "Entrada registrada y stock actualizado.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        // -------------------------------------------------------
        // GET: /Inventario/AjusteSalida?productoId=PR0000001
        // Formulario para registrar SALIDA manual
        // -------------------------------------------------------
        [HttpGet]
        public IActionResult AjusteSalida(string productoId)
        {
            var vm = new InventarioAjusteVM
            {
                PRODUCTO_ID = productoId,
                TIPO_MOVIMIENTO = "SALIDA"
            };
            return View(vm);
        }

        // -------------------------------------------------------
        // POST: /Inventario/AjusteSalida
        // Inserta en KARDEX (SALIDA) + actualiza INVENTARIO
        // -------------------------------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AjusteSalida(InventarioAjusteVM vm, CancellationToken ct)
        {
            // No exigimos COSTO en salida
            ModelState.Remove(nameof(vm.COSTO_UNITARIO));
            if (!ModelState.IsValid) return View(vm);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var usuario = User?.Identity?.Name ?? "sistema";
                var ahora = DateTime.Now;

                // 1) Cargar inventario
                var inv = await _db.INVENTARIO
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && i.ELIMINADO == false, ct);

                if (inv == null)
                    throw new InvalidOperationException("No existe inventario para este producto.");

                // 2) Validar stock
                if (inv.STOCK_ACTUAL < vm.CANTIDAD)
                    throw new InvalidOperationException("Stock insuficiente para realizar la salida.");

                inv.STOCK_ACTUAL -= vm.CANTIDAD;
                inv.USUARIO_MODIFICACION = usuario;
                inv.FECHA_MODIFICACION = ahora;

                // 3) Registrar KARDEX (**AJUSTE SALIDA**)
                var kardex = new KARDEX
                {
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10],
                    PRODUCTO_ID = vm.PRODUCTO_ID,
                    FECHA = ahora,
                    TIPO_MOVIMIENTO = "AJUSTE SALIDA",   // <- ANTES: "SALIDA"
                    CANTIDAD = vm.CANTIDAD,
                    COSTO_UNITARIO = inv.COSTO_UNITARIO, // opcional, referencia del costo vigente
                    REFERENCIA = string.IsNullOrWhiteSpace(vm.Razon) ? null : vm.Razon.Trim(), // <- guarda Razón
                    USUARIO_CREACION = usuario,
                    FECHA_CREACION = ahora,
                    ESTADO = true,
                    ELIMINADO = false
                };
                _db.KARDEX.Add(kardex);


                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["ok"] = "Salida registrada y stock actualizado.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        // =======================================================
        // GET: /Inventario/AjustePrecio?productoId=PR00000001
        // Muestra formulario para cambiar costo unitario.
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> AjustePrecio(string productoId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(productoId)) return NotFound();

            // Traemos costo actual (si hay inventario)
            var inv = await _db.INVENTARIO
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.PRODUCTO_ID == productoId && i.ELIMINADO == false, ct);

            var vm = new InventarioAjustePrecioVM
            {
                PRODUCTO_ID = productoId,
                CostoActual = inv?.COSTO_UNITARIO ?? 0
            };

            return View(vm); 
        }

        // POST: /Inventario/AjustePrecio
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustePrecio(InventarioAjustePrecioVM vm, CancellationToken ct)
        {
            // 1) Este campo es solo informativo: que no bloquee el POST
            ModelState.Remove(nameof(InventarioAjustePrecioVM.CostoActual));

            // 2) Si falló el parseo del número por tema de coma/punto, inténtalo manualmente
            if (!ModelState.IsValid)
            {
                var raw = Request.Form[nameof(InventarioAjustePrecioVM.NuevoCostoUnitario)];
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    // Intenta con cultura actual (ej. es-ES usa coma)
                    if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out var v1))
                    {
                        vm.NuevoCostoUnitario = v1;
                        ModelState.Clear(); // ya lo resolvimos, limpiamos errores de binding
                    }
                    else if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v2))
                    {
                        vm.NuevoCostoUnitario = v2;
                        ModelState.Clear();
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                // 3) Si aún hay errores, recarga el costo actual para no mostrar 0.00
                var invNow = await _db.INVENTARIO
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && i.ELIMINADO == false, ct);

                vm.CostoActual = invNow?.COSTO_UNITARIO ?? 0m;
                return View(vm);
            }

            // ======== LÓGICA ORIGINAL (sin cambios) ========
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

                inv.COSTO_UNITARIO = vm.NuevoCostoUnitario;
                inv.USUARIO_MODIFICACION = usuario;
                inv.FECHA_MODIFICACION = ahora;

                _db.KARDEX.Add(new KARDEX
                {
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10],
                    PRODUCTO_ID = vm.PRODUCTO_ID,
                    FECHA = ahora,
                    TIPO_MOVIMIENTO = "AJUSTE PRECIO",
                    CANTIDAD = 0,
                    COSTO_UNITARIO = vm.NuevoCostoUnitario,
                    REFERENCIA = string.IsNullOrWhiteSpace(vm.Razon) ? null : vm.Razon.Trim(),
                    USUARIO_CREACION = usuario,
                    FECHA_CREACION = ahora,
                    ESTADO = true,
                    ELIMINADO = false
                });

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["ok"] = "Ajuste de precio realizado correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                // Recarga costo actual para no mostrar 0
                var invNow = await _db.INVENTARIO
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == vm.PRODUCTO_ID && i.ELIMINADO == false, ct);
                vm.CostoActual = invNow?.COSTO_UNITARIO ?? 0m;

                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }
    }
}
