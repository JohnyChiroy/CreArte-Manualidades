using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;                 // <- por si lo usa tu servicio internamente
using CreArte.Services.Auditoria;            // <- IAuditoriaService
using Rotativa.AspNetCore;                     // ViewAsPdf
using Rotativa.AspNetCore.Options;             // para opciones de PDF


namespace CreArte.Controllers
{
    public class AreasController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit; // <- Servicio de auditoría

        // Inyección de dependencias (DbContext + Auditoría)
        public AreasController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO
        // Ruta: GET /Areas?Search=...&Area=...&Estado=...&FechaInicio=...&FechaFin=...&Sort=...&Dir=...
        // ============================================================
        public async Task<IActionResult> Index(
            string Search,
            string Area,
            DateTime? FechaInicio,
            DateTime? FechaFin,
            bool? Estado,
            string Sort = "id",   // ordenamiento por defecto
            string Dir = "asc",   // "asc" | "desc"
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Query base (solo no eliminados)
            var q = _context.AREA.AsQueryable()
                                 .Where(a => !a.ELIMINADO);

            // 2) Búsqueda global
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(a =>
                    EF.Functions.Like(a.AREA_ID, $"%{s}%") ||
                    EF.Functions.Like(a.AREA_NOMBRE, $"%{s}%"));
            }

            // 3) Filtro por nombre con contratos especiales
            if (!string.IsNullOrWhiteSpace(Area))
            {
                if (Area == "__BLANKS__")
                    q = q.Where(a => string.IsNullOrEmpty(a.AREA_NOMBRE));
                else if (Area == "__NONBLANKS__")
                    q = q.Where(a => !string.IsNullOrEmpty(a.AREA_NOMBRE));
                else
                {
                    string s = Area.Trim();
                    q = q.Where(a => EF.Functions.Like(a.AREA_NOMBRE, $"%{s}%"));
                }
            }

            // 4) Filtro por fecha de creación
            if (FechaInicio.HasValue)
                q = q.Where(a => a.FECHA_CREACION >= FechaInicio.Value.Date);

            if (FechaFin.HasValue)
                q = q.Where(a => a.FECHA_CREACION < FechaFin.Value.Date.AddDays(1)); // inclusivo

            // 5) Filtro por estado
            if (Estado.HasValue)
                q = q.Where(a => a.ESTADO == Estado.Value);

