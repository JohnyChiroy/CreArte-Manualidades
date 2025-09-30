using CreArte.Data;                 // DbContext
using CreArte.Models;               // Entidades (TIPO_DESCRIPCION)
using CreArte.ModelsPartial;        // ViewModels de TipoCliente
using CreArte.Services.Auditoria;   // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class TiposClienteController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public TiposClienteController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /TiposCliente?...
        // Filtros: Search (global), Nombre (texto), Estado (bool?)
        // Orden: id | nombre | estado | fecha
        // ============================================================
        public async Task<IActionResult> Index(
            string? Search,
            string? Nombre,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Base (no eliminados)
            IQueryable<TIPO_CLIENTE> q = _context.TIPO_CLIENTE.Where(c => !c.ELIMINADO);

            // 2) Búsqueda global (por ID, Nombre, Descripción)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(c =>
                    EF.Functions.Like(c.TIPO_CLIENTE_ID, $"%{s}%") ||
                    EF.Functions.Like(c.TIPO_CLIENTE_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(c.TIPO_CLIENTE_DESCRIPCION ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por NOMBRE (texto exacto/parcial)
            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                var n = Nombre.Trim();
                q = q.Where(c => EF.Functions.Like(c.TIPO_CLIENTE_NOMBRE, $"%{n}%"));
            }

            // 4) Filtro por ESTADO
            if (Estado.HasValue)
                q = q.Where(c => c.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(c => c.TIPO_CLIENTE_ID) : q.OrderByDescending(c => c.TIPO_CLIENTE_ID),
                "nombre" => asc ? q.OrderBy(c => c.TIPO_CLIENTE_NOMBRE) : q.OrderByDescending(c => c.TIPO_CLIENTE_NOMBRE),
                "estado" => asc ? q.OrderBy(c => c.ESTADO) : q.OrderByDescending(c => c.ESTADO),
                _ => asc ? q.OrderBy(c => c.FECHA_CREACION) : q.OrderByDescending(c => c.FECHA_CREACION),
            };

            // 6) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            var items = await q
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 7) VM salida
            var vm = new TipoClienteViewModels
            {
                Items = items,
                Search = Search,
                Nombre = Nombre,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = Page,
                PageSize = PageSize,
                TotalPages = totalPages,
                TotalItems = total
            };

            return View(vm);
        }

        // ============================================================
        // DETAILS (tarjeta/modal) – GET /TiposCliente/DetailsCard?id=...
        // ▸ Devuelve PartialView("Details", vm)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.TIPO_CLIENTE
                .AsNoTracking()
                .Where(c => c.TIPO_CLIENTE_ID == id && !c.ELIMINADO)
                .Select(c => new TipoClienteDetailsVM
                {
                    TC_ID = c.TIPO_CLIENTE_ID,
                    TC_NOMBRE = c.TIPO_CLIENTE_NOMBRE,
                    TC_DESCRIPCION = c.TIPO_CLIENTE_DESCRIPCION,
                    ESTADO = c.ESTADO,

                    // Auditoría
                    USUARIO_CREACION = c.USUARIO_CREACION,
                    FECHA_CREACION = c.FECHA_CREACION,
                    USUARIO_MODIFICACION = c.USUARIO_MODIFICACION,
                    FECHA_MODIFICACION = c.FECHA_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm);
        }

        // ============================================================
        // CREATE (GET) – RUTA: GET /TiposCliente/Create
        // Muestra formulario con ID generado.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nuevoId = await SiguienteTipoClienteIdAsync(); // CA + 8 dígitos

            var vm = new TipoClienteCreateVM
            {
                TC_ID = nuevoId,
                ESTADO = true
            };
            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /TiposCliente/Create
        // ▸ Recalcula ID en servidor
        // ▸ Valida campos y unicidad por nombre (no eliminado)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TipoClienteCreateVM vm)
        {
            // Recalcular ID en servidor (seguridad)
            var nuevoId = await SiguienteTipoClienteIdAsync();
            vm.TC_ID = nuevoId;

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.TC_NOMBRE))
                ModelState.AddModelError(nameof(vm.TC_NOMBRE), "El nombre es obligatorio.");

            // Unicidad por nombre (no eliminado)
            string nombre = (vm.TC_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool nombreDuplicado = await _context.TIPO_CLIENTE
                    .AnyAsync(c => !c.ELIMINADO && c.TIPO_CLIENTE_NOMBRE == nombre);
                if (nombreDuplicado)
                    ModelState.AddModelError(nameof(vm.TC_NOMBRE), "Ya existe un Tipo de Cliente con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Normalizar strings -> null si vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            try
            {
                var c = new TIPO_CLIENTE
                {
                    TIPO_CLIENTE_ID = nuevoId,
                    TIPO_CLIENTE_NOMBRE = nombre,
                    TIPO_CLIENTE_DESCRIPCION = s2(vm.TC_DESCRIPCION),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(c); // auditoría de creación
                _context.TIPO_CLIENTE.Add(c);
                await _context.SaveChangesAsync();

                // PRG + SweetAlert (mismo patrón que Empleados)
                TempData["SwalTitle"] = "¡Tipo de Cliente guardado!";
                TempData["SwalText"] = $"El registro \"{c.TIPO_CLIENTE_NOMBRE}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "TiposCliente");
                TempData["SwalCreateUrl"] = Url.Action("Create", "TiposCliente");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                ModelState.AddModelError("", "Ocurrió un error al crear el tipo de cliente. Intenta nuevamente.");
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /TiposCliente/Edit/{id}
        // Carga entidad y llena VM.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.TIPO_CLIENTE
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TIPO_CLIENTE_ID == id && !x.ELIMINADO);

            if (c == null) return NotFound();

            var vm = new TipoClienteCreateVM
            {
                TC_ID = c.TIPO_CLIENTE_ID,
                TC_NOMBRE = c.TIPO_CLIENTE_NOMBRE,
                TC_DESCRIPCION = c.TIPO_CLIENTE_DESCRIPCION,
                ESTADO = c.ESTADO
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /TiposCliente/Edit/{id}
        // ▸ Valida campos y unicidad por nombre (excluye el propio).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, TipoClienteCreateVM vm)
        {
            if (id != vm.TC_ID) return NotFound();

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.TC_NOMBRE))
                ModelState.AddModelError(nameof(vm.TC_NOMBRE), "El nombre es obligatorio.");

            string nombre = (vm.TC_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool duplicado = await _context.TIPO_CLIENTE
                    .AnyAsync(c => !c.ELIMINADO && c.TIPO_CLIENTE_NOMBRE == nombre && c.TIPO_CLIENTE_ID != id);
                if (duplicado)
                    ModelState.AddModelError(nameof(vm.TC_NOMBRE), "Ya existe un tipo de cliente con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Buscar la entidad original
            var c = await _context.TIPO_CLIENTE
                .FirstOrDefaultAsync(x => x.TIPO_CLIENTE_ID == id && !x.ELIMINADO);
            if (c == null) return NotFound();

            // Normalizar
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Aplicar cambios
            c.TIPO_CLIENTE_NOMBRE = nombre;
            c.TIPO_CLIENTE_DESCRIPCION = s2(vm.TC_DESCRIPCION);
            c.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(c);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Tipo de Cliente actualizado!";
            TempData["SwalText"] = $"\"{c.TIPO_CLIENTE_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = c.TIPO_CLIENTE_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico
        // Rutas:
        //   GET  /TiposCliente/Delete/{id}
        //   POST /TiposCliente/Delete/{id}  (ActionName("Delete"))
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.TIPO_CLIENTE
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TIPO_CLIENTE_ID == id);

            if (c == null) return NotFound();

            return View(c);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var c = await _context.TIPO_CLIENTE.FindAsync(id);
            if (c == null) return NotFound();

            _audit.StampSoftDelete(c); // tipo de cliente ELIMINADO = 1 + auditoría
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo TC00000001 para Tipo Cliente
        private async Task<string> SiguienteTipoClienteIdAsync()
        {
            const string prefijo = "TC";
            const int ancho = 8;

            // Traemos los IDs existentes que empiezan con "MA"
            var ids = await _context.TIPO_CLIENTE
                .Select(x => x.TIPO_CLIENTE_ID)
                .Where(id => id.StartsWith(prefijo))
                .ToListAsync();

            int maxNum = 0;
            var rx = new Regex(@"^" + prefijo + @"(?<n>\d+)$");
            foreach (var id in ids)
            {
                var m = rx.Match(id);
                if (m.Success && int.TryParse(m.Groups["n"].Value, out int n))
                    if (n > maxNum) maxNum = n;
            }

            var siguiente = maxNum + 1;
            return prefijo + siguiente.ToString(new string('0', ancho));
        }
    }
}
