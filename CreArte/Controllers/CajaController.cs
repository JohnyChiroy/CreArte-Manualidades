using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Bitacora;
using Microsoft.AspNetCore.Mvc;
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
        // Helpers comunes (mismo patrón que Pedidos)
        // ===============================
        private string UserName() => User?.Identity?.Name ?? "sistema";

        private static int RoundToInt(decimal value)
            => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

        // Genera IDs para CAJA_SESION: CS00000000
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
                    Abierta = c.ESTADO,

                    
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

            // 5) Devolver SIEMPRE el VM (clave del error)
            vm.Items = items;
            return View(vm);
        }

        // ===========================================================
        // GET: /Caja/Abrir
        // ===========================================================
        [HttpGet]
        public IActionResult Abrir()
        {
            var vm = new CajaAperturaVM();
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

            // ¿Ya existe sesión abierta para ese usuario?
            var abierta = await _db.CAJA_SESION
                .AnyAsync(c => c.USUARIO_APERTURA_ID == vm.UsuarioId && c.ESTADO == true, ct);

            if (abierta)
            {
                TempData["error"] = "Ya tiene una sesión de caja abierta.";
                return RedirectToAction(nameof(Index));
            }

            var sesion = new CAJA_SESION
            {
                SESION_ID = await SiguienteCajaSesionIdAsync(),
                USUARIO_APERTURA_ID = vm.UsuarioId,
                FECHA_APERTURA = DateTime.Now,
                MONTO_INICIAL = vm.MontoInicial,
                ESTADO = true,

                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now
            };

            _db.CAJA_SESION.Add(sesion);
            await _db.SaveChangesAsync(ct);

            await _bitacora.LogAsync("CAJA_SESION", "INSERT", UserName(),
                $"Caja {sesion.SESION_ID} abierta (Monto inicial: Q{vm.MontoInicial:F2}).", ct);

            TempData["ok"] = $"Caja {sesion.SESION_ID} abierta correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===========================================================
        // GET: /Caja/Cerrar/{id}
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Cerrar(string id)
        {
            var sesion = await _db.CAJA_SESION
                .Include(u => u.USUARIO_APERTURA)
                .FirstOrDefaultAsync(c => c.SESION_ID == id);

            if (sesion == null)
            {
                TempData["error"] = "Sesión no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            var ingresos = await _db.MOVIMIENTO_CAJA
                .Where(m => m.SESION_ID == id && m.MOVIMIENTO_ID == "INGRESO")
                .SumAsync(m => (decimal?)m.MONTO) ?? 0m;

            var egresos = await _db.MOVIMIENTO_CAJA
                .Where(m => m.SESION_ID == id && m.MOVIMIENTO_ID == "EGRESO")
                .SumAsync(m => (decimal?)m.MONTO) ?? 0m;

            var vm = new CajaCierreVM
            {
                CajaSesionId = id,
                UsuarioId = sesion.USUARIO_APERTURA_ID,
                MontoInicial = sesion.MONTO_INICIAL,
                TotalIngresos = ingresos,
                TotalEgresos = egresos
            };

            return View(vm);
        }

        // ===========================================================
        // POST: /Caja/Cerrar
        // ===========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(CajaCierreVM vm)
        {
            var sesion = await _db.CAJA_SESION.FirstOrDefaultAsync(c => c.SESION_ID == vm.CajaSesionId);
            if (sesion == null)
            {
                TempData["error"] = "No se encontró la sesión de caja.";
                return RedirectToAction(nameof(Index));
            }

            sesion.FECHA_CIERRE = DateTime.Now;
            sesion.ESTADO = false;
            sesion.USUARIO_MODIFICACION = vm.UsuarioId;
            sesion.FECHA_MODIFICACION = DateTime.Now;

            _db.CAJA_SESION.Update(sesion);
            await _db.SaveChangesAsync();

            await _bitacora.LogAsync("CAJA_SESION", "UPDATE", vm.UsuarioId,
                $"Caja cerrada. Saldo final: Q{vm.SaldoFinal:F2}");

            TempData["ok"] = "Caja cerrada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ===========================================================
        // GET: /Caja/Detalle/{id}
        // ===========================================================
        [HttpGet]
        public async Task<IActionResult> Detalle(string id)
        {
            var movimientos = await (from m in _db.MOVIMIENTO_CAJA
                                     join u in _db.USUARIO on m.USUARIO_CREACION equals u.USUARIO_ID
                                     where m.SESION_ID == id
                                     orderby m.FECHA descending
                                     select new CajaDetalleMovimientoVM
                                     {
                                         MovimientoId = m.MOVIMIENTO_ID,
                                         Fecha = m.FECHA,
                                         TipoMovimiento = m.MOVIMIENTO_ID,
                                         Referencia = m.REFERENCIA,
                                         Monto = m.MONTO,
                                         UsuarioNombre = u.USUARIO_NOMBRE
                                     })
                                     .ToListAsync();

            ViewBag.SesionId = id;
            return View(movimientos);
        }
    }
}
