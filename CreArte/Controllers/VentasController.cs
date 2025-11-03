using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Bitacora;

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

        // ===========================================================
        // GET: /Ventas
        // ===========================================================
        public async Task<IActionResult> Index()
        {
            // Join explícito para respetar tu modelo: CLIENTE -> PERSONA (nombre)
            var query = from v in _db.VENTA
                        join p in _db.PERSONA on v.CLIENTE_ID equals p.PERSONA_ID
                        join u in _db.USUARIO on v.USUARIO_ID equals u.USUARIO_ID
                        orderby v.FECHA descending
                        select new VentaIndexVM
                        {
                            VentaId = v.VENTA_ID,
                            ClienteNombre = (p.PERSONA_PRIMERNOMBRE + " " + p.PERSONA_PRIMERAPELLIDO),
                            Fecha = v.FECHA,
                            Total = v.TOTAL,
                            UsuarioNombre = u.USUARIO_NOMBRE
                        };

            var ventas = await query.ToListAsync();
            return View(ventas);
        }

        // ===========================================================
        // GET: /Ventas/Create
        // ===========================================================
        public async Task<IActionResult> Create()
        {
            // 1) Precio vigente por producto (PRECIO_HISTORICO: HASTA IS NULL o DESDE más reciente)
            var preciosVigentes = await _db.PRECIO_HISTORICO
                .GroupBy(ph => ph.PRODUCTO_ID)
                .Select(g => g
                    .OrderByDescending(x => x.HASTA == null)   // primero los vigentes (HASTA NULL)
                    .ThenByDescending(x => x.DESDE)            // luego el más reciente
                    .Select(x => new { x.PRODUCTO_ID, x.PRECIO })
                    .FirstOrDefault()
                )
                .ToListAsync();

            var precioIdx = preciosVigentes
                .Where(x => x != null)
                .ToDictionary(x => x.PRODUCTO_ID, x => x.PRECIO);

            // 2) Catálogo por INVENTARIO (debe enviarse INVENTARIO_ID y PRODUCTO_ID)
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

            // 3) Combo de clientes (PERSONA)
            var clientesCombo = await (from c in _db.CLIENTE
                                       join per in _db.PERSONA on c.CLIENTE_ID equals per.PERSONA_ID
                                       orderby per.PERSONA_PRIMERNOMBRE, per.PERSONA_PRIMERAPELLIDO
                                       select new SelectListItem
                                       {
                                           Value = c.CLIENTE_ID, // = PERSONA_ID
                                           Text = per.PERSONA_PRIMERNOMBRE + " " + per.PERSONA_PRIMERAPELLIDO
                                       }).ToListAsync();

            // 4) Combo de usuarios (vendedores)
            var usuariosCombo = await _db.USUARIO
                .Where(u => u.ESTADO == true)
                .Select(u => new SelectListItem
                {
                    Value = u.USUARIO_ID,
                    Text = u.USUARIO_NOMBRE
                })
                .ToListAsync();
            ViewBag.UsuariosCombo = usuariosCombo;

            // 5) VM inicial
            var vm = new VentaCreateEditVM
            {
                ClientesCombo = clientesCombo
                // ProductosCombo: no necesario si todo viene por JSON
            };

            return View(vm);
        }

        // ===========================================================
        // POST: /Ventas/Create
        // ===========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VentaCreateEditVM vm)
        {
            // 🔎 Validación inicial: modelo y al menos 1 detalle
            if (!ModelState.IsValid || vm.Detalles == null || vm.Detalles.Count == 0)
            {
                TempData["error"] = "Complete todos los campos obligatorios y agregue al menos un producto.";
                return RedirectToAction(nameof(Create));
            }

            using var trx = await _db.Database.BeginTransactionAsync();

            try
            {
                // 1) Cabecera de venta (usa FECHA de tu tabla VENTA)
                var venta = new VENTA
                {
                    VENTA_ID = "VEN" + Guid.NewGuid().ToString("N")[..7],
                    CLIENTE_ID = vm.ClienteId,      // = PERSONA_ID
                    USUARIO_ID = vm.UsuarioId,
                    FECHA = DateTime.Now,
                    TOTAL = vm.Total,

                    // Auditoría (según tu esquema)
                    USUARIO_CREACION = vm.UsuarioId,
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                };
                _db.VENTA.Add(venta);
                await _db.SaveChangesAsync();

                // 2) Detalles + Inventario + Kardex
                foreach (var d in vm.Detalles)
                {
                    // 2.a) Validar InventarioId (FK compuesta en DETALLE_VENTA)
                    if (string.IsNullOrWhiteSpace(d.InventarioId))
                    {
                        await trx.RollbackAsync();
                        TempData["error"] = "Falta el InventarioId en un renglón.";
                        return RedirectToAction(nameof(Create));
                    }

                    // 2.b) Insertar detalle de venta
                    var det = new DETALLE_VENTA
                    {
                        DETALLE_VENTA_ID = Guid.NewGuid().ToString("N")[..10],
                        VENTA_ID = venta.VENTA_ID,
                        INVENTARIO_ID = d.InventarioId,
                        PRODUCTO_ID = d.ProductoId,
                        CANTIDAD = d.Cantidad,
                        PRECIO_UNITARIO = d.PrecioUnitario,
                        // SUBTOTAL calculado en BD (si tu columna lo es)

                        // Auditoría
                        USUARIO_CREACION = vm.UsuarioId,
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.DETALLE_VENTA.Add(det);

                    // 2.c) Actualizar inventario (usa STOCK_ACTUAL)
                    var inv = await _db.INVENTARIO
                        .FirstOrDefaultAsync(i => i.INVENTARIO_ID == d.InventarioId && i.PRODUCTO_ID == d.ProductoId);

                    if (inv == null)
                    {
                        await trx.RollbackAsync();
                        TempData["error"] = $"No se encontró el inventario para el producto {d.NombreProducto}.";
                        return RedirectToAction(nameof(Create));
                    }

                    if (inv.STOCK_ACTUAL < d.Cantidad)
                    {
                        await trx.RollbackAsync();
                        TempData["error"] = $"Stock insuficiente para el producto {d.NombreProducto} (Stock: {inv.STOCK_ACTUAL}).";
                        return RedirectToAction(nameof(Create));
                    }

                    inv.STOCK_ACTUAL -= d.Cantidad;
                    inv.USUARIO_MODIFICACION = vm.UsuarioId;
                    inv.FECHA_MODIFICACION = DateTime.Now;
                    _db.INVENTARIO.Update(inv);

                    // 2.d) Registrar KARDEX (SALIDA)
                    var kardex = new KARDEX
                    {
                        KARDEX_ID = "KDX" + Guid.NewGuid().ToString("N")[..7],
                        PRODUCTO_ID = d.ProductoId,
                        FECHA = DateTime.Now,
                        TIPO_MOVIMIENTO = "SALIDA",
                        CANTIDAD = d.Cantidad,
                        COSTO_UNITARIO = d.PrecioUnitario, // si manejas costo distinto, ajústalo aquí
                        REFERENCIA = venta.VENTA_ID,

                        // Auditoría
                        USUARIO_CREACION = vm.UsuarioId,
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.KARDEX.Add(kardex);
                }

                await _db.SaveChangesAsync();

                // 3) Movimiento de CAJA (INGRESO)
                // Comentario:
                // - Vincula el ingreso de dinero por la venta a la sesión de caja activa del vendedor.
                // - Si no hay sesión activa, deja un WARN en bitácora (control administrativo).
                var sesionActiva = await GetSesionCajaActivaAsync(vm.UsuarioId);
                if (sesionActiva != null)
                {
                    var movCaja = new MOVIMIENTO_CAJA
                    {
                        MOVIMIENTO_ID = "MCJ" + Guid.NewGuid().ToString("N")[..7],
                        SESION_ID = sesionActiva,
                        TIPO = "INGRESO",
                        MONTO = vm.Total,
                        REFERENCIA = venta.VENTA_ID,    // trazabilidad con la venta
                        FECHA = DateTime.Now,

                        // Auditoría
                        USUARIO_CREACION = vm.UsuarioId,
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    };
                    _db.MOVIMIENTO_CAJA.Add(movCaja);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    // No se bloquea la venta: solo se registra advertencia
                    await _bitacora.LogAsync("CAJA_SESION", "WARN", vm.UsuarioId,
                        $"Venta {venta.VENTA_ID} registrada sin sesión de caja activa.");
                }

                // 4) Bitácora (venta creada)
                await _bitacora.LogAsync("VENTA", "INSERT", vm.UsuarioId, $"Venta {venta.VENTA_ID} creada.");

                // 5) Confirmar transacción
                await trx.CommitAsync();

                TempData["ok"] = $"Venta {venta.VENTA_ID} registrada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                TempData["error"] = "Error al guardar la venta: " + ex.Message;
                return RedirectToAction(nameof(Create));
            }
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

                    // 4️⃣ Movimiento de caja (EGRESO)
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

                // 5️⃣ Registrar en Bitácora
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

        // ===========================================================
        // Método auxiliar: obtener sesión de caja activa
        // ===========================================================
        private async Task<string?> GetSesionCajaActivaAsync(string usuarioId)
        {
            var sesion = await _db.CAJA_SESION
                .Where(c => c.USUARIO_APERTURA_ID == usuarioId && c.ESTADO == true)
                .OrderByDescending(c => c.FECHA_APERTURA)
                .FirstOrDefaultAsync();

            return sesion?.CAJA_ID;
        }
    }
}
