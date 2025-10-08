using CreArte.Data;                 // DbContext
using CreArte.Models;               // Entidades (CATEGORIA)
using CreArte.ModelsPartial;        // ViewModels de Categoría
using CreArte.Services.Auditoria;   // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class CategoriasController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public CategoriasController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /Categorias?...
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
            IQueryable<CATEGORIA> q = _context.CATEGORIA.Where(c => !c.ELIMINADO);

            // 2) Búsqueda global (por ID, Nombre, Descripción)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(c =>
                    EF.Functions.Like(c.CATEGORIA_ID, $"%{s}%") ||
                    EF.Functions.Like(c.CATEGORIA_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(c.CATEGORIA_DESCRIPCION ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por NOMBRE (texto exacto/parcial)
            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                var n = Nombre.Trim();
                q = q.Where(c => EF.Functions.Like(c.CATEGORIA_NOMBRE, $"%{n}%"));
            }

            // 4) Filtro por ESTADO
            if (Estado.HasValue)
                q = q.Where(c => c.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(c => c.CATEGORIA_ID) : q.OrderByDescending(c => c.CATEGORIA_ID),
                "nombre" => asc ? q.OrderBy(c => c.CATEGORIA_NOMBRE) : q.OrderByDescending(c => c.CATEGORIA_NOMBRE),
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
            var vm = new CategoriaViewModels
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
        // DETAILS (tarjeta/modal) – GET /Categorias/DetailsCard?id=...
        // ▸ Devuelve PartialView("Details", vm)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.CATEGORIA
                .AsNoTracking()
                .Where(c => c.CATEGORIA_ID == id && !c.ELIMINADO)
                .Select(c => new CategoriaDetailsVM
                {
                    CATEGORIA_ID = c.CATEGORIA_ID,
                    CATEGORIA_NOMBRE = c.CATEGORIA_NOMBRE,
                    CATEGORIA_DESCRIPCION = c.CATEGORIA_DESCRIPCION,
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
        // CREATE (GET) – RUTA: GET /Categorias/Create
        // Muestra formulario con ID generado.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nuevoId = await SiguienteCategoriaIdAsync(); // CA + 8 dígitos

            var vm = new CategoriaCreateVM
            {
                CATEGORIA_ID = nuevoId,
                ESTADO = true
            };
            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /Categorias/Create
        // ▸ Recalcula ID en servidor
        // ▸ Valida campos y unicidad por nombre (no eliminado)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoriaCreateVM vm)
        {
            // Recalcular ID en servidor (seguridad)
            var nuevoId = await SiguienteCategoriaIdAsync();
            vm.CATEGORIA_ID = nuevoId;

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.CATEGORIA_NOMBRE))
                ModelState.AddModelError(nameof(vm.CATEGORIA_NOMBRE), "El nombre es obligatorio.");

            // Unicidad por nombre (no eliminado)
            string nombre = (vm.CATEGORIA_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool nombreDuplicado = await _context.CATEGORIA
                    .AnyAsync(c => !c.ELIMINADO && c.CATEGORIA_NOMBRE == nombre);
                if (nombreDuplicado)
                    ModelState.AddModelError(nameof(vm.CATEGORIA_NOMBRE), "Ya existe una categoría con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Normalizar strings -> null si vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            try
            {
                var c = new CATEGORIA
                {
                    CATEGORIA_ID = nuevoId,
                    CATEGORIA_NOMBRE = nombre,
                    CATEGORIA_DESCRIPCION = s2(vm.CATEGORIA_DESCRIPCION),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(c); // auditoría de creación
                _context.CATEGORIA.Add(c);
                await _context.SaveChangesAsync();

                // PRG + SweetAlert (mismo patrón que Empleados)
                TempData["SwalTitle"] = "¡Categoría guardada!";
                TempData["SwalText"] = $"El registro \"{c.CATEGORIA_NOMBRE}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "Categorias");
                TempData["SwalCreateUrl"] = Url.Action("Create", "Categorias");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                ModelState.AddModelError("", "Ocurrió un error al crear la categoría. Intenta nuevamente.");
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /Categorias/Edit/{id}
        // Carga entidad y llena VM.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.CATEGORIA
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CATEGORIA_ID == id && !x.ELIMINADO);

            if (c == null) return NotFound();

            var vm = new CategoriaCreateVM
            {
                CATEGORIA_ID = c.CATEGORIA_ID,
                CATEGORIA_NOMBRE = c.CATEGORIA_NOMBRE,
                CATEGORIA_DESCRIPCION = c.CATEGORIA_DESCRIPCION,
                ESTADO = c.ESTADO
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /Categorias/Edit/{id}
        // ▸ Valida campos y unicidad por nombre (excluye el propio).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CategoriaCreateVM vm)
        {
            if (id != vm.CATEGORIA_ID) return NotFound();

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.CATEGORIA_NOMBRE))
                ModelState.AddModelError(nameof(vm.CATEGORIA_NOMBRE), "El nombre es obligatorio.");

            string nombre = (vm.CATEGORIA_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool duplicado = await _context.CATEGORIA
                    .AnyAsync(c => !c.ELIMINADO && c.CATEGORIA_NOMBRE == nombre && c.CATEGORIA_ID != id);
                if (duplicado)
                    ModelState.AddModelError(nameof(vm.CATEGORIA_NOMBRE), "Ya existe una categoría con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Buscar la entidad original
            var c = await _context.CATEGORIA
                .FirstOrDefaultAsync(x => x.CATEGORIA_ID == id && !x.ELIMINADO);
            if (c == null) return NotFound();

            // Normalizar
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Aplicar cambios
            c.CATEGORIA_NOMBRE = nombre;
            c.CATEGORIA_DESCRIPCION = s2(vm.CATEGORIA_DESCRIPCION);
            c.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(c);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Categoría actualizada!";
            TempData["SwalText"] = $"\"{c.CATEGORIA_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = c.CATEGORIA_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico
        // Rutas:
        //   GET  /Categorias/Delete/{id}
        //   POST /Categorias/Delete/{id}  (ActionName("Delete"))
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.CATEGORIA
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CATEGORIA_ID == id);

            if (c == null) return NotFound();

            return View(c);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var c = await _context.CATEGORIA.FindAsync(id);
            if (c == null) return NotFound();

            _audit.StampSoftDelete(c); // marca ELIMINADO = 1 + auditoría
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo CA00000001 para CATEGORIA
        private async Task<string> SiguienteCategoriaIdAsync()
        {
            const string prefijo = "CA";
            const int ancho = 8;

            // Traemos los IDs existentes que empiezan con "CA"
            var ids = await _context.CATEGORIA
                .Select(x => x.CATEGORIA_ID)
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
