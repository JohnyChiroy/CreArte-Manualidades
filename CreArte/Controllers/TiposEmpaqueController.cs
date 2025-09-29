using CreArte.Data;                 // DbContext
using CreArte.Models;               // Entidades (TIPO_EMPAQUE)
using CreArte.ModelsPartial;        // ViewModels de TipoEmpaque
using CreArte.Services.Auditoria;   // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class TiposEmpaqueController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public TiposEmpaqueController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /TiposEmpaque?...
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
            IQueryable<TIPO_EMPAQUE> q = _context.TIPO_EMPAQUE.Where(c => !c.ELIMINADO);

            // 2) Búsqueda global (por ID, Nombre, Descripción)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(c =>
                    EF.Functions.Like(c.TIPO_EMPAQUE_ID, $"%{s}%") ||
                    EF.Functions.Like(c.TIPO_EMPAQUE_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(c.TIPO_EMPAQUE_DESCRIPCION ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por NOMBRE (texto exacto/parcial)
            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                var n = Nombre.Trim();
                q = q.Where(c => EF.Functions.Like(c.TIPO_EMPAQUE_NOMBRE, $"%{n}%"));
            }

            // 4) Filtro por ESTADO
            if (Estado.HasValue)
                q = q.Where(c => c.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(c => c.TIPO_EMPAQUE_ID) : q.OrderByDescending(c => c.TIPO_EMPAQUE_ID),
                "nombre" => asc ? q.OrderBy(c => c.TIPO_EMPAQUE_NOMBRE) : q.OrderByDescending(c => c.TIPO_EMPAQUE_NOMBRE),
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
            var vm = new TipoEmpaqueViewModels
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
        // DETAILS (tarjeta/modal) – GET /TiposEmpaque/DetailsCard?id=...
        // ▸ Devuelve PartialView("Details", vm)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.TIPO_EMPAQUE
                .AsNoTracking()
                .Where(c => c.TIPO_EMPAQUE_ID == id && !c.ELIMINADO)
                .Select(c => new TipoEmpaqueDetailsVM
                {
                    TIPO_EMPAQUE_ID = c.TIPO_EMPAQUE_ID,
                    TIPO_EMPAQUE_NOMBRE = c.TIPO_EMPAQUE_NOMBRE,
                    TIPO_EMPAQUE_DESCRIPCION = c.TIPO_EMPAQUE_DESCRIPCION,
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
        // CREATE (GET) – RUTA: GET /TiposEmpaque/Create
        // Muestra formulario con ID generado.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nuevoId = await SiguienteTipoEmpaqueIdAsync(); // CA + 8 dígitos

            var vm = new TipoEmpaqueCreateVM
            {
                TIPO_EMPAQUE_ID = nuevoId,
                ESTADO = true
            };
            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /TiposEmpaque/Create
        // ▸ Recalcula ID en servidor
        // ▸ Valida campos y unicidad por nombre (no eliminado)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TipoEmpaqueCreateVM vm)
        {
            // Recalcular ID en servidor (seguridad)
            var nuevoId = await SiguienteTipoEmpaqueIdAsync();
            vm.TIPO_EMPAQUE_ID = nuevoId;

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.TIPO_EMPAQUE_NOMBRE))
                ModelState.AddModelError(nameof(vm.TIPO_EMPAQUE_NOMBRE), "El nombre es obligatorio.");

            // Unicidad por nombre (no eliminado)
            string nombre = (vm.TIPO_EMPAQUE_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool nombreDuplicado = await _context.TIPO_EMPAQUE
                    .AnyAsync(c => !c.ELIMINADO && c.TIPO_EMPAQUE_NOMBRE == nombre);
                if (nombreDuplicado)
                    ModelState.AddModelError(nameof(vm.TIPO_EMPAQUE_NOMBRE), "Ya existe un tipo de empaque con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Normalizar strings -> null si vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            try
            {
                var c = new TIPO_EMPAQUE
                {
                    TIPO_EMPAQUE_ID = nuevoId,
                    TIPO_EMPAQUE_NOMBRE = nombre,
                    TIPO_EMPAQUE_DESCRIPCION = s2(vm.TIPO_EMPAQUE_DESCRIPCION),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(c); // auditoría de creación
                _context.TIPO_EMPAQUE.Add(c);
                await _context.SaveChangesAsync();

                // PRG + SweetAlert (mismo patrón que Empleados)
                TempData["SwalTitle"] = "¡Tipo Empaque guardado!";
                TempData["SwalText"] = $"El registro \"{c.TIPO_EMPAQUE_NOMBRE}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "TiposEmpaque");
                TempData["SwalCreateUrl"] = Url.Action("Create", "TiposEmpaque");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                ModelState.AddModelError("", "Ocurrió un error al crear el tipo de empaque. Intenta nuevamente.");
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /TiposEmpaque/Edit/{id}
        // Carga entidad y llena VM.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.TIPO_EMPAQUE
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TIPO_EMPAQUE_ID == id && !x.ELIMINADO);

            if (c == null) return NotFound();

            var vm = new TipoEmpaqueCreateVM
            {
                TIPO_EMPAQUE_ID = c.TIPO_EMPAQUE_ID,
                TIPO_EMPAQUE_NOMBRE = c.TIPO_EMPAQUE_NOMBRE,
                TIPO_EMPAQUE_DESCRIPCION = c.TIPO_EMPAQUE_DESCRIPCION,
                ESTADO = c.ESTADO
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /TiposEmpaque/Edit/{id}
        // ▸ Valida campos y unicidad por nombre (excluye el propio).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, TipoEmpaqueCreateVM vm)
        {
            if (id != vm.TIPO_EMPAQUE_ID) return NotFound();

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.TIPO_EMPAQUE_NOMBRE))
                ModelState.AddModelError(nameof(vm.TIPO_EMPAQUE_NOMBRE), "El nombre es obligatorio.");

            string nombre = (vm.TIPO_EMPAQUE_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool duplicado = await _context.TIPO_EMPAQUE
                    .AnyAsync(c => !c.ELIMINADO && c.TIPO_EMPAQUE_NOMBRE == nombre && c.TIPO_EMPAQUE_ID != id);
                if (duplicado)
                    ModelState.AddModelError(nameof(vm.TIPO_EMPAQUE_NOMBRE), "Ya existe un tipo de empaque con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Buscar la entidad original
            var c = await _context.TIPO_EMPAQUE
                .FirstOrDefaultAsync(x => x.TIPO_EMPAQUE_ID == id && !x.ELIMINADO);
            if (c == null) return NotFound();

            // Normalizar
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Aplicar cambios
            c.TIPO_EMPAQUE_NOMBRE = nombre;
            c.TIPO_EMPAQUE_DESCRIPCION = s2(vm.TIPO_EMPAQUE_DESCRIPCION);
            c.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(c);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Tipo de Empaque actualizado!";
            TempData["SwalText"] = $"\"{c.TIPO_EMPAQUE_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = c.TIPO_EMPAQUE_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico
        // Rutas:
        //   GET  /TiposEmpaque/Delete/{id}
        //   POST /TiposEmpaque/Delete/{id}  (ActionName("Delete"))
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.TIPO_PRODUCTO
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TIPO_PRODUCTO_ID == id);

            if (c == null) return NotFound();

            return View(c);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var c = await _context.TIPO_EMPAQUE.FindAsync(id);
            if (c == null) return NotFound();

            _audit.StampSoftDelete(c); // tipo empaque ELIMINADO = 1 + auditoría
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo TE00000001 para TIPO_EMPAQUE
        private async Task<string> SiguienteTipoEmpaqueIdAsync()
        {
            const string prefijo = "TE";
            const int ancho = 8;

            // Traemos los IDs existentes que empiezan con "MA"
            var ids = await _context.TIPO_EMPAQUE
                .Select(x => x.TIPO_EMPAQUE_ID)
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
