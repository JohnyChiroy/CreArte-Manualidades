// ===============================================
// RUTA: Controllers/SubCategoriasController.cs
// DESCRIPCIÓN: CRUD de SUBCATEGORÍA con filtros/orden/paginación
//              y auditoría. Usa los mismos estilos/UX que Empleados.
//              ▸ Incluye: búsqueda global, filtro ESTADO, orden por
//                ID/NOMBRE/CATEGORÍA/ESTADO, paginación.
// ===============================================
using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Auditoria; // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class SubCategoriasController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public SubCategoriasController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /SubCategorias?...
        // Filtros: Search, Nombre, Categoria, Estado
        // Orden: id | nombre | categoria | estado | fecha
        // ============================================================
        public async Task<IActionResult> Index(
            string? Search,
            string? Nombre,
            string? Categoria,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Base con Include(CATEGORIA) para poder filtrar/ordenar
            IQueryable<SUBCATEGORIA> q = _context.SUBCATEGORIA
                .Where(s => !s.ELIMINADO)
                .Include(s => s.CATEGORIA);

            // 2) Búsqueda global (ID, Subcat.Nombre, Cat.Nombre, Descripciones)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(su =>
                    EF.Functions.Like(su.SUBCATEGORIA_ID, $"%{s}%") ||
                    EF.Functions.Like(su.SUBCATEGORIA_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(su.SUBCATEGORIA_DESCRIPCION ?? "", $"%{s}%") ||
                    EF.Functions.Like(su.CATEGORIA.CATEGORIA_NOMBRE ?? "", $"%{s}%") ||
                    EF.Functions.Like(su.CATEGORIA.CATEGORIA_DESCRIPCION ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por NOMBRE de Subcategoría
            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                var n = Nombre.Trim();
                q = q.Where(su => EF.Functions.Like(su.SUBCATEGORIA_NOMBRE, $"%{n}%"));
            }

            // 4) Filtro por CATEGORÍA:
            //    - "__BLANKS__": sin categoría (raro, pero soportado)
            //    - "__NONBLANKS__": con categoría
            //    - texto: busca en CATEGORIA_NOMBRE
            if (!string.IsNullOrWhiteSpace(Categoria))
            {
                var cat = Categoria.Trim();
                if (cat == "__BLANKS__")
                    q = q.Where(su => su.CATEGORIA == null || string.IsNullOrEmpty(su.CATEGORIA.CATEGORIA_NOMBRE));
                else if (cat == "__NONBLANKS__")
                    q = q.Where(su => su.CATEGORIA != null && !string.IsNullOrEmpty(su.CATEGORIA.CATEGORIA_NOMBRE));
                else
                    q = q.Where(su => su.CATEGORIA != null && EF.Functions.Like(su.CATEGORIA.CATEGORIA_NOMBRE, $"%{cat}%"));
            }

            // 5) Filtro por ESTADO
            if (Estado.HasValue)
                q = q.Where(su => su.ESTADO == Estado.Value);

            // 6) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(su => su.SUBCATEGORIA_ID) : q.OrderByDescending(su => su.SUBCATEGORIA_ID),
                "nombre" => asc ? q.OrderBy(su => su.SUBCATEGORIA_NOMBRE) : q.OrderByDescending(su => su.SUBCATEGORIA_NOMBRE),
                "categoria" => asc ? q.OrderBy(su => su.CATEGORIA.CATEGORIA_NOMBRE) : q.OrderByDescending(su => su.CATEGORIA.CATEGORIA_NOMBRE),
                "estado" => asc ? q.OrderBy(su => su.ESTADO) : q.OrderByDescending(su => su.ESTADO),
                _ => asc ? q.OrderBy(su => su.FECHA_CREACION) : q.OrderByDescending(su => su.FECHA_CREACION),
            };

            // 7) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            var items = await q
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 8) VM salida
            var vm = new SubCategoriaViewModels
            {
                Items = items,
                Search = Search,
                Nombre = Nombre,
                Categoria = Categoria,
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
        // DETAILS (Partial para modal) – /SubCategorias/DetailsCard?id=...
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.SUBCATEGORIA
                .AsNoTracking()
                .Include(x => x.CATEGORIA)
                .Where(x => x.SUBCATEGORIA_ID == id && !x.ELIMINADO)
                .Select(x => new SubCategoriaDetailsVM
                {
                    SUBCATEGORIA_ID = x.SUBCATEGORIA_ID,
                    SUBCATEGORIA_NOMBRE = x.SUBCATEGORIA_NOMBRE,
                    SUBCATEGORIA_DESCRIPCION = x.SUBCATEGORIA_DESCRIPCION,
                    CATEGORIA_ID = x.CATEGORIA_ID,
                    CATEGORIA_NOMBRE = x.CATEGORIA.CATEGORIA_NOMBRE,
                    ESTADO = x.ESTADO,
                    USUARIO_CREACION = x.USUARIO_CREACION,
                    FECHA_CREACION = x.FECHA_CREACION,
                    USUARIO_MODIFICACION = x.USUARIO_MODIFICACION,
                    FECHA_MODIFICACION = x.FECHA_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm);
        }

        // ============================================================
        // CREATE (GET) – /SubCategorias/Create
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new SubCategoriaCreateVM
            {
                SUBCATEGORIA_ID = await SiguienteSubCategoriaIdAsync(), // SC + 8 dígitos
                ESTADO = true,
                Categorias = await CargarCategoriasAsync()
            };
            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – /SubCategorias/Create
        // Validaciones: Nombre/Categoría obligatorios, FK Categoría activa.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubCategoriaCreateVM vm)
        {
            // Recalcular ID en servidor por seguridad
            vm.SUBCATEGORIA_ID = await SiguienteSubCategoriaIdAsync();

            // --------- VALIDACIONES ---------
            if (string.IsNullOrWhiteSpace(vm.SUBCATEGORIA_NOMBRE))
                ModelState.AddModelError(nameof(vm.SUBCATEGORIA_NOMBRE), "El nombre es obligatorio.");

            // Validar FK Categoría activa y no eliminada
            bool catOk = !string.IsNullOrWhiteSpace(vm.CATEGORIA_ID)
                         && await _context.CATEGORIA.AnyAsync(c => c.CATEGORIA_ID == vm.CATEGORIA_ID && !c.ELIMINADO && c.ESTADO);
            if (!catOk)
                ModelState.AddModelError(nameof(vm.CATEGORIA_ID), "La categoría seleccionada no existe o no está activa.");

            if (!ModelState.IsValid)
            {
                vm.Categorias = await CargarCategoriasAsync();
                return View(vm);
            }
            // --------------------------------

            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            var sc = new SUBCATEGORIA
            {
                SUBCATEGORIA_ID = vm.SUBCATEGORIA_ID,
                SUBCATEGORIA_NOMBRE = vm.SUBCATEGORIA_NOMBRE!.Trim(),
                SUBCATEGORIA_DESCRIPCION = s2(vm.SUBCATEGORIA_DESCRIPCION),
                CATEGORIA_ID = vm.CATEGORIA_ID!,
                ESTADO = vm.ESTADO,
                ELIMINADO = false
            };
            _audit.StampCreate(sc);

            _context.SUBCATEGORIA.Add(sc);
            await _context.SaveChangesAsync();

            // PRG + SweetAlert
            TempData["SwalTitle"] = "¡Subcategoría guardada!";
            TempData["SwalText"] = $"El registro \"{sc.SUBCATEGORIA_NOMBRE}\" se creó correctamente.";
            TempData["SwalIndexUrl"] = Url.Action("Index", "SubCategorias");
            TempData["SwalCreateUrl"] = Url.Action("Create", "SubCategorias");
            return RedirectToAction(nameof(Create));
        }

        // ============================================================
        // EDIT (GET) – /SubCategorias/Edit/{id}
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var x = await _context.SUBCATEGORIA
                .Include(s => s.CATEGORIA)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SUBCATEGORIA_ID == id && !s.ELIMINADO);

            if (x == null) return NotFound();

            var vm = new SubCategoriaCreateVM
            {
                SUBCATEGORIA_ID = x.SUBCATEGORIA_ID,
                SUBCATEGORIA_NOMBRE = x.SUBCATEGORIA_NOMBRE,
                SUBCATEGORIA_DESCRIPCION = x.SUBCATEGORIA_DESCRIPCION,
                CATEGORIA_ID = x.CATEGORIA_ID,
                ESTADO = x.ESTADO,
                Categorias = await CargarCategoriasAsync()
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – /SubCategorias/Edit/{id}
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, SubCategoriaCreateVM vm)
        {
            if (id != vm.SUBCATEGORIA_ID) return NotFound();

            // --------- VALIDACIONES ---------
            if (string.IsNullOrWhiteSpace(vm.SUBCATEGORIA_NOMBRE))
                ModelState.AddModelError(nameof(vm.SUBCATEGORIA_NOMBRE), "El nombre es obligatorio.");

            bool catOk = !string.IsNullOrWhiteSpace(vm.CATEGORIA_ID)
                         && await _context.CATEGORIA.AnyAsync(c => c.CATEGORIA_ID == vm.CATEGORIA_ID && !c.ELIMINADO && c.ESTADO);
            if (!catOk)
                ModelState.AddModelError(nameof(vm.CATEGORIA_ID), "La categoría seleccionada no existe o no está activa.");

            if (!ModelState.IsValid)
            {
                vm.Categorias = await CargarCategoriasAsync();
                return View(vm);
            }
            // --------------------------------

            var sc = await _context.SUBCATEGORIA.FirstOrDefaultAsync(s => s.SUBCATEGORIA_ID == id && !s.ELIMINADO);
            if (sc == null) return NotFound();

            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            sc.SUBCATEGORIA_NOMBRE = vm.SUBCATEGORIA_NOMBRE!.Trim();
            sc.SUBCATEGORIA_DESCRIPCION = s2(vm.SUBCATEGORIA_DESCRIPCION);
            sc.CATEGORIA_ID = vm.CATEGORIA_ID!;
            sc.ESTADO = vm.ESTADO;

            _audit.StampUpdate(sc);
            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Subcategoría actualizada!";
            TempData["SwalText"] = $"\"{sc.SUBCATEGORIA_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = sc.SUBCATEGORIA_ID });
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo SC00000001 (ajusta prefijo si quieres)
        private async Task<string> SiguienteSubCategoriaIdAsync()
        {
            const string prefijo = "SC";
            const int ancho = 8;

            var ids = await _context.SUBCATEGORIA
                .Select(p => p.SUBCATEGORIA_ID)
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

        // Cargar categorías activas para combo
        private async Task<List<SelectListItem>> CargarCategoriasAsync()
        {
            return await _context.CATEGORIA
                .Where(c => !c.ELIMINADO && c.ESTADO)
                .OrderBy(c => c.CATEGORIA_NOMBRE)
                .Select(c => new SelectListItem
                {
                    Text = c.CATEGORIA_NOMBRE,
                    Value = c.CATEGORIA_ID
                })
                .ToListAsync();
        }
    }
}
