using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial; 
using CreArte.Services.Auditoria; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;

namespace CreArte.Controllers
{
    public class ProductosController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;
        private readonly IWebHostEnvironment _env;

        // Carpeta relativa (web) donde se guardan las imágenes
        private const string UploadRelPath = "/uploads/productos/";
        // Extensiones permitidas para imágenes
        private static readonly string[] AllowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        // Tamaño máximo (en bytes) — 2 MB
        private const long MaxImageBytes = 2 * 1024 * 1024;

        public ProductosController(CreArteDbContext context, IAuditoriaService audit, IWebHostEnvironment env)
        {
            _context = context;
            _audit = audit;
            _env = env;
        }

        // ============================================================
        // LISTADO – RUTA: GET /Productos?...
        // ============================================================
        public async Task<IActionResult> Index(
            string? Search,
            string? SubCategoria,
            string? Tipo,
            string? Marca,
            string? Unidad,
            string? Empaque,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Base (no eliminados)
            IQueryable<PRODUCTO> q = _context.PRODUCTO.Where(p => !p.ELIMINADO);

            // 2) Búsqueda global: ID, Nombre, SubCategoría, Marca
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(p =>
                    EF.Functions.Like(p.PRODUCTO_ID, $"%{s}%") ||
                    EF.Functions.Like(p.PRODUCTO_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(p.SUBCATEGORIA.SUBCATEGORIA_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(p.MARCA.MARCA_NOMBRE, $"%{s}%")
                );
            }

            // 3) Filtros por combos (acepta texto libre por nombre)
            if (!string.IsNullOrWhiteSpace(SubCategoria))
            {
                string s = SubCategoria.Trim();
                q = q.Where(p => EF.Functions.Like(p.SUBCATEGORIA.SUBCATEGORIA_NOMBRE, $"%{s}%"));
            }
            if (!string.IsNullOrWhiteSpace(Tipo))
            {
                string s = Tipo.Trim();
                q = q.Where(p => EF.Functions.Like(p.TIPO_PRODUCTO.TIPO_PRODUCTO_NOMBRE, $"%{s}%"));
            }
            if (!string.IsNullOrWhiteSpace(Marca))
            {
                string s = Marca.Trim();
                q = q.Where(p => EF.Functions.Like(p.MARCA.MARCA_NOMBRE, $"%{s}%"));
            }
            if (!string.IsNullOrWhiteSpace(Unidad))
            {
                string s = Unidad.Trim();
                q = q.Where(p => EF.Functions.Like(p.UNIDAD_MEDIDA.UNIDAD_MEDIDA_NOMBRE, $"%{s}%"));
            }
            if (!string.IsNullOrWhiteSpace(Empaque))
            {
                string s = Empaque.Trim();
                q = q.Where(p => p.TIPO_EMPAQUE_ID != null &&
                                 EF.Functions.Like(p.TIPO_EMPAQUE.TIPO_EMPAQUE_NOMBRE, $"%{s}%"));
            }

            // 4) Estado
            if (Estado.HasValue)
                q = q.Where(p => p.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(p => p.PRODUCTO_ID) : q.OrderByDescending(p => p.PRODUCTO_ID),
                "nombre" => asc ? q.OrderBy(p => p.PRODUCTO_NOMBRE) : q.OrderByDescending(p => p.PRODUCTO_NOMBRE),
                "subcat" => asc ? q.OrderBy(p => p.SUBCATEGORIA.SUBCATEGORIA_NOMBRE) : q.OrderByDescending(p => p.SUBCATEGORIA.SUBCATEGORIA_NOMBRE),
                "tipo" => asc ? q.OrderBy(p => p.TIPO_PRODUCTO.TIPO_PRODUCTO_NOMBRE) : q.OrderByDescending(p => p.TIPO_PRODUCTO.TIPO_PRODUCTO_NOMBRE),
                "marca" => asc ? q.OrderBy(p => p.MARCA.MARCA_NOMBRE) : q.OrderByDescending(p => p.MARCA.MARCA_NOMBRE),
                "unidad" => asc ? q.OrderBy(p => p.UNIDAD_MEDIDA.UNIDAD_MEDIDA_NOMBRE) : q.OrderByDescending(p => p.UNIDAD_MEDIDA.UNIDAD_MEDIDA_NOMBRE),
                "empaque" => asc ? q.OrderBy(p => p.TIPO_EMPAQUE.TIPO_EMPAQUE_NOMBRE) : q.OrderByDescending(p => p.TIPO_EMPAQUE.TIPO_EMPAQUE_NOMBRE),
                "iva" => asc ? q.OrderBy(p => p.PORCENTAJE_IVA) : q.OrderByDescending(p => p.PORCENTAJE_IVA),
                "estado" => asc ? q.OrderBy(p => p.ESTADO) : q.OrderByDescending(p => p.ESTADO),
                _ => asc ? q.OrderBy(p => p.FECHA_CREACION) : q.OrderByDescending(p => p.FECHA_CREACION),
            };

            // 6) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // 7) Include de catálogos
            var qWithNavs = q
                .Include(p => p.SUBCATEGORIA)
                .Include(p => p.TIPO_PRODUCTO)
                .Include(p => p.UNIDAD_MEDIDA)
                .Include(p => p.TIPO_EMPAQUE)
                .Include(p => p.MARCA);

            var items = await qWithNavs
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 8) VM salida
            var vm = new ProductoViewModels
            {
                Items = items,
                Search = Search,
                SubCategoria = SubCategoria,
                Tipo = Tipo,
                Marca = Marca,
                Unidad = Unidad,
                Empaque = Empaque,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = Page,
                PageSize = PageSize,
                TotalPages = totalPages,
                TotalItems = total,
                SubCategorias = await CargarSubCategoriasAsync(),
                TiposProducto = await CargarTiposProductoAsync(),
                Marcas = await CargarMarcasAsync(),
                Unidades = await CargarUnidadesAsync(),
                Empaques = await CargarEmpaquesAsync()
            };

            return View(vm);
        }

