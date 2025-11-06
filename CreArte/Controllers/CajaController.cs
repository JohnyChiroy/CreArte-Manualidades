using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Bitacora;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CreArte.Controllers
{
    public class CajaController : Controller
    {
        private readonly CreArteDbContext _db;
        private readonly IBitacoraService _bitacora;

        public CajaController(CreArteDbContext db, IBitacoraService bitacora)
        {
            _db = db;
            _bitacora = bitacora;
        }

        // ===============================
        // Helpers comunes 
        // ===============================
        private string UserName() => User?.Identity?.Name ?? "sistema";

        private async Task<string?> ResolveUsuarioIdAsync(string identityOrId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(identityOrId)) return null;

            // Intenta por coincidencia directa de USUARIO_ID
            var direct = await _db.USUARIO
                .Where(u => u.USUARIO_ID == identityOrId)
                .Select(u => u.USUARIO_ID)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            // Intenta por nombre visible (USUARIO_NOMBRE)
            var byName = await _db.USUARIO
                .Where(u => u.USUARIO_NOMBRE == identityOrId)
                .Select(u => u.USUARIO_ID)
                .FirstOrDefaultAsync(ct);

            return byName;
        }

        private static int RoundToInt(decimal value)
            => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

        // Genera IDs para CAJA_SESION
        private async Task<string> SiguienteCajaSesionIdAsync()
        {
            const string prefijo = "CS";
            const int ancho = 8;

            var ids = await _db.CAJA_SESION
                .Select(s => s.SESION_ID)
                .Where(id => id.StartsWith(prefijo))
                .ToListAsync();

            int maxNum = 0;
            var rx = new Regex(@"^" + prefijo + @"(?<n>\d+)$");
            foreach (var id in ids)
            {
                var m = rx.Match(id ?? "");
                if (m.Success && int.TryParse(m.Groups["n"].Value, out int n))
                    if (n > maxNum) maxNum = n;
            }

            var siguiente = maxNum + 1;
            return prefijo + siguiente.ToString(new string('0', ancho));
        }

        // Busca una caja activa 
        private async Task<string> ResolverCajaIdAsync(CancellationToken ct = default)
        {
            
            var cajaId = await _db.CAJA
                .Where(c => c.ESTADO == true)
                .OrderBy(c => c.CAJA_ID)
                .Select(c => c.CAJA_ID)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(cajaId))
                throw new InvalidOperationException("No hay cajas activas configuradas. Configure al menos una en la tabla CAJA.");

            return cajaId;
        }

        // ===============================
        // GET: /Caja  (o /Caja/Index)
        // ===============================
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] CajaIndexVM vm)
        {
            // 1) Proyección base (AsNoTracking para performance)
            var baseQuery =
                from c in _db.CAJA_SESION.AsNoTracking()
                join u in _db.USUARIO.AsNoTracking() on c.USUARIO_APERTURA_ID equals u.USUARIO_ID
                select new CajaIndexItemVM
                {
                    CajaSesionId = c.SESION_ID,
                    UsuarioNombre = (u.USUARIO_NOMBRE ?? u.USUARIO_ID), 
                    FechaApertura = c.FECHA_APERTURA,
                    FechaCierre = c.FECHA_CIERRE,
                    MontoInicial = c.MONTO_INICIAL,
                    Abierta = (c.ESTADO_SESION == "ABIERTA"),


                    TotalIngresos = _db.MOVIMIENTO_CAJA
                        .Where(m => m.SESION_ID == c.SESION_ID && m.TIPO == "INGRESO")
                        .Select(m => (decimal?)m.MONTO).Sum() ?? 0m,

                    TotalEgresos = _db.MOVIMIENTO_CAJA
                        .Where(m => m.SESION_ID == c.SESION_ID && m.TIPO == "EGRESO")
                        .Select(m => (decimal?)m.MONTO).Sum() ?? 0m
                };

            // 2) Filtros
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim();
                baseQuery = baseQuery.Where(x =>
                    x.CajaSesionId.Contains(s) ||
                    x.UsuarioNombre.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(vm.Usuario))
            {
                var u = vm.Usuario.Trim();
                baseQuery = baseQuery.Where(x => x.UsuarioNombre.Contains(u));
            }

            if (vm.Abierta.HasValue)
                baseQuery = baseQuery.Where(x => x.Abierta == vm.Abierta.Value);

            if (vm.Desde.HasValue)
                baseQuery = baseQuery.Where(x => x.FechaApertura >= vm.Desde.Value);

            if (vm.Hasta.HasValue)
            {
                var hasta = vm.Hasta.Value.Date.AddDays(1).AddTicks(-1);
                baseQuery = baseQuery.Where(x => x.FechaApertura <= hasta);
            }

            if (vm.MontoMin.HasValue)
                baseQuery = baseQuery.Where(x => x.SaldoFinal >= vm.MontoMin.Value);
            if (vm.MontoMax.HasValue)
                baseQuery = baseQuery.Where(x => x.SaldoFinal <= vm.MontoMax.Value);

            // 3) Orden (id, apertura, cierre, usuario, inicial, ingresos, egresos, saldo, estado)
            bool asc = string.Equals(vm.Dir, "asc", StringComparison.OrdinalIgnoreCase);
            switch ((vm.Sort ?? "apertura").ToLower())
            {
                case "id": baseQuery = asc ? baseQuery.OrderBy(x => x.CajaSesionId) : baseQuery.OrderByDescending(x => x.CajaSesionId); break;
                case "usuario": baseQuery = asc ? baseQuery.OrderBy(x => x.UsuarioNombre) : baseQuery.OrderByDescending(x => x.UsuarioNombre); break;
                case "inicial": baseQuery = asc ? baseQuery.OrderBy(x => x.MontoInicial) : baseQuery.OrderByDescending(x => x.MontoInicial); break;
                case "ingresos": baseQuery = asc ? baseQuery.OrderBy(x => x.TotalIngresos) : baseQuery.OrderByDescending(x => x.TotalIngresos); break;
                case "egresos": baseQuery = asc ? baseQuery.OrderBy(x => x.TotalEgresos) : baseQuery.OrderByDescending(x => x.TotalEgresos); break;
                case "saldo": baseQuery = asc ? baseQuery.OrderBy(x => x.SaldoFinal) : baseQuery.OrderByDescending(x => x.SaldoFinal); break;
                case "cierre": baseQuery = asc ? baseQuery.OrderBy(x => x.FechaCierre) : baseQuery.OrderByDescending(x => x.FechaCierre); break;
                case "estado": baseQuery = asc ? baseQuery.OrderBy(x => x.Abierta) : baseQuery.OrderByDescending(x => x.Abierta); break;
                default: baseQuery = asc ? baseQuery.OrderBy(x => x.FechaApertura) : baseQuery.OrderByDescending(x => x.FechaApertura); break;
            }

            // 4) Paginación
            vm.TotalItems = await baseQuery.CountAsync();
            if (vm.PageSize <= 0) vm.PageSize = 10;
            vm.TotalPages = (int)Math.Ceiling((double)vm.TotalItems / vm.PageSize);
            if (vm.Page <= 0) vm.Page = 1;
            if (vm.TotalPages > 0 && vm.Page > vm.TotalPages) vm.Page = vm.TotalPages;

            var items = await baseQuery
                .Skip((vm.Page - 1) * vm.PageSize)
                .Take(vm.PageSize)
                .ToListAsync();

            var userId = User?.Identity?.Name ?? "";
            ViewBag.TieneAbiertaActual = await _db.CAJA_SESION
                .AnyAsync(c => c.USUARIO_APERTURA_ID == userId && c.ESTADO == true);
            vm.Items = items;
            return View(vm);
        }

        // ===========================================================
        // GET: /Caja/Abrir
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Abrir(CancellationToken ct)
        {
            var vm = new CajaAperturaVM();

            // 1) Resolver la caja con la que trabaja este usuario
            var cajaId = await ResolverCajaIdAsync(ct);

            // 2) Buscar la ULTIMA sesión CERRADA de esa caja
            var ultimaCerrada = await _db.CAJA_SESION
                .Where(s => s.CAJA_ID == cajaId && s.ESTADO_SESION == "CERRADA")
                .OrderByDescending(s => s.FECHA_CIERRE)      // o .OrderByDescending(s => s.SESION_ID)
                .FirstOrDefaultAsync(ct);

            if (ultimaCerrada != null)
            {
                // 3) Recalcular saldo final de esa sesión (por seguridad, del lado servidor)
                var ingresos = await _db.MOVIMIENTO_CAJA
                    .Where(m => m.SESION_ID == ultimaCerrada.SESION_ID && m.TIPO == "INGRESO")
                    .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

                var egresos = await _db.MOVIMIENTO_CAJA
                    .Where(m => m.SESION_ID == ultimaCerrada.SESION_ID && m.TIPO == "EGRESO")
                    .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

                var saldoFinal = ultimaCerrada.MONTO_INICIAL + ingresos - egresos;

                vm.MontoInicial = Math.Round(saldoFinal, 2, MidpointRounding.AwayFromZero);
                vm.BloquearMonto = true;
                vm.InfoAnterior = $"Última sesión {ultimaCerrada.SESION_ID} cerrada con saldo Q {saldoFinal:N2}.";
            }
            else
            {
                // Primera vez: permitir que el usuario digite
                vm.MontoInicial = 0m;
                vm.BloquearMonto = false;
                vm.InfoAnterior = "No existe historial de caja. Ingrese el monto inicial.";
            }

            return View(vm);
        }

        // ===========================================================
        // POST: /Caja/Abrir
        // ===========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Abrir(CajaAperturaVM vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Complete todos los campos.";
                return View(vm);
            }

            var cajaId = await ResolverCajaIdAsync(ct);

            // Usuario real
            var usuarioId = await _db.USUARIO
                .Where(u => u.USUARIO_NOMBRE == vm.UsuarioId || u.USUARIO_ID == vm.UsuarioId)
                .Select(u => u.USUARIO_ID)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(usuarioId))
            {
                TempData["error"] = "No se encontró el usuario en el sistema.";
                return View(vm);
            }

            // No permitir más de una abierta por CAJA
            var hayCajaAbierta = await _db.CAJA_SESION
                .AnyAsync(c => c.CAJA_ID == cajaId && c.ESTADO_SESION == "ABIERTA", ct);
            if (hayCajaAbierta)
            {
                TempData["error"] = $"La caja {cajaId} ya tiene una sesión abierta. Debe cerrarla antes de abrir otra.";
                return RedirectToAction(nameof(Index));
            }

            // Si venía BloquearMonto=true, recalculamos el monto inicial del lado servidor
            decimal montoInicial = vm.MontoInicial;
            if (vm.BloquearMonto)
            {
                var ultimaCerrada = await _db.CAJA_SESION
                    .Where(s => s.CAJA_ID == cajaId && s.ESTADO_SESION == "CERRADA")
                    .OrderByDescending(s => s.FECHA_CIERRE)
                    .FirstOrDefaultAsync(ct);

                if (ultimaCerrada != null)
                {
                    var ingresos = await _db.MOVIMIENTO_CAJA
                        .Where(m => m.SESION_ID == ultimaCerrada.SESION_ID && m.TIPO == "INGRESO")
                        .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

                    var egresos = await _db.MOVIMIENTO_CAJA
                        .Where(m => m.SESION_ID == ultimaCerrada.SESION_ID && m.TIPO == "EGRESO")
                        .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

                    montoInicial = Math.Round(ultimaCerrada.MONTO_INICIAL + ingresos - egresos, 2,
                                              MidpointRounding.AwayFromZero);
                }
            }

            var sesion = new CAJA_SESION
            {
                SESION_ID = await SiguienteCajaSesionIdAsync(),
                CAJA_ID = cajaId,
                USUARIO_APERTURA_ID = usuarioId,
                FECHA_APERTURA = DateTime.Now,
                MONTO_INICIAL = montoInicial,
                ESTADO = true,
                ESTADO_SESION = "ABIERTA",
                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now,
                ELIMINADO = false
            };

            _db.CAJA_SESION.Add(sesion);
            await _db.SaveChangesAsync(ct);

            await _bitacora.LogAsync("CAJA_SESION", "INSERT", UserName(),
                $"Caja {sesion.SESION_ID} abierta (Monto inicial: Q{montoInicial:F2}).", ct);

            TempData["ok"] = $"Caja {sesion.SESION_ID} abierta correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===========================================================
        // GET: /Caja/Cerrar/{id}
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Cerrar(string id, CancellationToken ct)
        {
            var sesion = await _db.CAJA_SESION
                .Include(c => c.USUARIO_APERTURA)
                .FirstOrDefaultAsync(c => c.SESION_ID == id, ct);

            if (sesion == null)
            {
                TempData["error"] = "Sesión no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            // Si YA está cerrada, no hay nada que cerrar
            if (sesion.ESTADO == false)
            {
                TempData["error"] = $"La sesión {sesion.SESION_ID} ya está cerrada.";
                return RedirectToAction(nameof(Index));
            }

            // Totales
            var ingresos = await _db.MOVIMIENTO_CAJA
                .Where(m => m.SESION_ID == id && m.TIPO == "INGRESO")
                .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

            var egresos = await _db.MOVIMIENTO_CAJA
                .Where(m => m.SESION_ID == id && m.TIPO == "EGRESO")
                .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

            var vm = new CajaCierreVM
            {
                CajaSesionId = id,
                UsuarioId = sesion.USUARIO_APERTURA_ID,
                UsuarioNombre = sesion.USUARIO_APERTURA?.USUARIO_NOMBRE ?? "",
                FechaApertura = sesion.FECHA_APERTURA,
                MontoInicial = sesion.MONTO_INICIAL,
                TotalIngresos = ingresos,
                TotalEgresos = egresos
                // SaldoFinal = Inicial + Ingresos - Egresos (si tu VM lo calcula, ok)
            };

            return View(vm);
        }

        // ===========================================================
        // POST: /Caja/Cerrar
        // ===========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(CajaCierreVM vm, CancellationToken ct)
        {
            var sesion = await _db.CAJA_SESION.FirstOrDefaultAsync(c => c.SESION_ID == vm.CajaSesionId, ct);
            if (sesion == null)
            {
                TempData["error"] = "No se encontró la sesión de caja.";
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(sesion.ESTADO_SESION, "CERRADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["error"] = "La sesión ya está cerrada.";
                return RedirectToAction(nameof(Index));
            }

            var usuarioIdReal = await ResolveUsuarioIdAsync(UserName(), ct) ?? sesion.USUARIO_APERTURA_ID;

            sesion.FECHA_CIERRE = DateTime.Now;
            sesion.ESTADO = false;                 // bit
            sesion.ESTADO_SESION = "CERRADA";      // texto
            sesion.USUARIO_MODIFICACION = usuarioIdReal;
            sesion.FECHA_MODIFICACION = DateTime.Now;

            _db.CAJA_SESION.Update(sesion);
            await _db.SaveChangesAsync(ct);

            await _bitacora.LogAsync("CAJA_SESION", "UPDATE", usuarioIdReal,
                $"Caja {sesion.SESION_ID} cerrada. Saldo final: Q{vm.SaldoFinal:F2}", ct);

            TempData["ok"] = "Caja cerrada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===========================================================
        // GET: /Caja/Detalle/{id}
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Details(string id, CancellationToken ct)
        {
            var sesion = await _db.CAJA_SESION.AsNoTracking()
                .FirstOrDefaultAsync(c => c.SESION_ID == id, ct);
            if (sesion == null)
            {
                TempData["error"] = "Sesión no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            var ingresos = await _db.MOVIMIENTO_CAJA
                .Where(m => m.SESION_ID == id && m.TIPO == "INGRESO")
                .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

            var egresos = await _db.MOVIMIENTO_CAJA
                .Where(m => m.SESION_ID == id && m.TIPO == "EGRESO")
                .Select(m => (decimal?)m.MONTO).SumAsync(ct) ?? 0m;

            ViewBag.Header = new
            {
                SesionId = sesion.SESION_ID,
                Estado = sesion.ESTADO,
                FechaAper = sesion.FECHA_APERTURA,
                FechaCierre = sesion.FECHA_CIERRE,
                MontoInicial = sesion.MONTO_INICIAL,
                Ingresos = ingresos,
                Egresos = egresos,
                Saldo = sesion.MONTO_INICIAL + ingresos - egresos
            };

            var movimientos = await (
                from m in _db.MOVIMIENTO_CAJA.AsNoTracking()
                join u in _db.USUARIO on m.USUARIO_CREACION equals u.USUARIO_ID into ju
                from u in ju.DefaultIfEmpty()
                where m.SESION_ID == id
                orderby m.FECHA descending
                select new CajaDetalleMovimientoVM
                {
                    MovimientoId = m.MOVIMIENTO_ID,
                    Fecha = m.FECHA,
                    TipoMovimiento = m.TIPO,
                    Referencia = m.REFERENCIA,
                    Monto = m.MONTO,
                    UsuarioNombre = (u != null ? (u.USUARIO_NOMBRE ?? u.USUARIO_ID)
                                                : (m.USUARIO_CREACION ?? "sistema"))
                }
            ).ToListAsync(ct);

            ViewBag.SesionId = id;
            return View(movimientos); 
        }
    }
}
