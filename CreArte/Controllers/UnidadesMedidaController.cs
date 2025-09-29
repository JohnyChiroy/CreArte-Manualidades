using CreArte.Data;                 // DbContext
using CreArte.Models;               // Entidades (UNIDAD_MEDIDA)
using CreArte.ModelsPartial;        // ViewModels de UNIDAD_MEDIDA
using CreArte.Services.Auditoria;   // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class UnidadesMedidaController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public UnidadesMedidaController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /UnidadesMedida?...
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
            IQueryable<UNIDAD_MEDIDA> q = _context.UNIDAD_MEDIDA.Where(c => !c.ELIMINADO);

            // 2) Búsqueda global (por ID, Nombre, Descripción)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(c =>
                    EF.Functions.Like(c.UNIDAD_MEDIDA_ID, $"%{s}%") ||
                    EF.Functions.Like(c.UNIDAD_MEDIDA_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(c.UNIDAD_MEDIDA_DESCRIPCION ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por NOMBRE (texto exacto/parcial)
            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                var n = Nombre.Trim();
                q = q.Where(c => EF.Functions.Like(c.UNIDAD_MEDIDA_NOMBRE, $"%{n}%"));
            }

            // 4) Filtro por ESTADO
            if (Estado.HasValue)
                q = q.Where(c => c.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(c => c.UNIDAD_MEDIDA_ID) : q.OrderByDescending(c => c.UNIDAD_MEDIDA_ID),
                "nombre" => asc ? q.OrderBy(c => c.UNIDAD_MEDIDA_NOMBRE) : q.OrderByDescending(c => c.UNIDAD_MEDIDA_NOMBRE),
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
            var vm = new UnidadMedidaViewModels
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
        // DETAILS (tarjeta/modal) – GET /UnidadesMedida/DetailsCard?id=...
        // ▸ Devuelve PartialView("Details", vm)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.UNIDAD_MEDIDA
                .AsNoTracking()
                .Where(c => c.UNIDAD_MEDIDA_ID == id && !c.ELIMINADO)
                .Select(c => new UnidadMedidaDetailsVM
                {
                    UNIDAD_MEDIDA_ID = c.UNIDAD_MEDIDA_ID,
                    UNIDAD_MEDIDA_NOMBRE = c.UNIDAD_MEDIDA_NOMBRE,
                    UNIDAD_MEDIDA_DESCRIPCION = c.UNIDAD_MEDIDA_DESCRIPCION,
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
        // CREATE (GET) – RUTA: GET /UnidadesMedida/Create
        // Muestra formulario con ID generado.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nuevoId = await SiguienteUnidadMedidaIdAsync(); // CA + 8 dígitos

            var vm = new UnidadMedidaCreateVM
            {
                UNIDAD_MEDIDA_ID = nuevoId,
                ESTADO = true
            };
            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /UnidadesMedida/Create
        // ▸ Recalcula ID en servidor
        // ▸ Valida campos y unicidad por nombre (no eliminado)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UnidadMedidaCreateVM vm)
        {
            // Recalcular ID en servidor (seguridad)
            var nuevoId = await SiguienteUnidadMedidaIdAsync();
            vm.UNIDAD_MEDIDA_ID = nuevoId;

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.UNIDAD_MEDIDA_NOMBRE))
                ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_NOMBRE), "El nombre es obligatorio.");

            // Unicidad por nombre (no eliminado)
            string nombre = (vm.UNIDAD_MEDIDA_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool nombreDuplicado = await _context.UNIDAD_MEDIDA
                    .AnyAsync(c => !c.ELIMINADO && c.UNIDAD_MEDIDA_NOMBRE == nombre);
                if (nombreDuplicado)
                    ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_NOMBRE), "Ya existe una unidad de medida con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Normalizar strings -> null si vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            try
            {
                var c = new UNIDAD_MEDIDA
                {
                    UNIDAD_MEDIDA_ID = nuevoId,
                    UNIDAD_MEDIDA_NOMBRE = nombre,
                    UNIDAD_MEDIDA_DESCRIPCION = s2(vm.UNIDAD_MEDIDA_DESCRIPCION),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(c); // auditoría de creación
                _context.UNIDAD_MEDIDA.Add(c);
                await _context.SaveChangesAsync();

                // PRG + SweetAlert (mismo patrón que Empleados)
                TempData["SwalTitle"] = "¡Unidad de Medida guardada!";
                TempData["SwalText"] = $"El registro \"{c.UNIDAD_MEDIDA_NOMBRE}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "UnidadesMedida");
                TempData["SwalCreateUrl"] = Url.Action("Create", "UnidadesMedida");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                ModelState.AddModelError("", "Ocurrió un error al crear la unidad de medida. Intenta nuevamente.");
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /UnidadesMedida/Edit/{id}
        // Carga entidad y llena VM.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.UNIDAD_MEDIDA
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UNIDAD_MEDIDA_ID== id && !x.ELIMINADO);

            if (c == null) return NotFound();

            var vm = new UnidadMedidaCreateVM
            {
                UNIDAD_MEDIDA_ID = c.UNIDAD_MEDIDA_ID,
                UNIDAD_MEDIDA_NOMBRE = c.UNIDAD_MEDIDA_NOMBRE,
                UNIDAD_MEDIDA_DESCRIPCION = c.UNIDAD_MEDIDA_DESCRIPCION,
                ESTADO = c.ESTADO
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /UnidadesMedida/Edit/{id}
        // ▸ Valida campos y unicidad por nombre (excluye el propio).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UnidadMedidaCreateVM vm)
        {
            if (id != vm.UNIDAD_MEDIDA_ID) return NotFound();

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.UNIDAD_MEDIDA_NOMBRE))
                ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_NOMBRE), "El nombre es obligatorio.");

            string nombre = (vm.UNIDAD_MEDIDA_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool duplicado = await _context.UNIDAD_MEDIDA
                    .AnyAsync(c => !c.ELIMINADO && c.UNIDAD_MEDIDA_NOMBRE == nombre && c.UNIDAD_MEDIDA_ID != id);
                if (duplicado)
                    ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_NOMBRE), "Ya existe una unidad de medida con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Buscar la entidad original
            var c = await _context.UNIDAD_MEDIDA
                .FirstOrDefaultAsync(x => x.UNIDAD_MEDIDA_ID == id && !x.ELIMINADO);
            if (c == null) return NotFound();

            // Normalizar
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Aplicar cambios
            c.UNIDAD_MEDIDA_NOMBRE = nombre;
            c.UNIDAD_MEDIDA_DESCRIPCION = s2(vm.UNIDAD_MEDIDA_DESCRIPCION);
            c.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(c);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Unidad de Medida actualizada!";
            TempData["SwalText"] = $"\"{c.UNIDAD_MEDIDA_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = c.UNIDAD_MEDIDA_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico
        // Rutas:
        //   GET  /UnidadesMedida/Delete/{id}
        //   POST /UnidadesMedida/Delete/{id}  (ActionName("Delete"))
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.UNIDAD_MEDIDA
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UNIDAD_MEDIDA_ID == id);

            if (c == null) return NotFound();

            return View(c);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var c = await _context.UNIDAD_MEDIDA.FindAsync(id);
            if (c == null) return NotFound();

            _audit.StampSoftDelete(c); // unidad medida ELIMINADO = 1 + auditoría
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo UM00000001 para UNIDAD_MEDIDA
        private async Task<string> SiguienteUnidadMedidaIdAsync()
        {
            const string prefijo = "UM";
            const int ancho = 8;

            // Traemos los IDs existentes que empiezan con "UM"
            var ids = await _context.UNIDAD_MEDIDA
                .Select(x => x.UNIDAD_MEDIDA_ID)
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