            // 6) Ordenamiento
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id"     => asc ? q.OrderBy(a => a.AREA_ID)       : q.OrderByDescending(a => a.AREA_ID),
                "area"   => asc ? q.OrderBy(a => a.AREA_NOMBRE)   : q.OrderByDescending(a => a.AREA_NOMBRE),
                "estado" => asc ? q.OrderBy(a => a.ESTADO)        : q.OrderByDescending(a => a.ESTADO),
                _        => asc ? q.OrderBy(a => a.FECHA_CREACION): q.OrderByDescending(a => a.FECHA_CREACION),
            };

            // 7) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            var items = await q.Skip((Page - 1) * PageSize)
                               .Take(PageSize)
                               .ToListAsync();

            // 8) ViewModel de listado
            var vm = new AreaViewModels
            {
                Items = items,
                Search = Search,
                Area = Area,
                FechaInicio = FechaInicio,
                FechaFin = FechaFin,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = Page,
                PageSize = PageSize,
                TotalPages = totalPages
            };

            return View(vm);
        }

        // ============================================================
        // GET: /Areas/DetailsCard?id=AR0001
        // Devuelve PartialView (tarjeta) para incrustar en SweetAlert2
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.AREA
                .AsNoTracking()
                .Where(a => a.AREA_ID == id && !a.ELIMINADO)
                .Select(a => new AreaDetailsVM
                {
                    AREA_ID = a.AREA_ID,
                    AREA_NOMBRE = a.AREA_NOMBRE,
                    AREA_DESCRIPCION = a.AREA_DESCRIPCION,
                    NIVEL_ID = a.NIVEL_ID,
                    NIVEL_NOMBRE = a.NIVEL.NIVEL_NOMBRE,
                    ESTADO = a.ESTADO,
                    FECHA_CREACION = a.FECHA_CREACION,
                    USUARIO_CREACION = a.USUARIO_CREACION,
                    FECHA_MODIFICACION = a.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = a.USUARIO_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();

            return PartialView("Details", vm);
        }

        // =====================================
        // GET: /Area/Create
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new AreaCreateVM
            {
                AREA_ID = await SiguienteAreaIdAsync(), // prefijo AR
                ESTADO = true,
                Niveles = await CargarNivelesAsync()
            };
            return View(vm);
        }

        // =====================================
        // POST: /Area/Create
        // (Con auditoría centralizada: StampCreate)
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AreaCreateVM vm)
        {
            // (1) Re-generar ID en servidor
            vm.AREA_ID = await SiguienteAreaIdAsync();

            // (2) Validación
            if (!ModelState.IsValid)
            {
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            // Normalizar
            var nombre = (vm.AREA_NOMBRE ?? string.Empty).Trim();
            var descripcion = (vm.AREA_DESCRIPCION ?? string.Empty).Trim();
            var nivelId = (vm.NIVEL_ID ?? string.Empty).Trim();

            // (3) FK nivel
            var nivelExiste = await _context.NIVEL.AnyAsync(n => n.NIVEL_ID == nivelId);
            if (!nivelExiste)
            {
                ModelState.AddModelError(nameof(vm.NIVEL_ID), "El nivel seleccionado no existe.");
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            // (4) Nombre único entre no eliminados
            var nombreDuplicado = await _context.AREA
                .AnyAsync(a => !a.ELIMINADO && a.AREA_NOMBRE.ToLower() == nombre.ToLower());
            if (nombreDuplicado)
            {
                ModelState.AddModelError(nameof(vm.AREA_NOMBRE), "Ya existe un área con ese nombre.");
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            // (5) Mapeo VM -> Entidad
            var entidad = new AREA
            {
                AREA_ID = vm.AREA_ID,
                AREA_NOMBRE = nombre,
                AREA_DESCRIPCION = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion,
                NIVEL_ID = nivelId,
                ESTADO = vm.ESTADO
            };

            // Auditoría de creación CENTRALIZADA
            // - Setea: USUARIO_CREACION, FECHA_CREACION, ELIMINADO=false, limpia campos de mod/eliminación.
            _audit.StampCreate(entidad);

            _context.AREA.Add(entidad);
            await _context.SaveChangesAsync();

            // (6) SweetAlert2 (2 botones en Create)
            TempData["SwalTitle"] = "¡Área guardada!";
            TempData["SwalText"] = $"El registro \"{vm.AREA_NOMBRE}\" se creó correctamente.";
            TempData["SwalIndexUrl"] = Url.Action("Index", "Areas");   // Confirmar
            TempData["SwalCreateUrl"] = Url.Action("Create", "Areas"); // Denegar (nuevo)

            return RedirectToAction(nameof(Create));
        }

        // =====================================
        // Helper: Genera IDs tipo AR00000000 (prefijo + 8 dígitos)
        // =====================================
        private async Task<string> SiguienteAreaIdAsync()
        {
            const string prefijo = "AR";
            const int ancho = 8; // AR + 8 dígitos

            var ids = await _context.AREA
                .Select(a => a.AREA_ID)
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

        private async Task<List<SelectListItem>> CargarNivelesAsync()
        {
            return await _context.NIVEL
                .Where(n => !n.ELIMINADO && n.ESTADO) //  solo niveles activos y no eliminados
                .OrderBy(n => n.NIVEL_NOMBRE)
                .Select(n => new SelectListItem
                {
                    Text = n.NIVEL_NOMBRE,
                    Value = n.NIVEL_ID
                })
                .ToListAsync();
        }

        // ============================================================
        // EDIT (GET): /Areas/Edit/{id}
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var area = await _context.AREA
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AREA_ID == id && !a.ELIMINADO);

            if (area == null) return NotFound();

            var vm = new AreaCreateVM
            {
                AREA_ID = area.AREA_ID,
                AREA_NOMBRE = area.AREA_NOMBRE,
                AREA_DESCRIPCION = area.AREA_DESCRIPCION,
                NIVEL_ID = area.NIVEL_ID,
                ESTADO = area.ESTADO,
                Niveles = await CargarNivelesAsync()
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST): /Areas/Edit/{id}
        // (Con auditoría centralizada: StampUpdate)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AreaCreateVM vm)
        {
            if (id != vm.AREA_ID) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Niveles = await CargarNivelesAsync();
                return View(vm);
            }

            var dbArea = await _context.AREA.FirstOrDefaultAsync(a => a.AREA_ID == id);
            if (dbArea == null) return NotFound();

            // Normalizar
            var nuevoNombre = (vm.AREA_NOMBRE ?? "").Trim();
            var nuevaDesc   = string.IsNullOrWhiteSpace(vm.AREA_DESCRIPCION) ? null : vm.AREA_DESCRIPCION.Trim();
            var nuevoNivel  = (vm.NIVEL_ID ?? "").Trim();
            var nuevoEstado = vm.ESTADO;

            bool hayCambios =
                !string.Equals(dbArea.AREA_NOMBRE,       nuevoNombre) ||
                !string.Equals(dbArea.AREA_DESCRIPCION,   nuevaDesc)   ||
                !string.Equals(dbArea.NIVEL_ID,           nuevoNivel)  ||
                 dbArea.ESTADO != nuevoEstado;

            if (!hayCambios)
            {
                // Modal informativo (1 botón) en Edit
                TempData["SwalOneBtnFlag"] = "nochange";
                TempData["SwalTitle"] = "Sin cambios";
                TempData["SwalText"]  = "No se modificó ningún dato.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Aplicar cambios editables
            dbArea.AREA_NOMBRE       = nuevoNombre;
            dbArea.AREA_DESCRIPCION  = nuevaDesc;
            dbArea.NIVEL_ID          = nuevoNivel;
            dbArea.ESTADO            = nuevoEstado;

            // Auditoría de modificación CENTRALIZADA
            // - Setea: USUARIO_MODIFICACION y FECHA_MODIFICACION.
            _audit.StampUpdate(dbArea);

            await _context.SaveChangesAsync();

            // Modal de éxito (1 botón) en Edit
            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Área actualizada!";
            TempData["SwalText"]  = $"\"{dbArea.AREA_NOMBRE}\" se actualizó correctamente.";

            // PRG → volver a Edit para mostrar el modal y, al Aceptar, redirigir a Index (lo hace _SwalBootstrap)
            return RedirectToAction(nameof(Edit), new { id = dbArea.AREA_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico (con auditoría centralizada)
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var area = await _context.AREA
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.AREA_ID == id);

            if (area == null) return NotFound();
            return View(area);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var area = await _context.AREA.FindAsync(id);
            if (area == null) return NotFound();

            // Auditoría de eliminación lógica CENTRALIZADA
            // - Setea: ELIMINADO=true, USUARIO_ELIMINACION, FECHA_ELIMINACION.
            _audit.StampSoftDelete(area);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // =====================  HELPERS  ============================
        // ============================================================
        private bool AREAExists(string id) => _context.AREA.Any(e => e.AREA_ID == id);

        // ===================================================================
        // Helper reutilizable que construye el query de Áreas con filtros/orden
        // Usado por Index y por ReportePDF para evitar duplicar la lógica.
        // ===================================================================
        private IQueryable<AREA> BuildAreasQuery(
            string Search,
            string Area,
            DateTime? FechaInicio,
            DateTime? FechaFin,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc")
        {
            // (1) Base: solo no eliminados
            var q = _context.AREA.AsNoTracking().Where(a => !a.ELIMINADO);

            // (2) Búsqueda global
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(a =>
                    EF.Functions.Like(a.AREA_ID, $"%{s}%") ||
                    EF.Functions.Like(a.AREA_NOMBRE, $"%{s}%"));
            }

            // (3) Filtro por nombre especial ("__BLANKS__", "__NONBLANKS__", texto)
            if (!string.IsNullOrWhiteSpace(Area))
            {
                if (Area == "__BLANKS__")
                    q = q.Where(a => string.IsNullOrEmpty(a.AREA_NOMBRE));
                else if (Area == "__NONBLANKS__")
                    q = q.Where(a => !string.IsNullOrEmpty(a.AREA_NOMBRE));
                else
                {
                    string s = Area.Trim();
                    q = q.Where(a => EF.Functions.Like(a.AREA_NOMBRE, $"%{s}%"));
                }
            }

            // (4) Rango de fechas (inclusivo)
            if (FechaInicio.HasValue)
                q = q.Where(a => a.FECHA_CREACION >= FechaInicio.Value.Date);

            if (FechaFin.HasValue)
                q = q.Where(a => a.FECHA_CREACION < FechaFin.Value.Date.AddDays(1));

            // (5) Estado
            if (Estado.HasValue)
                q = q.Where(a => a.ESTADO == Estado.Value);

            // (6) Ordenamiento
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(a => a.AREA_ID) : q.OrderByDescending(a => a.AREA_ID),
                "area" => asc ? q.OrderBy(a => a.AREA_NOMBRE) : q.OrderByDescending(a => a.AREA_NOMBRE),
                "estado" => asc ? q.OrderBy(a => a.ESTADO) : q.OrderByDescending(a => a.ESTADO),
                _ => asc ? q.OrderBy(a => a.FECHA_CREACION) : q.OrderByDescending(a => a.FECHA_CREACION),
            };

            return q;
        }


        // ============================================================
        // GET: /Areas/ReportePDF
        // Genera PDF con Rotativa usando la vista "ReporteAreas.cshtml"
        // Recibe los mismos filtros del Index (sin paginar).
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ReportePDF(
            string Search,
            string Area,
            DateTime? FechaInicio,
            DateTime? FechaFin,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc")
        {
            // 1) Construimos el query con los mismos filtros/orden del Index
            var q = BuildAreasQuery(Search, Area, FechaInicio, FechaFin, Estado, Sort, Dir);

            // 2) Traemos TODO (sin Skip/Take)
            var items = await q.ToListAsync();

            // 3) Armamos un VM (reutilizamos AreaViewModels sin paginar)
            var vm = new AreaViewModels
            {
                Items = items,
                Search = Search,
                Area = Area,
                FechaInicio = FechaInicio,
                FechaFin = FechaFin,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = 1,
                PageSize = items.Count,
                TotalPages = 1,
                TotalItems = items.Count
            };

            // 4) Devolvemos un PDF a partir de la vista "ReporteAreas"
            //var pdf = new ViewAsPdf("ReporteAreas", vm)
            //{
            //    FileName = $"Reporte_Areas_{DateTime.Now:yyyyMMdd_HHmm}.pdf",

            //    // --- Opciones del PDF ---
            //    PageSize = Size.Letter,                            // Carta (puedes usar A4 si prefieres)
            //    PageOrientation = Orientation.Portrait,            // Vertical
            //    PageMargins = new Margins { Left = 10, Right = 10, Top = 15, Bottom = 15 },

            //    // Puedes agregar cabeceras/pies si lo requieres:
            //    // CustomSwitches = "--print-media-type"
            //};


            var pdf = new ViewAsPdf("ReporteAreas", vm)
            {
                
                ContentDisposition = ContentDisposition.Inline,                  // ✅ Abrir inline
                PageSize = Size.Letter,
                PageOrientation = Orientation.Landscape,
                PageMargins = new Margins { Left = 10, Right = 10, Top = 15, Bottom = 15 }
            };

            return pdf;
        }

        // ============================================================
        // GET: /Areas/ReportePreview
        // Vista HTML para previsualizar el reporte (sin generar PDF).
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ReportePreview(
            string Search,
            string Area,
            DateTime? FechaInicio,
            DateTime? FechaFin,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc")
        {
            var q = BuildAreasQuery(Search, Area, FechaInicio, FechaFin, Estado, Sort, Dir);
            var items = await q.ToListAsync();

            var vm = new AreaViewModels
            {
                Items = items,
                Search = Search,
                Area = Area,
                FechaInicio = FechaInicio,
                FechaFin = FechaFin,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = 1,
                PageSize = items.Count,
                TotalPages = 1,
                TotalItems = items.Count
            };

            // Reutilizamos la MISMA vista del PDF
            return View("ReporteAreas", vm);
        }

    }
}