        // ============================================================
        // DETAILS (tarjeta modal) – RUTA: GET /Productos/DetailsCard?id=...
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.PRODUCTO
                .AsNoTracking()
                .Include(p => p.SUBCATEGORIA)
                .Include(p => p.TIPO_PRODUCTO)
                .Include(p => p.UNIDAD_MEDIDA)
                .Include(p => p.TIPO_EMPAQUE)
                .Include(p => p.MARCA)
                .Where(p => p.PRODUCTO_ID == id && !p.ELIMINADO)
                .Select(p => new ProductoDetailsVM
                {
                    PRODUCTO_ID = p.PRODUCTO_ID,
                    PRODUCTO_NOMBRE = p.PRODUCTO_NOMBRE,
                    PRODUCTO_DESCRIPCION = p.PRODUCTO_DESCRIPCION,
                    SUBCATEGORIA_ID = p.SUBCATEGORIA_ID,
                    SUBCATEGORIA_NOMBRE = p.SUBCATEGORIA.SUBCATEGORIA_NOMBRE,
                    TIPO_PRODUCTO_ID = p.TIPO_PRODUCTO_ID,
                    TIPO_PRODUCTO_NOMBRE = p.TIPO_PRODUCTO.TIPO_PRODUCTO_NOMBRE,
                    UNIDAD_MEDIDA_ID = p.UNIDAD_MEDIDA_ID,
                    UNIDAD_MEDIDA_NOMBRE = p.UNIDAD_MEDIDA.UNIDAD_MEDIDA_NOMBRE,
                    TIPO_EMPAQUE_ID = p.TIPO_EMPAQUE_ID,
                    TIPO_EMPAQUE_NOMBRE = p.TIPO_EMPAQUE_ID == null ? null : p.TIPO_EMPAQUE.TIPO_EMPAQUE_NOMBRE,
                    MARCA_ID = p.MARCA_ID,
                    MARCA_NOMBRE = p.MARCA_ID == null ? null : p.MARCA.MARCA_NOMBRE,
                    IMAGEN_PRODUCTO = p.IMAGEN_PRODUCTO,
                    PORCENTAJE_IVA = p.PORCENTAJE_IVA,
                    ESTADO = p.ESTADO,

                    // Auditoría
                    FECHA_CREACION = p.FECHA_CREACION,
                    USUARIO_CREACION = p.USUARIO_CREACION,
                    FECHA_MODIFICACION = p.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = p.USUARIO_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm);
        }

