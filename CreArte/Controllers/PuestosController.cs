// ===============================================
// RUTA: Controllers/PuestosController.cs
// DESCRIPCIÓN: CRUD de PUESTO con borrado lógico,
//              filtros/orden/paginación y auditoría centralizada
// ===============================================
using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CreArte.Services.Auditoria; // IAuditoriaService

namespace CreArte.Controllers
{
    public class PuestosController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public PuestosController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO: GET /Puestos?Search=...&Puesto=...&Area=...&FechaInicio=...&FechaFin=...&Estado=...&Sort=...&Dir=...&Page=1&PageSize=10
        // ============================================================
        public async Task<IActionResult> Index(
    string Search,
    string Puesto,
    string Area,
    string Nivel,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    bool? Estado,
    string Sort = "id",
    string Dir = "asc",
    int Page = 1,
    int PageSize = 10)
        {
            // ============================================================
            // 1) BASE como IQueryable<PUESTO> (SIN Include aquí)
            //    De esta forma q siempre es IQueryable y no choca el tipo
            //    al reasignar con Where/OrderBy.
            // ============================================================
            IQueryable<PUESTO> q = _context.PUESTO.Where(p => !p.ELIMINADO);

            // ============================================================
            // 2) BÚSQUEDA GLOBAL (ID, NOMBRE, ÁREA)
            //    Nota: Ordenar/filtrar por p.AREA.AREA_NOMBRE funciona
            //    sin Include; EF lo traduce en SQL (JOIN).
            // ============================================================
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(p =>
                    EF.Functions.Like(p.PUESTO_ID, $"%{s}%") ||
                    EF.Functions.Like(p.PUESTO_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(p.AREA.AREA_NOMBRE, $"%{s}%"));// ||
                    //EF.Functions.Like(p.NIVEL.NIVEL_NOMBRE), $"%{s}%"));
            }

            // ============================================================
            // 3) FILTRO POR PUESTO (texto / vacíos)
            // ============================================================
            if (!string.IsNullOrWhiteSpace(Puesto))
            {
                if (Puesto == "__BLANKS__")
                    q = q.Where(p => string.IsNullOrEmpty(p.PUESTO_NOMBRE));
                else if (Puesto == "__NONBLANKS__")
                    q = q.Where(p => !string.IsNullOrEmpty(p.PUESTO_NOMBRE));
                else
                {
                    string s = Puesto.Trim();
                    q = q.Where(p => EF.Functions.Like(p.PUESTO_NOMBRE, $"%{s}%"));
                }
            }

            // ============================================================
            // 4) FILTRO POR ÁREA (texto / vacíos)
            // ============================================================
            if (!string.IsNullOrWhiteSpace(Area))
            {
                if (Area == "__BLANKS__")
                    q = q.Where(p => p.AREA == null || string.IsNullOrEmpty(p.AREA.AREA_NOMBRE));
                else if (Area == "__NONBLANKS__")
                    q = q.Where(p => p.AREA != null && !string.IsNullOrEmpty(p.AREA.AREA_NOMBRE));
                else
                {
                    string s = Area.Trim();
                    q = q.Where(p => EF.Functions.Like(p.AREA.AREA_NOMBRE, $"%{s}%"));
                }
            }

            // ============================================================
            // 4.1) FILTRO POR Nivel (texto / vacíos)
            // ============================================================
            if (!string.IsNullOrWhiteSpace(Nivel))
            {
                if (Nivel == "__BLANKS__")
                    q = q.Where(p => p.NIVEL == null || string.IsNullOrEmpty(p.NIVEL.NIVEL_NOMBRE));
                else if (Nivel == "__NONBLANKS__")
                    q = q.Where(p => p.NIVEL != null && !string.IsNullOrEmpty(p.NIVEL.NIVEL_NOMBRE));
                else
                {
                    string s = Nivel.Trim();
                    q = q.Where(p => EF.Functions.Like(p.NIVEL.NIVEL_NOMBRE, $"%{s}%"));
                }
            }
             
            // ============================================================
            // 5) FILTRO POR FECHAS (creación)
            // ============================================================
            if (FechaInicio.HasValue)
                q = q.Where(p => p.FECHA_CREACION >= FechaInicio.Value.Date);

            if (FechaFin.HasValue)
                q = q.Where(p => p.FECHA_CREACION < FechaFin.Value.Date.AddDays(1));

            // ============================================================
            // 6) FILTRO POR ESTADO
            // ============================================================
            if (Estado.HasValue)
                q = q.Where(p => p.ESTADO == Estado.Value);

            // ============================================================
            // 7) ORDENAMIENTO (sobre el IQueryable sin Include)
            // ============================================================
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(p => p.PUESTO_ID) : q.OrderByDescending(p => p.PUESTO_ID),
                "puesto" => asc ? q.OrderBy(p => p.PUESTO_NOMBRE) : q.OrderByDescending(p => p.PUESTO_NOMBRE),
                "area" => asc ? q.OrderBy(p => p.AREA.AREA_NOMBRE) : q.OrderByDescending(p => p.AREA.AREA_NOMBRE),
                "estado" => asc ? q.OrderBy(p => p.ESTADO) : q.OrderByDescending(p => p.ESTADO),
                _ => asc ? q.OrderBy(p => p.FECHA_CREACION) : q.OrderByDescending(p => p.FECHA_CREACION),
            };

