// ===============================================
// RUTA: Controllers/RolesController.cs
// DESCRIPCIÓN: CRUD de ROL con filtros/orden/paginación,
//              validaciones, auditoría (IAuditoriaService) y
//              borrado lógico. Estructura y estilos alineados
//              con EmpleadosController.
// ===============================================
using CreArte.Data;                 // CreArteDbContext
using CreArte.Models;               // Entidad ROL (EF)
using CreArte.ModelsPartial;        // RolViewModels
using CreArte.Services.Auditoria;   // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class RolesController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public RolesController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // INDEX/LISTADO – RUTA: GET /Roles?Search=...&Estado=...&Sort=...&Dir=...&Page=...&PageSize=...
        // ▸ Filtros: Search (id/nombre), Estado
        // ▸ Orden: id, nombre, estado, fecha
        // ▸ Paginación: Page/PageSize
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? Search,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Base: sólo no eliminados
            IQueryable<ROL> q = _context.ROL.Where(r => !r.ELIMINADO);

            // 2) Búsqueda global (ID + Nombre)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(r =>
                    EF.Functions.Like(r.ROL_ID, $"%{s}%") ||
                    EF.Functions.Like(r.ROL_NOMBRE, $"%{s}%")
                );
            }

            // 3) Filtro por Estado
            if (Estado.HasValue)
                q = q.Where(r => r.ESTADO == Estado.Value);

            // 4) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(r => r.ROL_ID) : q.OrderByDescending(r => r.ROL_ID),
                "nombre" => asc ? q.OrderBy(r => r.ROL_NOMBRE) : q.OrderByDescending(r => r.ROL_NOMBRE),
                "estado" => asc ? q.OrderBy(r => r.ESTADO) : q.OrderByDescending(r => r.ESTADO),
                _ => asc ? q.OrderBy(r => r.FECHA_CREACION) : q.OrderByDescending(r => r.FECHA_CREACION),
            };

            // 5) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            var items = await q
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 6) VM de salida
            var vm = new RolViewModels
            {
                Items = items,
                Search = Search,
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
        // DETAILS (Partial para modal) – RUTA: GET /Roles/DetailsCard?id=...
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.ROL
                .AsNoTracking()
                .Where(r => r.ROL_ID == id && !r.ELIMINADO)
                .Select(r => new RolDetailsVM
                {
                    ROL_ID = r.ROL_ID,
                    ROL_NOMBRE = r.ROL_NOMBRE,
                    ROL_DESCRIPCION = r.ROL_DESCRIPCION,
                    ESTADO = r.ESTADO,

                    // Auditoría (para mostrar en la tarjeta si así lo deseas)
                    USUARIO_CREACION = r.USUARIO_CREACION,
                    FECHA_CREACION = r.FECHA_CREACION,
                    USUARIO_MODIFICACION = r.USUARIO_MODIFICACION,
                    FECHA_MODIFICACION = r.FECHA_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm); // Vista parcial: Views/Roles/Details.cshtml
        }

        // ============================================================
        // CREATE (GET) – RUTA: GET /Roles/Create
        // ▸ Genera ID "RO00000001"
        // ▸ Estado por defecto: true
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nuevoId = await SiguienteRolIdAsync();
            var vm = new RolCreateVM
            {
                ROL_ID = nuevoId,
                ESTADO = true
            };
            return View(vm); // Vista: Views/Roles/Create.cshtml
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /Roles/Create
        // ▸ Valida requeridos y unicidad de nombre
        // ▸ Auditoría de creación
        // ▸ PRG + SweetAlert (TempData)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RolCreateVM vm)
        {
            // Revalidar/generar ID en servidor (seguridad)
            vm.ROL_ID = await SiguienteRolIdAsync();

            // ------- VALIDACIONES -------
            if (string.IsNullOrWhiteSpace(vm.ROL_NOMBRE))
                ModelState.AddModelError(nameof(vm.ROL_NOMBRE), "El nombre del rol es obligatorio.");

            string nombreNorm = (vm.ROL_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombreNorm))
            {
                bool dup = await _context.ROL.AnyAsync(r => !r.ELIMINADO && r.ROL_NOMBRE == nombreNorm);
                if (dup)
                    ModelState.AddModelError(nameof(vm.ROL_NOMBRE), "Ya existe un rol con este nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Normalizador: string vacío -> null
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            var rol = new ROL
            {
                ROL_ID = vm.ROL_ID!,
                ROL_NOMBRE = nombreNorm,
                ROL_DESCRIPCION = s2(vm.ROL_DESCRIPCION),
                ESTADO = vm.ESTADO,
                ELIMINADO = false
            };

            _audit.StampCreate(rol);      // auditoría creación
            _context.ROL.Add(rol);
            await _context.SaveChangesAsync();

            // PRG + SweetAlert (mismo patrón que Empleados)
            TempData["SwalTitle"] = "¡Rol guardado!";
            TempData["SwalText"] = $"El rol \"{rol.ROL_NOMBRE}\" se creó correctamente.";
            TempData["SwalIndexUrl"] = Url.Action("Index", "Roles");
            TempData["SwalCreateUrl"] = Url.Action("Create", "Roles");
            return RedirectToAction(nameof(Create));
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /Roles/Edit/{id}
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var r = await _context.ROL
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ROL_ID == id && !x.ELIMINADO);

            if (r == null) return NotFound();

            var vm = new RolCreateVM
            {
                ROL_ID = r.ROL_ID,
                ROL_NOMBRE = r.ROL_NOMBRE,
                ROL_DESCRIPCION = r.ROL_DESCRIPCION,
                ESTADO = r.ESTADO
            };

            return View(vm); // Vista: Views/Roles/Edit.cshtml
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /Roles/Edit/{id}
        // ▸ Valida requeridos y unicidad de nombre (excluyendo propio)
        // ▸ Auditoría de modificación
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, RolCreateVM vm)
        {
            if (id != vm.ROL_ID) return NotFound();

            if (string.IsNullOrWhiteSpace(vm.ROL_NOMBRE))
                ModelState.AddModelError(nameof(vm.ROL_NOMBRE), "El nombre del rol es obligatorio.");

            string nombreNorm = (vm.ROL_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombreNorm))
            {
                bool dup = await _context.ROL.AnyAsync(r =>
                    !r.ELIMINADO && r.ROL_NOMBRE == nombreNorm && r.ROL_ID != id);
                if (dup)
                    ModelState.AddModelError(nameof(vm.ROL_NOMBRE), "Ya existe un rol con este nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            var r = await _context.ROL.FirstOrDefaultAsync(x => x.ROL_ID == id && !x.ELIMINADO);
            if (r == null) return NotFound();

            // Normalizador
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Aplicar cambios
            r.ROL_NOMBRE = nombreNorm;
            r.ROL_DESCRIPCION = s2(vm.ROL_DESCRIPCION);
            r.ESTADO = vm.ESTADO;

            _audit.StampUpdate(r); // auditoría modificación
            await _context.SaveChangesAsync();

            // PRG (+ bandera opcional para modal 1 botón como en Empleados)
            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Rol actualizado!";
            TempData["SwalText"] = $"\"{r.ROL_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = r.ROL_ID });
        }

        // ============================================================
        // DELETE – RUTA: GET /Roles/Delete/{id} y POST /Roles/Delete/{id}
        // ▸ Borrado lógico (ELIMINADO = 1 + auditoría de eliminación)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var r = await _context.ROL
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ROL_ID == id);

            if (r == null) return NotFound();
            return View(r); // Vista: Views/Roles/Delete.cshtml
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var r = await _context.ROL.FindAsync(id);
            if (r == null) return NotFound();

            _audit.StampSoftDelete(r); // marca ELIMINADO=1 + usuario/fecha
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo RO00000001 para ROL
        private async Task<string> SiguienteRolIdAsync()
        {
            const string prefijo = "RO";
            const int ancho = 8;

            var ids = await _context.ROL
                .Select(r => r.ROL_ID)
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