        // ============================================================
        // CREATE (GET) – RUTA: GET /Productos/Create
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var prodId = await SiguienteProductoIdAsync(); // PR + 8 dígitos

            var vm = new ProductoCreateVM
            {
                PRODUCTO_ID = prodId,
                ESTADO = true,
                SubCategorias = await CargarSubCategoriasAsync(),
                TiposProducto = await CargarTiposProductoAsync(),
                Unidades = await CargarUnidadesAsync(),
                Empaques = await CargarEmpaquesAsync(),
                Marcas = await CargarMarcasAsync()
            };

            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /Productos/Create
        // Recalcula ID en servidor, valida campos y FK activas.
        // Maneja carga de imagen en /wwwroot/uploads/productos/
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductoCreateVM vm, IFormFile? ImagenFile)
        {
            // Recalcular ID (seguridad)
            var prodId = await SiguienteProductoIdAsync();
            vm.PRODUCTO_ID = prodId;

            // --------- VALIDACIONES PRINCIPALES ---------
            if (string.IsNullOrWhiteSpace(vm.PRODUCTO_NOMBRE))
                ModelState.AddModelError(nameof(vm.PRODUCTO_NOMBRE), "El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.SUBCATEGORIA_ID))
                ModelState.AddModelError(nameof(vm.SUBCATEGORIA_ID), "La subcategoría es obligatoria.");

            if (string.IsNullOrWhiteSpace(vm.TIPO_PRODUCTO_ID))
                ModelState.AddModelError(nameof(vm.TIPO_PRODUCTO_ID), "El tipo de producto es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.UNIDAD_MEDIDA_ID))
                ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_ID), "La unidad de medida es obligatoria.");

            if (vm.PORCENTAJE_IVA.HasValue)
            {
                if (vm.PORCENTAJE_IVA.Value < 0 || vm.PORCENTAJE_IVA.Value > 100)
                    ModelState.AddModelError(nameof(vm.PORCENTAJE_IVA), "El IVA debe estar entre 0 y 100.");
            }

            // Validar FKs activas y no eliminadas
            bool subcatOk = !string.IsNullOrWhiteSpace(vm.SUBCATEGORIA_ID) &&
                await _context.SUBCATEGORIA.AnyAsync(s => s.SUBCATEGORIA_ID == vm.SUBCATEGORIA_ID && !s.ELIMINADO && s.ESTADO);
            if (!subcatOk)
                ModelState.AddModelError(nameof(vm.SUBCATEGORIA_ID), "La subcategoría no existe o no está activa.");

            bool tipoOk = !string.IsNullOrWhiteSpace(vm.TIPO_PRODUCTO_ID) &&
                await _context.TIPO_PRODUCTO.AnyAsync(t => t.TIPO_PRODUCTO_ID == vm.TIPO_PRODUCTO_ID && !t.ELIMINADO && t.ESTADO);
            if (!tipoOk)
                ModelState.AddModelError(nameof(vm.TIPO_PRODUCTO_ID), "El tipo de producto no existe o no está activo.");

            bool umOk = !string.IsNullOrWhiteSpace(vm.UNIDAD_MEDIDA_ID) &&
                await _context.UNIDAD_MEDIDA.AnyAsync(u => u.UNIDAD_MEDIDA_ID == vm.UNIDAD_MEDIDA_ID && !u.ELIMINADO && u.ESTADO);
            if (!umOk)
                ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_ID), "La unidad de medida no existe o no está activa.");

            // Empaque y Marca son opcionales, pero si vienen deben existir/estar activos
            if (!string.IsNullOrWhiteSpace(vm.TIPO_EMPAQUE_ID))
            {
                bool empOk = await _context.TIPO_EMPAQUE.AnyAsync(e => e.TIPO_EMPAQUE_ID == vm.TIPO_EMPAQUE_ID && !e.ELIMINADO && e.ESTADO);
                if (!empOk)
                    ModelState.AddModelError(nameof(vm.TIPO_EMPAQUE_ID), "El tipo de empaque no existe o no está activo.");
            }
            if (!string.IsNullOrWhiteSpace(vm.MARCA_ID))
            {
                bool marcaOk = await _context.MARCA.AnyAsync(m => m.MARCA_ID == vm.MARCA_ID && !m.ELIMINADO && m.ESTADO);
                if (!marcaOk)
                    ModelState.AddModelError(nameof(vm.MARCA_ID), "La marca no existe o no está activa.");
            }

            // Validación del archivo de imagen (si se envía)
            string? imageRelUrl = null; // se guardará /uploads/productos/archivo.ext
            if (ImagenFile != null && ImagenFile.Length > 0)
            {
                var validationError = ValidarImagen(ImagenFile);
                if (validationError != null)
                    ModelState.AddModelError(nameof(vm.IMAGEN_PRODUCTO), validationError);
            }

            if (!ModelState.IsValid)
            {
                // Recargar combos
                await CargarCombosAsync(vm);
                return View(vm);
            }

            // Normalización de strings (null si vacío)
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Guardar imagen si se mandó
            if (ImagenFile != null && ImagenFile.Length > 0)
            {
                imageRelUrl = await GuardarImagenAsync(ImagenFile);
            }

            // ENTIDAD
            var prod = new PRODUCTO
            {
                PRODUCTO_ID = prodId,
                PRODUCTO_NOMBRE = vm.PRODUCTO_NOMBRE!.Trim(),
                PRODUCTO_DESCRIPCION = s2(vm.PRODUCTO_DESCRIPCION),
                SUBCATEGORIA_ID = vm.SUBCATEGORIA_ID!,
                TIPO_PRODUCTO_ID = vm.TIPO_PRODUCTO_ID!,
                UNIDAD_MEDIDA_ID = vm.UNIDAD_MEDIDA_ID!,
                TIPO_EMPAQUE_ID = s2(vm.TIPO_EMPAQUE_ID),
                MARCA_ID = s2(vm.MARCA_ID),
                IMAGEN_PRODUCTO = imageRelUrl,
                PORCENTAJE_IVA = vm.PORCENTAJE_IVA,
                ESTADO = vm.ESTADO,
                ELIMINADO = false
            };

            _audit.StampCreate(prod);
            _context.PRODUCTO.Add(prod);
            await _context.SaveChangesAsync();

            TempData["SwalTitle"] = "¡Producto guardado!";
            TempData["SwalText"] = $"El producto \"{prod.PRODUCTO_NOMBRE}\" se creó correctamente.";
            TempData["SwalIndexUrl"] = Url.Action("Index", "Productos");
            TempData["SwalCreateUrl"] = Url.Action("Create", "Productos");
            return RedirectToAction(nameof(Create));
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /Productos/Edit/{id}
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var p = await _context.PRODUCTO
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PRODUCTO_ID == id && !x.ELIMINADO);

            if (p == null) return NotFound();

            var vm = new ProductoCreateVM
            {
                PRODUCTO_ID = p.PRODUCTO_ID,
                PRODUCTO_NOMBRE = p.PRODUCTO_NOMBRE,
                PRODUCTO_DESCRIPCION = p.PRODUCTO_DESCRIPCION,
                SUBCATEGORIA_ID = p.SUBCATEGORIA_ID,
                TIPO_PRODUCTO_ID = p.TIPO_PRODUCTO_ID,
                UNIDAD_MEDIDA_ID = p.UNIDAD_MEDIDA_ID,
                TIPO_EMPAQUE_ID = p.TIPO_EMPAQUE_ID,
                MARCA_ID = p.MARCA_ID,
                IMAGEN_PRODUCTO = p.IMAGEN_PRODUCTO,
                PORCENTAJE_IVA = p.PORCENTAJE_IVA,
                ESTADO = p.ESTADO,

                SubCategorias = await CargarSubCategoriasAsync(),
                TiposProducto = await CargarTiposProductoAsync(),
                Unidades = await CargarUnidadesAsync(),
                Empaques = await CargarEmpaquesAsync(),
                Marcas = await CargarMarcasAsync()
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /Productos/Edit/{id}
        // Actualiza datos del producto. Si viene nueva imagen, la reemplaza.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ProductoCreateVM vm, IFormFile? ImagenFile)
        {
            if (id != vm.PRODUCTO_ID) return NotFound();

            // Validaciones (mismas que Create)
            if (string.IsNullOrWhiteSpace(vm.PRODUCTO_NOMBRE))
                ModelState.AddModelError(nameof(vm.PRODUCTO_NOMBRE), "El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.SUBCATEGORIA_ID))
                ModelState.AddModelError(nameof(vm.SUBCATEGORIA_ID), "La subcategoría es obligatoria.");

            if (string.IsNullOrWhiteSpace(vm.TIPO_PRODUCTO_ID))
                ModelState.AddModelError(nameof(vm.TIPO_PRODUCTO_ID), "El tipo de producto es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.UNIDAD_MEDIDA_ID))
                ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_ID), "La unidad de medida es obligatoria.");

            if (vm.PORCENTAJE_IVA.HasValue)
            {
                if (vm.PORCENTAJE_IVA.Value < 0 || vm.PORCENTAJE_IVA.Value > 100)
                    ModelState.AddModelError(nameof(vm.PORCENTAJE_IVA), "El IVA debe estar entre 0 y 100.");
            }

            bool subcatOk = !string.IsNullOrWhiteSpace(vm.SUBCATEGORIA_ID) &&
                await _context.SUBCATEGORIA.AnyAsync(s => s.SUBCATEGORIA_ID == vm.SUBCATEGORIA_ID && !s.ELIMINADO && s.ESTADO);
            if (!subcatOk)
                ModelState.AddModelError(nameof(vm.SUBCATEGORIA_ID), "La subcategoría no existe o no está activa.");

            bool tipoOk = !string.IsNullOrWhiteSpace(vm.TIPO_PRODUCTO_ID) &&
                await _context.TIPO_PRODUCTO.AnyAsync(t => t.TIPO_PRODUCTO_ID == vm.TIPO_PRODUCTO_ID && !t.ELIMINADO && t.ESTADO);
            if (!tipoOk)
                ModelState.AddModelError(nameof(vm.TIPO_PRODUCTO_ID), "El tipo de producto no existe o no está activo.");

            bool umOk = !string.IsNullOrWhiteSpace(vm.UNIDAD_MEDIDA_ID) &&
                await _context.UNIDAD_MEDIDA.AnyAsync(u => u.UNIDAD_MEDIDA_ID == vm.UNIDAD_MEDIDA_ID && !u.ELIMINADO && u.ESTADO);
            if (!umOk)
                ModelState.AddModelError(nameof(vm.UNIDAD_MEDIDA_ID), "La unidad de medida no existe o no está activa.");

            if (!string.IsNullOrWhiteSpace(vm.TIPO_EMPAQUE_ID))
            {
                bool empOk = await _context.TIPO_EMPAQUE.AnyAsync(e => e.TIPO_EMPAQUE_ID == vm.TIPO_EMPAQUE_ID && !e.ELIMINADO && e.ESTADO);
                if (!empOk)
                    ModelState.AddModelError(nameof(vm.TIPO_EMPAQUE_ID), "El tipo de empaque no existe o no está activo.");
            }
            if (!string.IsNullOrWhiteSpace(vm.MARCA_ID))
            {
                bool marcaOk = await _context.MARCA.AnyAsync(m => m.MARCA_ID == vm.MARCA_ID && !m.ELIMINADO && m.ESTADO);
                if (!marcaOk)
                    ModelState.AddModelError(nameof(vm.MARCA_ID), "La marca no existe o no está activa.");
            }

            // Validar imagen nueva si viene
            if (ImagenFile != null && ImagenFile.Length > 0)
            {
                var validationError = ValidarImagen(ImagenFile);
                if (validationError != null)
                    ModelState.AddModelError(nameof(vm.IMAGEN_PRODUCTO), validationError);
            }

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(vm);
                return View(vm);
            }

            var prod = await _context.PRODUCTO.FirstOrDefaultAsync(p => p.PRODUCTO_ID == id && !p.ELIMINADO);
            if (prod == null) return NotFound();

            // Normalización
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Si vino nueva imagen, guardar y (opcional) eliminar la anterior
            if (ImagenFile != null && ImagenFile.Length > 0)
            {
                var newRel = await GuardarImagenAsync(ImagenFile);

                // Si tenías una imagen previa, puedes eliminar el archivo anterior
                // (opcional). Si no deseas eliminar, comenta este bloque.
                if (!string.IsNullOrWhiteSpace(prod.IMAGEN_PRODUCTO))
                {
                    EliminarImagenFisica(prod.IMAGEN_PRODUCTO);
                }

                prod.IMAGEN_PRODUCTO = newRel;
            }

            // Aplicar cambios
            prod.PRODUCTO_NOMBRE = vm.PRODUCTO_NOMBRE!.Trim();
            prod.PRODUCTO_DESCRIPCION = s2(vm.PRODUCTO_DESCRIPCION);
            prod.SUBCATEGORIA_ID = vm.SUBCATEGORIA_ID!;
            prod.TIPO_PRODUCTO_ID = vm.TIPO_PRODUCTO_ID!;
            prod.UNIDAD_MEDIDA_ID = vm.UNIDAD_MEDIDA_ID!;
            prod.TIPO_EMPAQUE_ID = s2(vm.TIPO_EMPAQUE_ID);
            prod.MARCA_ID = s2(vm.MARCA_ID);
            prod.PORCENTAJE_IVA = vm.PORCENTAJE_IVA;
            prod.ESTADO = vm.ESTADO;

            _audit.StampUpdate(prod);
            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Producto actualizado!";
            TempData["SwalText"] = $"\"{prod.PRODUCTO_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = prod.PRODUCTO_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – RUTA: GET /Productos/Delete/{id}
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var p = await _context.PRODUCTO
                .AsNoTracking()
                .Include(x => x.SUBCATEGORIA)
                .Include(x => x.TIPO_PRODUCTO)
                .Include(x => x.UNIDAD_MEDIDA)
                .Include(x => x.MARCA)
                .FirstOrDefaultAsync(x => x.PRODUCTO_ID == id);

            if (p == null) return NotFound();

            return View(p);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var p = await _context.PRODUCTO.FindAsync(id);
            if (p == null) return NotFound();

            _audit.StampSoftDelete(p);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo PR00000001 para PRODUCTO
        private async Task<string> SiguienteProductoIdAsync()
        {
            const string prefijo = "PR";
            const int ancho = 8;

            var ids = await _context.PRODUCTO
                .Select(p => p.PRODUCTO_ID)
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

        // Cargar combos de catálogos (solo activos y no eliminados)
        private async Task<List<SelectListItem>> CargarSubCategoriasAsync()
        {
            return await _context.SUBCATEGORIA
                .Where(s => !s.ELIMINADO && s.ESTADO)
                .OrderBy(s => s.SUBCATEGORIA_NOMBRE)
                .Select(s => new SelectListItem { Text = s.SUBCATEGORIA_NOMBRE, Value = s.SUBCATEGORIA_ID })
                .ToListAsync();
        }
        private async Task<List<SelectListItem>> CargarTiposProductoAsync()
        {
            return await _context.TIPO_PRODUCTO
                .Where(t => !t.ELIMINADO && t.ESTADO)
                .OrderBy(t => t.TIPO_PRODUCTO_NOMBRE)
                .Select(t => new SelectListItem { Text = t.TIPO_PRODUCTO_NOMBRE, Value = t.TIPO_PRODUCTO_ID })
                .ToListAsync();
        }
        private async Task<List<SelectListItem>> CargarUnidadesAsync()
        {
            return await _context.UNIDAD_MEDIDA
                .Where(u => !u.ELIMINADO && u.ESTADO)
                .OrderBy(u => u.UNIDAD_MEDIDA_NOMBRE)
                .Select(u => new SelectListItem { Text = u.UNIDAD_MEDIDA_NOMBRE, Value = u.UNIDAD_MEDIDA_ID })
                .ToListAsync();
        }
        private async Task<List<SelectListItem>> CargarEmpaquesAsync()
        {
            return await _context.TIPO_EMPAQUE
                .Where(e => !e.ELIMINADO && e.ESTADO)
                .OrderBy(e => e.TIPO_EMPAQUE_NOMBRE)
                .Select(e => new SelectListItem { Text = e.TIPO_EMPAQUE_NOMBRE, Value = e.TIPO_EMPAQUE_ID })
                .ToListAsync();
        }
        private async Task<List<SelectListItem>> CargarMarcasAsync()
        {
            return await _context.MARCA
                .Where(m => !m.ELIMINADO && m.ESTADO)
                .OrderBy(m => m.MARCA_NOMBRE)
                .Select(m => new SelectListItem { Text = m.MARCA_NOMBRE, Value = m.MARCA_ID })
                .ToListAsync();
        }

        private async Task CargarCombosAsync(ProductoCreateVM vm)
        {
            vm.SubCategorias = await CargarSubCategoriasAsync();
            vm.TiposProducto = await CargarTiposProductoAsync();
            vm.Unidades = await CargarUnidadesAsync();
            vm.Empaques = await CargarEmpaquesAsync();
            vm.Marcas = await CargarMarcasAsync();
        }

        private string? ValidarImagen(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExts.Contains(ext))
                return "Formato de imagen inválido. Usa JPG, JPEG, PNG o WEBP.";

            if (file.Length > MaxImageBytes)
                return "La imagen supera el tamaño máximo permitido (2 MB).";

            return null;
        }

        private async Task<string> GuardarImagenAsync(IFormFile file)
        {
            // 1) Asegurar que exista el folder físico
            var webroot = _env.WebRootPath; // .../wwwroot
            var physicalFolder = Path.Combine(webroot, UploadRelPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(physicalFolder))
                Directory.CreateDirectory(physicalFolder);

            // 2) Generar nombre único
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var rand = Guid.NewGuid().ToString("N").Substring(0, 6);
            var safeBase = Path.GetFileNameWithoutExtension(file.FileName);
            // Limpia el base por si trae caracteres raros
            foreach (var c in Path.GetInvalidFileNameChars()) safeBase = safeBase.Replace(c, '_');
            var newFileName = $"{safeBase}_{stamp}_{rand}{ext}";
            var physicalPath = Path.Combine(physicalFolder, newFileName);

            // 3) Guardar archivo
            using (var fs = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            // 4) Devolver URL relativa (para guardar en DB)
            return $"{UploadRelPath}{newFileName}";
        }

        // Elimina físicamente la imagen anterior (si existe). Recibe la URL relativa guardada en DB.
        private void EliminarImagenFisica(string relativeUrl)
        {
            try
            {
                var webroot = _env.WebRootPath;
                var rel = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var physicalPath = Path.Combine(webroot, rel);
                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }
            catch
            {
                // Silencioso: no bloquear la operación por error al borrar archivo
            }
        }

        // ============================================================
        // GET: /Productos/ReportePDF
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ReportePDF(
            string Search,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc")
        {

            var q = _context.PRODUCTO
                .AsNoTracking()
                .Include(p => p.SUBCATEGORIA)
                .Include(p => p.TIPO_PRODUCTO)
                .Include(p => p.UNIDAD_MEDIDA)
                .Include(p => p.TIPO_EMPAQUE)
                .Include(p => p.MARCA)
                .AsQueryable();

            // Búsqueda global (por ID, nombre o descripción)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(p =>
                    EF.Functions.Like(p.PRODUCTO_ID, $"%{s}%") ||
                    EF.Functions.Like(p.PRODUCTO_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(p.PRODUCTO_DESCRIPCION, $"%{s}%"));
            }

            // Filtro por estado (Activo / Inactivo)
            if (Estado.HasValue)
            {
                q = q.Where(p => p.ESTADO == Estado.Value);
            }

            // Ordenamiento
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);

            q = (Sort ?? "id").ToLower() switch
            {
                "id" => asc ? q.OrderBy(p => p.PRODUCTO_ID)
                                 : q.OrderByDescending(p => p.PRODUCTO_ID),

                "nombre" => asc ? q.OrderBy(p => p.PRODUCTO_NOMBRE)
                                 : q.OrderByDescending(p => p.PRODUCTO_NOMBRE),

                "fecha" => asc ? q.OrderBy(p => p.FECHA_CREACION)
                                 : q.OrderByDescending(p => p.FECHA_CREACION),

                "estado" => asc ? q.OrderBy(p => p.ESTADO)
                                 : q.OrderByDescending(p => p.ESTADO),

                _ => asc ? q.OrderBy(p => p.PRODUCTO_ID)
                                 : q.OrderByDescending(p => p.PRODUCTO_ID),
            };

            // Traer todos los datos 
            var items = await q.ToListAsync();

            // Totales: activos e inactivos
            int totActivos = items.Count(p => p.ESTADO && !p.ELIMINADO);
            int totInactivos = items.Count(p => !p.ESTADO && !p.ELIMINADO);

            var vm = new ReporteViewModel<PRODUCTO>
            {
                // Datos
                Items = items,

                // Filtros que se usaron
                Search = Search,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,

                // Para este reporte no usamos paginación
                Page = 1,
                PageSize = items.Count,
                TotalItems = items.Count,
                TotalPages = 1,

                // Metadatos del reporte
                ReportTitle = "Reporte de Productos",
                CompanyInfo = "CreArte Manualidades — Sololá, Guatemala",
                GeneratedBy = User?.Identity?.Name ?? "Usuario no autenticado",
                LogoUrl = Url.Content("~/Imagenes/logoCreArte.png")
            };

            // Registrar totales en el diccionario genérico
            vm.AddTotal("Activos", totActivos);
            vm.AddTotal("Inactivos", totInactivos);

            //Generar PDF en una nueva pestaña
            var pdf = new ViewAsPdf("ReporteProductos", vm)
            {
                FileName = $"ReporteProductos.pdf",
                ContentDisposition = ContentDisposition.Inline,   // se abre en el navegador
                PageSize = Size.Letter,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins { Left = 10, Right = 10, Top = 10, Bottom = 10 },
                CustomSwitches =
                    $"--footer-center \"Página [page] de [toPage]\" " +
                    $"--footer-right \"CreArte Manualidades © {DateTime.Now:yyyy}\" " +
                    $"--footer-font-size 8 --footer-spacing 3 --footer-line"
            };

            return pdf;
        }
    }
}