            // ============================================================
            // 8) TOTAL PARA PAGINACIÓN (ANTES de Include para optimizar)
            // ============================================================
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // ============================================================
            // 9) AHORA SÍ: INCLUDE para poblar navegaciones que se muestran
            //    en la vista (AREA, NIVEL). Luego paginamos y materializamos.
            // ============================================================
            var qWithNavs = q
                .Include(p => p.AREA)
                .Include(p => p.NIVEL);

            var items = await qWithNavs
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // ============================================================
            // 10) VIEWMODEL
            // ============================================================
            var vm = new PuestoViewModels
            {
                Items = items,
                Search = Search,
                Puesto = Puesto,
                Area = Area,
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
        // GET: /Puestos/DetailsCard?id=PU00000001
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.PUESTO
                .AsNoTracking()
                .Include(p => p.AREA)
                .Include(p => p.NIVEL)
                .Where(p => p.PUESTO_ID == id && !p.ELIMINADO)
                .Select(p => new PuestoDetailsVM
                {
                    PUESTO_ID = p.PUESTO_ID,
                    PUESTO_NOMBRE = p.PUESTO_NOMBRE,
                    PUESTO_DESCRIPCION = p.PUESTO_DESCRIPCION,
                    AREA_ID = p.AREA_ID,
                    AREA_NOMBRE = p.AREA != null ? p.AREA.AREA_NOMBRE : "",
                    NIVEL_ID = p.NIVEL_ID,
                    NIVEL_NOMBRE = p.NIVEL != null ? p.NIVEL.NIVEL_NOMBRE : "",
                    ESTADO = p.ESTADO,
                    FECHA_CREACION = p.FECHA_CREACION,
                    USUARIO_CREACION = p.USUARIO_CREACION,
                    FECHA_MODIFICACION = p.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = p.USUARIO_MODIFICACION
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
            var vm = new PuestoCreateVM
            {
                PUESTO_ID = await SiguientePuestoIdAsync(), // prefijo PU + 8 dígitos
                ESTADO = true,
                Areas = await CargarAreasAsync(),
                Niveles = await CargarNivelesAsync()
            };
            return View(vm);
        }

        // =====================================
        // CREATE (POST) con auditoría centralizada
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PuestoCreateVM vm)
        {
            // Re-generamos el ID en servidor (seguridad)
            vm.PUESTO_ID = await SiguientePuestoIdAsync();

            if (!ModelState.IsValid)
            {
                vm.Areas = await CargarAreasAsync();
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            // Normalizar datos
            var nombre = (vm.PUESTO_NOMBRE ?? string.Empty).Trim();
            var desc   = string.IsNullOrWhiteSpace(vm.PUESTO_DESCRIPCION) ? null : vm.PUESTO_DESCRIPCION.Trim();
            var areaId = (vm.AREA_ID ?? string.Empty).Trim();
            var nivelId = (vm.NIVEL_ID ?? string.Empty).Trim();

            // Validar FKs
            bool areaOk = await _context.AREA.AnyAsync(a => a.AREA_ID == areaId && !a.ELIMINADO);
            if (!areaOk)
                ModelState.AddModelError(nameof(vm.AREA_ID), "El área seleccionada no existe.");

            bool nivelOk = await _context.NIVEL.AnyAsync(n => n.NIVEL_ID == nivelId);
            if (!nivelOk)
                ModelState.AddModelError(nameof(vm.NIVEL_ID), "El nivel seleccionado no existe.");

            if (!ModelState.IsValid)
            {
                vm.Areas = await CargarAreasAsync();
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            // Unicidad: nombre de puesto ÚNICO por ÁREA (entre no eliminados)
            bool duplicado = await _context.PUESTO.AnyAsync(p =>
                !p.ELIMINADO &&
                p.AREA_ID == areaId &&
                p.PUESTO_NOMBRE.ToLower() == nombre.ToLower());

            if (duplicado)
            {
                ModelState.AddModelError(nameof(vm.PUESTO_NOMBRE), "Ya existe un puesto con ese nombre en el área seleccionada.");
                vm.Areas = await CargarAreasAsync();
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            // Mapear VM -> Entidad
            var entidad = new PUESTO
            {
                PUESTO_ID = vm.PUESTO_ID,
                PUESTO_NOMBRE = nombre,
                PUESTO_DESCRIPCION = desc,
                AREA_ID = areaId,
                NIVEL_ID = nivelId,
                ESTADO = vm.ESTADO
            };

            // Auditoría de creación centralizada
            _audit.StampCreate(entidad);

            _context.PUESTO.Add(entidad);
            await _context.SaveChangesAsync();

            // PRG con SweetAlert2 (2 botones: Index/Create)
            TempData["SwalTitle"] = "¡Puesto guardado!";
            TempData["SwalText"] = $"El registro \"{vm.PUESTO_NOMBRE}\" se creó correctamente.";
            TempData["SwalIndexUrl"] = Url.Action("Index", "Puestos");
            TempData["SwalCreateUrl"] = Url.Action("Create", "Puestos");

            return RedirectToAction(nameof(Create));
        }

        // =====================================
        // EDIT (GET)
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var p = await _context.PUESTO
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PUESTO_ID == id && !x.ELIMINADO);

            if (p == null) return NotFound();

            var vm = new PuestoCreateVM
            {
                PUESTO_ID = p.PUESTO_ID,
                PUESTO_NOMBRE = p.PUESTO_NOMBRE,
                PUESTO_DESCRIPCION = p.PUESTO_DESCRIPCION,
                AREA_ID = p.AREA_ID,
                NIVEL_ID = p.NIVEL_ID,
                ESTADO = p.ESTADO,
                Areas = await CargarAreasAsync(),
                Niveles = await CargarNivelesAsync()
            };

            return View(vm);
        }

        // =====================================
        // EDIT (POST) con auditoría centralizada
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, PuestoCreateVM vm)
        {
            if (id != vm.PUESTO_ID) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Areas = await CargarAreasAsync();
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            var db = await _context.PUESTO.FirstOrDefaultAsync(p => p.PUESTO_ID == id);
            if (db == null) return NotFound();

            // Normalizar
            var nuevoNombre = (vm.PUESTO_NOMBRE ?? "").Trim();
            var nuevaDesc = string.IsNullOrWhiteSpace(vm.PUESTO_DESCRIPCION) ? null : vm.PUESTO_DESCRIPCION.Trim();
            var nuevaArea = (vm.AREA_ID ?? "").Trim();
            var nuevoNivel = (vm.NIVEL_ID ?? "").Trim();
            var nuevoEstado = vm.ESTADO;

            // Validar FKs
            bool areaOk = await _context.AREA.AnyAsync(a => a.AREA_ID == nuevaArea && !a.ELIMINADO);
            if (!areaOk)
                ModelState.AddModelError(nameof(vm.AREA_ID), "El área seleccionada no existe.");

            bool nivelOk = await _context.NIVEL.AnyAsync(n => n.NIVEL_ID == nuevoNivel);
            if (!nivelOk)
                ModelState.AddModelError(nameof(vm.NIVEL_ID), "El nivel seleccionado no existe.");

            // Validar unicidad (nombre+área) si cambió nombre o área
            if (!string.Equals(db.PUESTO_NOMBRE, nuevoNombre, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(db.AREA_ID, nuevaArea, StringComparison.OrdinalIgnoreCase))
            {
                bool duplicado = await _context.PUESTO.AnyAsync(p =>
                    p.PUESTO_ID != db.PUESTO_ID &&
                    !p.ELIMINADO &&
                    p.AREA_ID == nuevaArea &&
                    p.PUESTO_NOMBRE.ToLower() == nuevoNombre.ToLower());

                if (duplicado)
                    ModelState.AddModelError(nameof(vm.PUESTO_NOMBRE), "Ya existe un puesto con ese nombre en el área seleccionada.");
            }

            if (!ModelState.IsValid)
            {
                vm.Areas = await CargarAreasAsync();
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            bool hayCambios =
                !string.Equals(db.PUESTO_NOMBRE, nuevoNombre) ||
                !string.Equals(db.PUESTO_DESCRIPCION, nuevaDesc) ||
                !string.Equals(db.AREA_ID, nuevaArea) ||
                !string.Equals(db.NIVEL_ID, nuevoNivel) ||
                 db.ESTADO != nuevoEstado;

            if (!hayCambios)
            {
                TempData["SwalOneBtnFlag"] = "nochange";
                TempData["SwalTitle"] = "Sin cambios";
                TempData["SwalText"] = "No se modificó ningún dato.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Aplicar cambios
            db.PUESTO_NOMBRE = nuevoNombre;
            db.PUESTO_DESCRIPCION = nuevaDesc;
            db.AREA_ID = nuevaArea;
            db.NIVEL_ID = nuevoNivel;
            db.ESTADO = nuevoEstado;

            // Auditoría modificación
            _audit.StampUpdate(db);
            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Puesto actualizado!";
            TempData["SwalText"] = $"\"{db.PUESTO_NOMBRE}\" se actualizó correctamente.";

            return RedirectToAction(nameof(Edit), new { id = db.PUESTO_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico con auditoría
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var p = await _context.PUESTO
                .AsNoTracking()
                .Include(x => x.AREA)
                .Include(x => x.NIVEL)
                .FirstOrDefaultAsync(x => x.PUESTO_ID == id);

            if (p == null) return NotFound();

            return View(p);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var p = await _context.PUESTO.FindAsync(id);
            if (p == null) return NotFound();

            _audit.StampSoftDelete(p);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo PU00000001 (prefijo + 8 dígitos)
        private async Task<string> SiguientePuestoIdAsync()
        {
            const string prefijo = "PU";
            const int ancho = 8;

            var ids = await _context.PUESTO
                .Select(p => p.PUESTO_ID)
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

        private async Task<List<SelectListItem>> CargarAreasAsync()
        {
            return await _context.AREA
                .Where(a => !a.ELIMINADO)
                .OrderBy(a => a.AREA_NOMBRE)
                .Select(a => new SelectListItem
                {
                    Text = a.AREA_NOMBRE,
                    Value = a.AREA_ID
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> CargarNivelesAsync()
        {
            return await _context.NIVEL
                .OrderBy(n => n.NIVEL_NOMBRE)
                .Select(n => new SelectListItem
                {
                    Text = n.NIVEL_NOMBRE,
                    Value = n.NIVEL_ID
                })
                .ToListAsync();
        }

        private bool PuestoExists(string id) => _context.PUESTO.Any(e => e.PUESTO_ID == id);
    }
}
