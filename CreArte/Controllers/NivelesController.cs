// ===============================================
// RUTA: Controllers/NivelesController.cs
// DESCRIPCIÓN: CRUD de NIVEL con borrado lógico,
//              filtros/orden/paginación y auditoría centralizada
// ===============================================
using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CreArte.Services.Auditoria; // IAuditoriaService

namespace CreArte.Controllers
{
    public class NivelesController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public NivelesController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO:
        // GET /Niveles?Search=...&Nivel=...&FechaInicio=...&FechaFin=...&Estado=...&Sort=...&Dir=...&Page=1&PageSize=10
        // ============================================================
        public async Task<IActionResult> Index(
            string Search,
            string Nivel,            // filtro puntual por nombre
            DateTime? FechaInicio,   // rango de fechas por FECHA_CREACION
            DateTime? FechaFin,
            bool? Estado,            // true/false/null
            string Sort = "id",      // "id","nivel","fecha","estado"
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Base IQueryable<NIVEL> sólo NO eliminados
            IQueryable<NIVEL> q = _context.NIVEL.Where(n => !n.ELIMINADO);

            // 2) Búsqueda global (ID, NOMBRE)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(n =>
                    EF.Functions.Like(n.NIVEL_ID, $"%{s}%") ||
                    EF.Functions.Like(n.NIVEL_NOMBRE, $"%{s}%"));
            }

            // 3) Filtro por NIVEL (texto / vacíos)
            if (!string.IsNullOrWhiteSpace(Nivel))
            {
                if (Nivel == "__BLANKS__")
                    q = q.Where(n => string.IsNullOrEmpty(n.NIVEL_NOMBRE));
                else if (Nivel == "__NONBLANKS__")
                    q = q.Where(n => !string.IsNullOrEmpty(n.NIVEL_NOMBRE));
                else
                {
                    string s = Nivel.Trim();
                    q = q.Where(n => EF.Functions.Like(n.NIVEL_NOMBRE, $"%{s}%"));
                }
            }

            // 4) Filtro por fechas (creación)
            if (FechaInicio.HasValue)
                q = q.Where(n => n.FECHA_CREACION >= FechaInicio.Value.Date);

            if (FechaFin.HasValue)
                q = q.Where(n => n.FECHA_CREACION < FechaFin.Value.Date.AddDays(1));

            // 5) Filtro por estado
            if (Estado.HasValue)
                q = q.Where(n => n.ESTADO == Estado.Value);

            // 6) Ordenamiento
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(n => n.NIVEL_ID) : q.OrderByDescending(n => n.NIVEL_ID),
                "nivel" => asc ? q.OrderBy(n => n.NIVEL_NOMBRE) : q.OrderByDescending(n => n.NIVEL_NOMBRE),
                "estado" => asc ? q.OrderBy(n => n.ESTADO) : q.OrderByDescending(n => n.ESTADO),
                _ => asc ? q.OrderBy(n => n.FECHA_CREACION) : q.OrderByDescending(n => n.FECHA_CREACION)
            };

            // 7) Paginación (total)
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // 8) Materializar la página
            var items = await q
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 9) ViewModel para la vista
            var vm = new NivelViewModels
            {
                Items = items,
                Search = Search,
                Nivel = Nivel,
                FechaInicio = FechaInicio,
                FechaFin = FechaFin,
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
        // DETAILS para modal SweetAlert2 (PartialView)
        // GET: /Niveles/DetailsCard?id=NV00000001
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.NIVEL
                .AsNoTracking()
                .Where(n => n.NIVEL_ID == id && !n.ELIMINADO)
                .Select(n => new NivelDetailsVM
                {
                    NIVEL_ID = n.NIVEL_ID,
                    NIVEL_NOMBRE = n.NIVEL_NOMBRE,
                    NIVEL_DESCRIPCION = n.NIVEL_DESCRIPCION,
                    ESTADO = n.ESTADO,
                    FECHA_CREACION = n.FECHA_CREACION,
                    USUARIO_CREACION = n.USUARIO_CREACION,
                    FECHA_MODIFICACION = n.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = n.USUARIO_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();

            return PartialView("Details", vm);
        }

        // =====================================
        // CREATE (GET)
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new NivelCreateVM
            {
                NIVEL_ID = await SiguienteNivelIdAsync(), // prefijo NV + 8 dígitos
                ESTADO = true
            };
            return View(vm);
        }

        // =====================================
        // CREATE (POST) con auditoría centralizada
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NivelCreateVM vm)
        {
            // Re-generamos el ID en servidor (seguridad)
            vm.NIVEL_ID = await SiguienteNivelIdAsync();

            if (!ModelState.IsValid)
                return View(vm);

            // Normalizar entradas
            var nombre = (vm.NIVEL_NOMBRE ?? string.Empty).Trim();
            var desc = string.IsNullOrWhiteSpace(vm.NIVEL_DESCRIPCION) ? null : vm.NIVEL_DESCRIPCION.Trim();

            // Unicidad: nombre de NIVEL único entre no eliminados (case-insensitive)
            bool duplicado = await _context.NIVEL.AnyAsync(n =>
                !n.ELIMINADO &&
                n.NIVEL_NOMBRE.ToLower() == nombre.ToLower());

            if (duplicado)
            {
                ModelState.AddModelError(nameof(vm.NIVEL_NOMBRE), "Ya existe un nivel con ese nombre.");
                return View(vm);
            }

            // Mapear VM -> Entidad
            var entidad = new NIVEL
            {
                NIVEL_ID = vm.NIVEL_ID,
                NIVEL_NOMBRE = nombre,
                NIVEL_DESCRIPCION = desc,
                ESTADO = vm.ESTADO
            };

            // Auditoría de creación
            _audit.StampCreate(entidad);

            _context.NIVEL.Add(entidad);
            await _context.SaveChangesAsync();

            // PRG con SweetAlert2 (2 botones: Index/Create)
            TempData["SwalTitle"] = "¡Nivel guardado!";
            TempData["SwalText"] = $"El registro \"{vm.NIVEL_NOMBRE}\" se creó correctamente.";
            TempData["SwalIndexUrl"] = Url.Action("Index", "Niveles");
            TempData["SwalCreateUrl"] = Url.Action("Create", "Niveles");

            return RedirectToAction(nameof(Create));
        }

        // =====================================
        // EDIT (GET)
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var n = await _context.NIVEL
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NIVEL_ID == id && !x.ELIMINADO);

            if (n == null) return NotFound();

            var vm = new NivelCreateVM
            {
                NIVEL_ID = n.NIVEL_ID,
                NIVEL_NOMBRE = n.NIVEL_NOMBRE,
                NIVEL_DESCRIPCION = n.NIVEL_DESCRIPCION,
                ESTADO = n.ESTADO
            };

            return View(vm);
        }

        // =====================================
        // EDIT (POST) con auditoría centralizada
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, NivelCreateVM vm)
        {
            if (id != vm.NIVEL_ID) return NotFound();

            if (!ModelState.IsValid)
                return View(vm);

            var db = await _context.NIVEL.FirstOrDefaultAsync(n => n.NIVEL_ID == id);
            if (db == null) return NotFound();

            // Normalizar
            var nuevoNombre = (vm.NIVEL_NOMBRE ?? "").Trim();
            var nuevaDesc = string.IsNullOrWhiteSpace(vm.NIVEL_DESCRIPCION) ? null : vm.NIVEL_DESCRIPCION.Trim();
            var nuevoEstado = vm.ESTADO;

            // Unicidad (si cambió el nombre)
            if (!string.Equals(db.NIVEL_NOMBRE, nuevoNombre, StringComparison.OrdinalIgnoreCase))
            {
                bool duplicado = await _context.NIVEL.AnyAsync(n =>
                    n.NIVEL_ID != db.NIVEL_ID &&
                    !n.ELIMINADO &&
                    n.NIVEL_NOMBRE.ToLower() == nuevoNombre.ToLower());

                if (duplicado)
                    ModelState.AddModelError(nameof(vm.NIVEL_NOMBRE), "Ya existe un nivel con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            bool hayCambios =
                !string.Equals(db.NIVEL_NOMBRE, nuevoNombre) ||
                !string.Equals(db.NIVEL_DESCRIPCION, nuevaDesc) ||
                db.ESTADO != nuevoEstado;

            if (!hayCambios)
            {
                TempData["SwalOneBtnFlag"] = "nochange";
                TempData["SwalTitle"] = "Sin cambios";
                TempData["SwalText"] = "No se modificó ningún dato.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Aplicar cambios
            db.NIVEL_NOMBRE = nuevoNombre;
            db.NIVEL_DESCRIPCION = nuevaDesc;
            db.ESTADO = nuevoEstado;

            // Auditoría modificación
            _audit.StampUpdate(db);
            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Nivel actualizado!";
            TempData["SwalText"] = $"\"{db.NIVEL_NOMBRE}\" se actualizó correctamente.";

            return RedirectToAction(nameof(Edit), new { id = db.NIVEL_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico con auditoría
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var n = await _context.NIVEL
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NIVEL_ID == id);

            if (n == null) return NotFound();

            return View(n);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var n = await _context.NIVEL.FindAsync(id);
            if (n == null) return NotFound();

            _audit.StampSoftDelete(n);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo NV00000001 (prefijo + 8 dígitos)
        private async Task<string> SiguienteNivelIdAsync()
        {
            const string prefijo = "NV";
            const int ancho = 8;

            var ids = await _context.NIVEL
                .Select(n => n.NIVEL_ID)
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

        private bool NivelExists(string id) => _context.NIVEL.Any(e => e.NIVEL_ID == id);
    }
}
