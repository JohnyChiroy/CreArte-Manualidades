using CreArte.Data;                 // DbContext
using CreArte.Models;               // Entidades (TIPO_PRODUCTO)
using CreArte.ModelsPartial;        // ViewModels de Tipo Producto
using CreArte.Services.Auditoria;   // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class TiposProductoController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public TiposProductoController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /TiposProducto?...
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
            IQueryable<TIPO_PRODUCTO> q = _context.TIPO_PRODUCTO.Where(c => !c.ELIMINADO);

            // 2) Búsqueda global (por ID, Nombre, Descripción)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(c =>
                    EF.Functions.Like(c.TIPO_PRODUCTO_ID, $"%{s}%") ||
                    EF.Functions.Like(c.TIPO_PRODUCTO_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(c.TIPO_PRODUCTO_DESCRIPCION ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por NOMBRE (texto exacto/parcial)
            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                var n = Nombre.Trim();
                q = q.Where(c => EF.Functions.Like(c.TIPO_PRODUCTO_NOMBRE, $"%{n}%"));
            }

            // 4) Filtro por ESTADO
            if (Estado.HasValue)
                q = q.Where(c => c.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(c => c.TIPO_PRODUCTO_ID) : q.OrderByDescending(c => c.TIPO_PRODUCTO_ID),
                "nombre" => asc ? q.OrderBy(c => c.TIPO_PRODUCTO_NOMBRE) : q.OrderByDescending(c => c.TIPO_PRODUCTO_NOMBRE),
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
            var vm = new TipoProductoViewModels
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
        // DETAILS (tarjeta/modal) – GET /TiposProducto/DetailsCard?id=...
        // ▸ Devuelve PartialView("Details", vm)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.TIPO_PRODUCTO
                .AsNoTracking()
                .Where(c => c.TIPO_PRODUCTO_ID == id && !c.ELIMINADO)
                .Select(c => new TipoProductoDetailsVM
                {
                    TP_ID = c.TIPO_PRODUCTO_ID,
                    TP_NOMBRE = c.TIPO_PRODUCTO_NOMBRE,
                    TP_DESCRIPCION = c.TIPO_PRODUCTO_DESCRIPCION,
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
        // CREATE (GET) – RUTA: GET /TiposProducto/Create
        // Muestra formulario con ID generado.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nuevoId = await SiguienteTipoProductoIdAsync(); // CA + 8 dígitos

            var vm = new TipoProductoCreateVM
            {
                TP_ID = nuevoId,
                ESTADO = true
            };
            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /TiposProducto/Create
        // ▸ Recalcula ID en servidor
        // ▸ Valida campos y unicidad por nombre (no eliminado)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TipoProductoCreateVM vm)
        {
            // Recalcular ID en servidor (seguridad)
            var nuevoId = await SiguienteTipoProductoIdAsync();
            vm.TP_ID = nuevoId;

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.TP_NOMBRE))
                ModelState.AddModelError(nameof(vm.TP_NOMBRE), "El nombre es obligatorio.");

            // Unicidad por nombre (no eliminado)
            string nombre = (vm.TP_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool nombreDuplicado = await _context.TIPO_PRODUCTO
                    .AnyAsync(c => !c.ELIMINADO && c.TIPO_PRODUCTO_NOMBRE == nombre);
                if (nombreDuplicado)
                    ModelState.AddModelError(nameof(vm.TP_NOMBRE), "Ya existe un Tipo de Producto con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Normalizar strings -> null si vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            try
            {
                var c = new TIPO_PRODUCTO
                {
                    TIPO_PRODUCTO_ID = nuevoId,
                    TIPO_PRODUCTO_NOMBRE = nombre,
                    TIPO_PRODUCTO_DESCRIPCION = s2(vm.TP_DESCRIPCION),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(c); // auditoría de creación
                _context.TIPO_PRODUCTO.Add(c);
                await _context.SaveChangesAsync();

                // PRG + SweetAlert (mismo patrón que Empleados)
                TempData["SwalTitle"] = "¡Tipo de Producto guardado!";
                TempData["SwalText"] = $"El registro \"{c.TIPO_PRODUCTO_NOMBRE}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "TiposProducto");
                TempData["SwalCreateUrl"] = Url.Action("Create", "TiposProducto");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                ModelState.AddModelError("", "Ocurrió un error al crear el tipo de producto. Intenta nuevamente.");
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /TiposProducto/Edit/{id}
        // Carga entidad y llena VM.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.TIPO_PRODUCTO
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TIPO_PRODUCTO_ID == id && !x.ELIMINADO);

            if (c == null) return NotFound();

            var vm = new TipoProductoCreateVM
            {
                TP_ID = c.TIPO_PRODUCTO_ID,
                TP_NOMBRE = c.TIPO_PRODUCTO_NOMBRE,
                TP_DESCRIPCION = c.TIPO_PRODUCTO_DESCRIPCION,
                ESTADO = c.ESTADO
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /TiposProducto/Edit/{id}
        // ▸ Valida campos y unicidad por nombre (excluye el propio).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, TipoProductoCreateVM vm)
        {
            if (id != vm.TP_ID) return NotFound();

            // --- VALIDACIONES ---
            if (string.IsNullOrWhiteSpace(vm.TP_NOMBRE))
                ModelState.AddModelError(nameof(vm.TP_NOMBRE), "El nombre es obligatorio.");

            string nombre = (vm.TP_NOMBRE ?? "").Trim();
            if (!string.IsNullOrEmpty(nombre))
            {
                bool duplicado = await _context.TIPO_PRODUCTO
                    .AnyAsync(c => !c.ELIMINADO && c.TIPO_PRODUCTO_NOMBRE == nombre && c.TIPO_PRODUCTO_ID != id);
                if (duplicado)
                    ModelState.AddModelError(nameof(vm.TP_NOMBRE), "Ya existe un tipo de producto con ese nombre.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // Buscar la entidad original
            var c = await _context.TIPO_PRODUCTO
                .FirstOrDefaultAsync(x => x.TIPO_PRODUCTO_ID== id && !x.ELIMINADO);
            if (c == null) return NotFound();

            // Normalizar
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Aplicar cambios
            c.TIPO_PRODUCTO_NOMBRE = nombre;
            c.TIPO_PRODUCTO_DESCRIPCION = s2(vm.TP_DESCRIPCION);
            c.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(c);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Tipo Producto actualizado!";
            TempData["SwalText"] = $"\"{c.TIPO_PRODUCTO_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = c.TIPO_PRODUCTO_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico
        // Rutas:
        //   GET  /TiposProducto/Delete/{id}
        //   POST /TiposProducto/Delete/{id}  (ActionName("Delete"))
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
            var c = await _context.TIPO_PRODUCTO.FindAsync(id);
            if (c == null) return NotFound();

            _audit.StampSoftDelete(c); // tipo producto ELIMINADO = 1 + auditoría
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo TP00000001 para Tipo Producto
        private async Task<string> SiguienteTipoProductoIdAsync()
        {
            const string prefijo = "TP";
            const int ancho = 8;

            // Traemos los IDs existentes que empiezan con "MA"
            var ids = await _context.TIPO_PRODUCTO
                .Select(x => x.TIPO_PRODUCTO_ID)
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
