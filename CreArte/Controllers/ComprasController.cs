using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Bitacora;
using CreArte.Services.Auditoria;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
public class ComprasController : Controller
{
    private readonly CreArteDbContext _db;
    private readonly IAuditoriaService _audit;
    private readonly IBitacoraService _bitacora;

    public ComprasController(CreArteDbContext db, IAuditoriaService audit, IBitacoraService bitacora)
    {
        _db = db;
        _audit = audit;
        _bitacora = bitacora;
    }

    //genera id para ordenes de compra
    private static string NewReciboId() => "OC" + Guid.NewGuid().ToString("N").Substring(0, 8);

    // Obtiene la sesión de caja activa para el usuario 
    private async Task<string?> GetSesionCajaActivaAsync(string usuarioId)
    {
        return await _db.CAJA_SESION
            .Where(c => c.ESTADO == true)
            .OrderByDescending(c => c.FECHA_APERTURA)
            .Select(c => c.SESION_ID)
            .FirstOrDefaultAsync();
    }

    // MC secuencial tipo "MC00000001" 
    private async Task<string> SiguienteMovimientoCajaIdAsync()
    {
        const string prefijo = "MC";
        const int ancho = 8;
        var ids = await _db.MOVIMIENTO_CAJA.Select(x => x.MOVIMIENTO_ID)
            .Where(id => id.StartsWith(prefijo)).ToListAsync();
        int maxNum = 0;
        var rx = new System.Text.RegularExpressions.Regex(@"^" + prefijo + @"(?<n>\d+)$");
        foreach (var id in ids)
        {
            var m = rx.Match(id ?? "");
            if (m.Success && int.TryParse(m.Groups["n"].Value, out int n) && n > maxNum) maxNum = n;
        }
        return prefijo + (maxNum + 1).ToString(new string('0', ancho));
    }

    // =====================================
    // Helper: Genera IDs 
    // =====================================
    private async Task<string> SiguienteCompraIdAsync()
    {
        const string prefijo = "CO";
        const int ancho = 8;

        var ids = await _db.COMPRA
            .Select(c => c.COMPRA_ID)
            .Where(id => id.StartsWith(prefijo))
            .ToListAsync();

        int maxNum = 0;
        var rx = new System.Text.RegularExpressions.Regex(@"^" + prefijo + @"(?<n>\d+)$");
        foreach (var id in ids)
        {
            var m = rx.Match(id);
            if (m.Success && int.TryParse(m.Groups["n"].Value, out int n))
                if (n > maxNum) maxNum = n;
        }

        var siguiente = maxNum + 1;
        return prefijo + siguiente.ToString(new string('0', ancho));
    }

    // --------- Proveedores Activos ---------
    private async Task<SelectList> BuildProveedoresSelectAsync(string? selectedId = null, bool includeSelectedIfInactive = false)
    {
        // Base: solo activos y no eliminados
        var activos = await _db.PROVEEDOR
            .Where(p => !p.ELIMINADO && p.ESTADO)                 // <- SOLO ACTIVOS
            .Select(p => new { id = p.PROVEEDOR_ID, nombre = p.EMPRESA })
            .OrderBy(p => p.nombre)
            .ToListAsync();

        // Si estamos en EDIT y el proveedor actual está inactivo, lo incluimos solo para mostrarlo seleccionado.
        if (includeSelectedIfInactive && !string.IsNullOrWhiteSpace(selectedId) && !activos.Any(a => a.id == selectedId))
        {
            var actual = await _db.PROVEEDOR
                .Where(p => p.PROVEEDOR_ID == selectedId)
                .Select(p => new { id = p.PROVEEDOR_ID, nombre = p.EMPRESA + " (inactivo)" })
                .FirstOrDefaultAsync();

            if (actual != null)
            {
                // Lo inserto al inicio para que aparezca; sigue marcado como seleccionado.
                activos.Insert(0, actual);
            }
        }

        return new SelectList(activos, "id", "nombre", selectedId);
    }
    private async Task CargarCatalogosEditAsync(string? proveedorSeleccionado)
    {
        //var productosMin = await _db.PRODUCTO
        //    .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
        //    .ToListAsync();
        var productosMin = await GetProductosActivosInventarioAsync();
        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);

        var proveedores = await _db.PROVEEDOR
            .Select(p => new { id = p.PROVEEDOR_ID, nombre = p.EMPRESA })
            .ToListAsync();
        ViewBag.Proveedores = new SelectList(proveedores, "id", "nombre", proveedorSeleccionado);
    }

    private async Task<List<object>> GetProductosActivosInventarioAsync()
    {
        var productos = await (
            from p in _db.PRODUCTO
            join inv in _db.INVENTARIO on p.PRODUCTO_ID equals inv.PRODUCTO_ID
            where inv.ESTADO == true   // activo en inventario
            select new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO }
        )
        .OrderBy(x => x.nombre)
        .ToListAsync();

        return productos.Cast<object>().ToList();
    }

    private async Task CargarMetodosPagoAsync()
    {
        ViewBag.MetodosPagoCombo = await _db.METODO_PAGO
            .Where(m => m.ESTADO && !m.ELIMINADO)
            .OrderBy(m => m.METODO_PAGO_NOMBRE)
            .Select(m => new SelectListItem
            {
                Value = m.METODO_PAGO_ID,
                Text = m.METODO_PAGO_NOMBRE
            })
            .ToListAsync();
    }

    // ============================================================
    // INDEX
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? Search,
        string? Estado,         
        string? Sort = "fecha",  
        string? Dir = "desc",   
        int Page = 1,
        int PageSize = 10)
    {
        // Normalización
        Page = Page <= 0 ? 1 : Page;
        PageSize = (PageSize is < 5 or > 100) ? 10 : PageSize;
        Sort = string.IsNullOrWhiteSpace(Sort) ? "fecha" : Sort.Trim().ToLower();
        Dir = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        bool asc = Dir == "asc";

        // Query base (no tracking para listado)
        var q = _db.COMPRA
            .AsNoTracking()
            .Include(c => c.PROVEEDOR)
            .AsQueryable();

        // Filtro Search
        if (!string.IsNullOrWhiteSpace(Search))
        {
            string s = Search.Trim();
            q = q.Where(c =>
                c.COMPRA_ID.Contains(s) ||
                c.PROVEEDOR_ID.Contains(s) ||
                (c.PROVEEDOR != null && c.PROVEEDOR.EMPRESA.Contains(s))
            );
        }

        // Filtro Estado
        if (!string.IsNullOrWhiteSpace(Estado))
        {
            string e = Estado.Trim().ToUpper();
            q = q.Where(c => c.ESTADO_COMPRA_ID == e);
        }

        // Orden dinámico
        q = (Sort, asc) switch
        {
            ("id", true) => q.OrderBy(c => c.COMPRA_ID),
            ("id", false) => q.OrderByDescending(c => c.COMPRA_ID),

            ("proveedor", true) => q.OrderBy(c => c.PROVEEDOR != null ? c.PROVEEDOR.EMPRESA : c.PROVEEDOR_ID)
                                     .ThenBy(c => c.PROVEEDOR_ID),
            ("proveedor", false) => q.OrderByDescending(c => c.PROVEEDOR != null ? c.PROVEEDOR.EMPRESA : c.PROVEEDOR_ID)
                                     .ThenByDescending(c => c.PROVEEDOR_ID),

            ("fecha", true) => q.OrderBy(c => c.FECHA_COMPRA),
            ("fecha", false) => q.OrderByDescending(c => c.FECHA_COMPRA),

            ("entrega", true) => q.OrderBy(c => c.FECHA_ENTREGA_COMPRA),
            ("entrega", false) => q.OrderByDescending(c => c.FECHA_ENTREGA_COMPRA),

            ("estado", true) => q.OrderBy(c => c.ESTADO_COMPRA_ID),
            ("estado", false) => q.OrderByDescending(c => c.ESTADO_COMPRA_ID),

            ("inv", true) => q.OrderBy(c => c.CARGADA_INVENTARIO),
            ("inv", false) => q.OrderByDescending(c => c.CARGADA_INVENTARIO),

            _ => q.OrderByDescending(c => c.FECHA_COMPRA)
        };

        // Conteo
        int total = await q.CountAsync();

        // Paginación segura
        int totalPages = (int)Math.Ceiling(total / (double)PageSize);
        if (totalPages == 0) totalPages = 1;
        if (Page > totalPages) Page = totalPages;

        // Proyección a RowVM
        var rows = await q
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .Select(c => new CompraIndexRowVM
            {
                COMPRA_ID = c.COMPRA_ID,
                PROVEEDOR_ID = c.PROVEEDOR_ID,
                PROVEEDOR_NOMBRE = c.PROVEEDOR != null ? c.PROVEEDOR.EMPRESA : null,
                FECHA_COMPRA = c.FECHA_COMPRA,
                FECHA_ENTREGA_COMPRA = c.FECHA_ENTREGA_COMPRA,
                ESTADO_COMPRA_ID = c.ESTADO_COMPRA_ID,
                CARGADA_INVENTARIO = c.CARGADA_INVENTARIO,
            })
            .ToListAsync();

        // VM de índice
        var vm = new ComprasIndexVM
        {
            Items = rows,
            Search = Search,
            Estado = Estado,
            Sort = Sort,
            Dir = Dir,
            Page = Page,
            PageSize = PageSize,
            TotalPages = totalPages,
            TotalCount = total
        };

        return View(vm);
    }

    // -------------------------------------------
    // GET /Compras/Details/{id}
    // -------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var compra = await _db.COMPRA
            .Include(c => c.PROVEEDOR)
            .FirstOrDefaultAsync(c => c.COMPRA_ID == id);

        if (compra == null) return NotFound();

        var lineas = await _db.DETALLE_COMPRA
            .Where(d => d.COMPRA_ID == id)
            .Join(_db.PRODUCTO,
                  d => d.PRODUCTO_ID,
                  p => p.PRODUCTO_ID,
                  (d, p) => new CompraLineaVM
                  {
                      ProductoId = d.PRODUCTO_ID,
                      ProductoNombre = p.PRODUCTO_NOMBRE,    // ← nombre visible
                      ImagenProducto = p.IMAGEN_PRODUCTO,    // ← imagen
                      Cantidad = d.CANTIDAD,
                      PrecioCompra = d.PRECIO_COMPRA,
                      PrecioVenta = d.PRECIO_VENTA,       // ← precio venta
                      FechaVencimiento = d.FECHA_VENCIMIENTO,
                      CantidadRecibida = d.CANTIDAD_RECIBIDA
                  })
            .ToListAsync();

        var vm = new CompraDetailsVM
        {
            CompraId = compra.COMPRA_ID,
            EstadoId = compra.ESTADO_COMPRA_ID,
            EstadoNombre = compra.ESTADO_COMPRA_ID,
            ProveedorId = compra.PROVEEDOR_ID,
            ProveedorNombre = compra.PROVEEDOR?.EMPRESA ?? compra.PROVEEDOR_ID,
            FechaCompra = compra.FECHA_COMPRA,
            FechaEntregaCompra = compra.FECHA_ENTREGA_COMPRA,
            CargadaInventario = compra.CARGADA_INVENTARIO,
            FechaCarga = compra.FECHA_CARGA,
            Observaciones = compra.OBSERVACIONES_COMPRA,
            Lineas = lineas,

            PuedeAgregarEliminar = compra.ESTADO_COMPRA_ID is "BOR" or "REV" or "APR" or "ENV",
            PuedeEditarPrecio = compra.ESTADO_COMPRA_ID is "CON",
            PuedeMarcarRecibida = compra.ESTADO_COMPRA_ID is "CON",
            PuedeCargarInventario = compra.ESTADO_COMPRA_ID == "REC" && !compra.CARGADA_INVENTARIO,
            PuedeAnular = (compra.ESTADO_COMPRA_ID != "CER" && compra.ESTADO_COMPRA_ID != "ANU" && compra.ESTADO_COMPRA_ID != "REC")
                          || (compra.ESTADO_COMPRA_ID == "REC" && !compra.CARGADA_INVENTARIO)
        };

        await CargarMetodosPagoAsync();

        return View(vm);
    }

    // =====================================
    // GET: /Compras/Create
    // =====================================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var nuevoId = await SiguienteCompraIdAsync();

        var productosMin = await _db.PRODUCTO
            .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
            .OrderBy(p => p.nombre)
            .ToListAsync();

        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);

        // Solo proveedores ACTIVO = true (y no eliminados)
        ViewBag.Proveedores = await BuildProveedoresSelectAsync();

        var vm = new CompraCreateVM
        {
            CompraId = nuevoId,
            Lineas = new List<CompraLineaVM> { new CompraLineaVM() }
        };
        return View(vm);
    }

    // -------------------------------------------
    // POST /Compras/Create
    // -------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompraCreateVM vm)
    {
        var productosMin = await _db.PRODUCTO
            .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
            .OrderBy(p => p.nombre)
            .ToListAsync();

        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);

        // Solo proveedores activos en el combo
        ViewBag.Proveedores = await BuildProveedoresSelectAsync(vm.ProveedorId);

        if (!ModelState.IsValid) return View(vm);

        // Validar proveedor activo
        var proveedorEsActivo = await _db.PROVEEDOR
            .AnyAsync(p => p.PROVEEDOR_ID == vm.ProveedorId && !p.ELIMINADO && p.ESTADO);
        if (!proveedorEsActivo)
        {
            ModelState.AddModelError(nameof(vm.ProveedorId), "Debes seleccionar un proveedor activo.");
            return View(vm);
        }

        if (string.IsNullOrWhiteSpace(vm.CompraId))
            vm.CompraId = await SiguienteCompraIdAsync();

        var lineasValidas = vm.Lineas?
            .Where(l => !string.IsNullOrWhiteSpace(l.ProductoId) && l.Cantidad > 0)
            .ToList() ?? new();

        if (lineasValidas.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Agrega al menos un producto con cantidad mayor a 0.");
            return View(vm);
        }

        // No duplicados
        var duplicados = lineasValidas
            .GroupBy(l => l.ProductoId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicados.Any())
        {
            ModelState.AddModelError(string.Empty, "No puedes repetir el mismo producto en varias líneas.");
            return View(vm);
        }

        var user = User?.Identity?.Name ?? "system";

        var compra = new COMPRA
        {
            COMPRA_ID = vm.CompraId,
            FECHA_COMPRA = DateTime.Now,
            ESTADO_COMPRA_ID = "BOR",
            PROVEEDOR_ID = vm.ProveedorId,
            OBSERVACIONES_COMPRA = vm.Observaciones,
            CARGADA_INVENTARIO = false,
            USUARIO_CREACION = user,
            ESTADO = true
        };
        _db.COMPRA.Add(compra);

        foreach (var l in lineasValidas)
        {
            _db.DETALLE_COMPRA.Add(new DETALLE_COMPRA
            {
                DETALLE_COMPRA_ID = Guid.NewGuid().ToString("N")[..10],
                COMPRA_ID = vm.CompraId,
                PRODUCTO_ID = l.ProductoId!,
                CANTIDAD = l.Cantidad,
                PRECIO_COMPRA = null,
                FECHA_VENCIMIENTO = null,
                SUBTOTAL = 0m,
                USUARIO_CREACION = user,
                ESTADO = true
            });
        }

        await _db.SaveChangesAsync();

        TempData["SwalTitle"] = "Compra registrada";
        TempData["SwalText"] = $"La compra \"{vm.CompraId}\" se guardó exitosamente en estado BORRADOR.";
        TempData["SwalIndexUrl"] = Url.Action("Index", "Compras");
        TempData["SwalCreateUrl"] = Url.Action("Create", "Compras");

        return RedirectToAction(nameof(Create));
    }

    // =====================================
    // GET: /Compras/Edit
    // =====================================
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var compra = await _db.COMPRA
            .Include(c => c.PROVEEDOR)
            .FirstOrDefaultAsync(c => c.COMPRA_ID == id);

        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID is not ("BOR" or "REV" or "APR"))
        {
            TempData["err"] = "Solo puedes modificar compras en BORRADOR, EN_REVISION o APROBADA.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var productosMin = await _db.PRODUCTO
            .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
            .OrderBy(p => p.nombre)
            .ToListAsync();
        //var productosMin = await GetProductosActivosInventarioAsync();
        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);

        ViewBag.Proveedores = await BuildProveedoresSelectAsync(compra.PROVEEDOR_ID, includeSelectedIfInactive: true);

        var detalles = await _db.DETALLE_COMPRA
            .Where(d => d.COMPRA_ID == id)
            .Select(d => new CompraLineaEditVM
            {
                DetalleCompraId = d.DETALLE_COMPRA_ID,
                ProductoId = d.PRODUCTO_ID,
                Cantidad = d.CANTIDAD
            })
            .ToListAsync();

        var vm = new CompraEditVM
        {
            CompraId = compra.COMPRA_ID,
            ProveedorId = compra.PROVEEDOR_ID,
            ProveedorNombre = compra.PROVEEDOR?.EMPRESA ?? compra.PROVEEDOR_ID,
            Observaciones = compra.OBSERVACIONES_COMPRA,
            Lineas = detalles.Count > 0 ? detalles : new List<CompraLineaEditVM> { new CompraLineaEditVM() }
        };

        return View(vm);
    }

    // ===============================
    // POST: /Compras/Edit/{id}
    // ===============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, CompraEditVM vm)
    {
        if (id != vm.CompraId) return NotFound();

        var compra = await _db.COMPRA
            .Include(c => c.DETALLE_COMPRA)
            .FirstOrDefaultAsync(c => c.COMPRA_ID == id);
        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID is not ("BOR" or "REV" or "APR"))
        {
            TempData["err"] = "Solo puedes modificar compras en BORRADOR, EN_REVISION o APROBADA.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!ModelState.IsValid)
        {
            await CargarCatalogosEditAsync(compra.PROVEEDOR_ID);
            return View(vm);
        }

        // Validar proveedor activo al guardar
        var proveedorEsActivo = await _db.PROVEEDOR
            .AnyAsync(p => p.PROVEEDOR_ID == vm.ProveedorId && !p.ELIMINADO && p.ESTADO);
        if (!proveedorEsActivo)
        {
            ModelState.AddModelError(nameof(vm.ProveedorId), "Debes seleccionar un proveedor activo.");
            await CargarCatalogosEditAsync(compra.PROVEEDOR_ID);
            return View(vm);
        }

        var nuevoProveedor = (vm.ProveedorId ?? "").Trim();
        var nuevasObs = string.IsNullOrWhiteSpace(vm.Observaciones) ? null : vm.Observaciones.Trim();

        var lineasValidas = (vm.Lineas ?? new List<CompraLineaEditVM>())
            .Where(l => !string.IsNullOrWhiteSpace(l.ProductoId) && l.Cantidad > 0)
            .ToList();

        if (lineasValidas.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Agrega al menos un producto con cantidad mayor a 0.");
            await CargarCatalogosEditAsync(compra.PROVEEDOR_ID);
            return View(vm);
        }

        var duplicados = lineasValidas
            .GroupBy(l => l.ProductoId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicados.Any())
        {
            ModelState.AddModelError(string.Empty, "No puedes repetir el mismo producto en varias líneas.");
            await CargarCatalogosEditAsync(compra.PROVEEDOR_ID);
            return View(vm);
        }

        bool hayCambiosCab =
            !string.Equals(compra.PROVEEDOR_ID, nuevoProveedor) ||
            !string.Equals(compra.OBSERVACIONES_COMPRA, nuevasObs);

        var mapExistentesPorId = compra.DETALLE_COMPRA.ToDictionary(d => d.DETALLE_COMPRA_ID);
        var idsEnVM = new HashSet<string>(lineasValidas.Where(l => !string.IsNullOrWhiteSpace(l.DetalleCompraId))
                                                       .Select(l => l.DetalleCompraId!));

        bool hayCambiosDet = false;

        foreach (var l in lineasValidas)
        {
            if (!string.IsNullOrWhiteSpace(l.DetalleCompraId) &&
                mapExistentesPorId.TryGetValue(l.DetalleCompraId!, out var d))
            {
                if (d.PRODUCTO_ID != l.ProductoId || d.CANTIDAD != l.Cantidad)
                {
                    d.PRODUCTO_ID = l.ProductoId!;
                    d.CANTIDAD = l.Cantidad;
                    d.SUBTOTAL = 0m;
                    hayCambiosDet = true;
                }
            }
            else
            {
                _db.DETALLE_COMPRA.Add(new DETALLE_COMPRA
                {
                    DETALLE_COMPRA_ID = Guid.NewGuid().ToString("N")[..10],
                    COMPRA_ID = compra.COMPRA_ID,
                    PRODUCTO_ID = l.ProductoId!,
                    CANTIDAD = l.Cantidad,
                    PRECIO_COMPRA = null,
                    FECHA_VENCIMIENTO = null,
                    SUBTOTAL = 0m,
                    USUARIO_CREACION = User?.Identity?.Name ?? "system",
                    ESTADO = true
                });
                hayCambiosDet = true;
            }
        }

        foreach (var d in compra.DETALLE_COMPRA.ToList())
        {
            if (!idsEnVM.Contains(d.DETALLE_COMPRA_ID) &&
                !lineasValidas.Any(x => string.IsNullOrWhiteSpace(x.DetalleCompraId) &&
                                        x.ProductoId == d.PRODUCTO_ID &&
                                        x.Cantidad == d.CANTIDAD))
            {
                _db.DETALLE_COMPRA.Remove(d);
                hayCambiosDet = true;
            }
        }

        if (hayCambiosCab)
        {
            compra.PROVEEDOR_ID = nuevoProveedor;
            compra.OBSERVACIONES_COMPRA = nuevasObs;
            compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
            compra.FECHA_MODIFICACION = DateTime.Now;
        }

        bool hayCambios = hayCambiosCab || hayCambiosDet;

        if (!hayCambios)
        {
            TempData["SwalOneBtnFlag"] = "nochange";
            TempData["SwalTitle"] = "Sin cambios";
            TempData["SwalText"] = "No se modificó ningún dato.";
            return RedirectToAction(nameof(Edit), new { id = compra.COMPRA_ID });
        }

        await _db.SaveChangesAsync();

        TempData["SwalOneBtnFlag"] = "updated";
        TempData["SwalTitle"] = "¡Compra actualizada!";
        TempData["SwalText"] = $"La compra \"{compra.COMPRA_ID}\" se actualizó correctamente.";

        return RedirectToAction(nameof(Edit), new { id = compra.COMPRA_ID });
    }

    // -------------------------------------------
    // POST /Compras/Revision
    // -------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToReview(string id)
    {
        var c = await _db.COMPRA.FirstOrDefaultAsync(x => x.COMPRA_ID == id);
        if (c == null) return NotFound();

        if (c.ESTADO_COMPRA_ID != "BOR" && c.ESTADO_COMPRA_ID != "REV")
        {
            TempData["err"] = "Solo compras en BORRADOR pueden pasar a EN_REVISIÓN.";
            return RedirectToAction(nameof(Details), new { id });
        }

        c.ESTADO_COMPRA_ID = "REV";
        c.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        c.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync();
        TempData["ok"] = "Compra enviada a revisión.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------
    // POST /Compras/Approve/{id}
    // REV -> APR (idempotente si ya está en APR)
    // -------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id)
    {
        var compra = await _db.COMPRA.FindAsync(id);
        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID != "REV" && compra.ESTADO_COMPRA_ID != "APR")
        {
            TempData["err"] = "Solo compras en EN_REVISIÓN pueden pasar a APROBADA.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var tieneLineas = await _db.DETALLE_COMPRA.AnyAsync(d => d.COMPRA_ID == id);
        if (!tieneLineas)
        {
            TempData["err"] = "No puedes aprobar una compra sin líneas.";
            return RedirectToAction(nameof(Details), new { id });
        }

        compra.ESTADO_COMPRA_ID = "APR";
        compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        compra.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync();
        TempData["ok"] = "Compra aprobada.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // -------------------------------------------
    // POST /Compras/Send/{id}
    // APR -> ENV (idempotente si ya está en ENV)
    // -------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string id)
    {
        var compra = await _db.COMPRA.FindAsync(id);
        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID != "APR" && compra.ESTADO_COMPRA_ID != "ENV")
        {
            TempData["err"] = "Solo se puede enviar una compra APROBADA.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Mantener idempotencia
        compra.ESTADO_COMPRA_ID = "ENV";
        compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        compra.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync();

        TempData["SwalOC_Flag"] = "oc_ready";
        TempData["SwalOC_Title"] = "Orden de compra";
        TempData["SwalOC_Text"] = "¿Deseas descargar la Orden de Compra en PDF ahora?";
        TempData["SwalOC_Url"] = Url.Action("OrdenCompra", "Compras", new { id }, Request.Scheme);

        TempData["ok"] = "Compra enviada al proveedor.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ==========================================
    // GET: /Compras/OrdenCompra/{id}  -> PDF
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> OrdenCompra(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var cab = await (from c in _db.COMPRA
                         join prov in _db.PROVEEDOR on c.PROVEEDOR_ID equals prov.PROVEEDOR_ID into gj
                         from prov in gj.DefaultIfEmpty()
                         where c.COMPRA_ID == id
                         select new
                         {
                             c.COMPRA_ID,
                             c.FECHA_COMPRA,
                             PROVEEDOR = prov != null ? (prov.EMPRESA ?? prov.PROVEEDOR_ID) : c.PROVEEDOR_ID
                         }).FirstOrDefaultAsync();

        if (cab == null) return NotFound();

        var lineas = await (from d in _db.DETALLE_COMPRA
                            join p in _db.PRODUCTO on d.PRODUCTO_ID equals p.PRODUCTO_ID
                            where d.COMPRA_ID == id
                            select new
                            {
                                Codigo = d.PRODUCTO_ID,
                                Producto = p.PRODUCTO_NOMBRE ?? d.PRODUCTO_ID,
                                Imagen = p.IMAGEN_PRODUCTO,
                                Cantidad = d.CANTIDAD
                                // Precios/Subtotal irán en blanco en el PDF
                            }).ToListAsync();

        var recibo = new RECIBO
        {
            RECIBO_ID = NewReciboId(),        
            VENTA_ID = id,                    
            METODO_PAGO_ID = "OC",            
            MONTO = 0m,
            FECHA = DateTime.Now,
            USUARIO_CREACION = User?.Identity?.Name ?? "system",
            FECHA_CREACION = DateTime.Now,
            ESTADO = true
        };
        _db.RECIBO.Add(recibo);
        await _db.SaveChangesAsync();

        // ViewModel anónimo simple para el PDF
        var vm = new
        {
            CompraId = cab.COMPRA_ID,
            Fecha = cab.FECHA_COMPRA,
            Proveedor = cab.PROVEEDOR,
            Lineas = lineas.Select(l => new
            {
                l.Codigo,
                l.Producto,
                l.Imagen,
                l.Cantidad,
                PrecioCompra = (decimal?)null,  
                Subtotal = (decimal?)null       
            }).ToList(),
            Total = (decimal?)null              
        };

        return new Rotativa.AspNetCore.ViewAsPdf("OrdenCompra", vm)
        {
            FileName = $"OrdenCompra_{cab.COMPRA_ID}.pdf",
            ContentDisposition = Rotativa.AspNetCore.Options.ContentDisposition.Inline,
            PageMargins = new Rotativa.AspNetCore.Options.Margins(15, 10, 15, 10),
            PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
            PageSize = Rotativa.AspNetCore.Options.Size.A5
        };
    }

    // -------------------------------------------
    // GET /Compras/Confirm/{id}
    // Muestra fecha de entrega y precios editables
    // -------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Confirm(string id)
    {
        var compra = await _db.COMPRA.FirstOrDefaultAsync(c => c.COMPRA_ID == id);
        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID is not ("APR" or "ENV" or "CON"))
        {
            TempData["err"] = "Solo se puede confirmar una compra aprobada o enviada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        //var lineas = await _db.DETALLE_COMPRA
        //    .Where(d => d.COMPRA_ID == id)
        //    .Select(d => new CompraLineaVM
        //    {
        //        ProductoId = d.PRODUCTO_ID,
        //        Cantidad = d.CANTIDAD,
        //        PrecioCompra = d.PRECIO_COMPRA,
        //        FechaVencimiento = d.FECHA_VENCIMIENTO
        //    }).ToListAsync();

        
        var lineas = await (
            from d in _db.DETALLE_COMPRA
            join p in _db.PRODUCTO on d.PRODUCTO_ID equals p.PRODUCTO_ID
            where d.COMPRA_ID == id
            select new CompraLineaVM
            {
                ProductoId = d.PRODUCTO_ID,
                Cantidad = d.CANTIDAD,
                PrecioCompra = d.PRECIO_COMPRA,
                FechaVencimiento = d.FECHA_VENCIMIENTO,

                ImagenProducto = p.IMAGEN_PRODUCTO,
                ProductoNombre = p.PRODUCTO_NOMBRE
            }
        ).ToListAsync();

        var vm = new CompraConfirmarVM
        {
            CompraId = id,
            FechaEntregaCompra = compra.FECHA_ENTREGA_COMPRA ?? DateOnly.FromDateTime(DateTime.Today),
            FechaCompra = compra.FECHA_COMPRA,
            Lineas = lineas
        };

        return View(vm);
    }

    // -------------------------------------------
    // POST /Compras/Confirm
    // Exige fecha + precios; pasa a CON
    // -------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(CompraConfirmarVM vm)
    {
        var compra = await _db.COMPRA
            .Include(c => c.DETALLE_COMPRA)
            .FirstOrDefaultAsync(c => c.COMPRA_ID == vm.CompraId);

        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID is not ("APR" or "ENV" or "CON"))
        {
            TempData["err"] = "Solo se puede confirmar una compra aprobada o enviada.";
            return RedirectToAction(nameof(Details), new { id = vm.CompraId });
        }

        // Validaciones
        if (vm.FechaEntregaCompra == default)
            ModelState.AddModelError(nameof(vm.FechaEntregaCompra), "La fecha de entrega es obligatoria.");
        if (vm.Lineas == null || vm.Lineas.Count == 0)
            ModelState.AddModelError(string.Empty, "Debe existir al menos una línea.");
        if (vm.Lineas != null && vm.Lineas.Any(l => l.PrecioCompra == null || l.PrecioCompra < 0))
            ModelState.AddModelError(string.Empty, "Todas las líneas deben tener PRECIO_COMPRA válido (>= 0).");
        if (vm.FechaEntregaCompra < DateOnly.FromDateTime(compra.FECHA_COMPRA))
            ModelState.AddModelError(nameof(vm.FechaEntregaCompra),
                $"La fecha de entrega no puede ser anterior a la fecha de compra ({compra.FECHA_COMPRA:dd/MM/yyyy}).");

        //if (!ModelState.IsValid)

        //    return View(vm);

        if (!ModelState.IsValid)
        {
            // Volver a mostrar vista con errores
            vm.Lineas = vm.Lineas ?? new();
            return View(vm);
        }

        // Mapear precios a detalle (y recalculo referencial)
        var preciosPorProd = vm.Lineas.ToDictionary(l => l.ProductoId, l => l.PrecioCompra!.Value);
        foreach (var d in compra.DETALLE_COMPRA)
        {
            if (preciosPorProd.TryGetValue(d.PRODUCTO_ID, out var nuevoPrecio))
            {
                d.PRECIO_COMPRA = nuevoPrecio;
                d.SUBTOTAL = nuevoPrecio * d.CANTIDAD; // previo a recibir
            }
        }

        compra.FECHA_ENTREGA_COMPRA = vm.FechaEntregaCompra;
        compra.ESTADO_COMPRA_ID = "CON";
        compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        compra.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync();

        // Aquí iría el registro en PRECIO_HISTORICO (cuando lo actives).

        TempData["ok"] = "Compra confirmada. Ahora puedes registrar la recepción.";
        return RedirectToAction(nameof(Details), new { id = vm.CompraId });
    }

    // ======================
    // GET: /Compras/Recibir/{id}
    // Muestra el formulario para capturar CANTIDAD_RECIBIDA por renglón.
    // Solo se permite si la compra está CONFIRMADA (CON).
    // ======================
    [HttpGet]
    public async Task<IActionResult> Recibir(string id)
    {
        var compra = await _db.COMPRA.FirstOrDefaultAsync(c => c.COMPRA_ID == id);
        if (compra == null) return NotFound();

        // Regla de flujo: solo compras CONFIRMADAS pueden pasar a RECIBIDA
        if (compra.ESTADO_COMPRA_ID != "CON")
        {
            TempData["err"] = "Solo compras CONFIRMADAS pueden registrar recepción.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new CompraRecibirVM
        {
            CompraId = id,
            Lineas = await _db.DETALLE_COMPRA
            .Where(d => d.COMPRA_ID == id)
            .Join(_db.PRODUCTO,
                  d => d.PRODUCTO_ID,
                  p => p.PRODUCTO_ID,
                  (d, p) => new CompraLineaVM
                  {
                      ProductoId = d.PRODUCTO_ID,
                      ProductoNombre = p.PRODUCTO_NOMBRE,
                      ImagenProducto = p.IMAGEN_PRODUCTO,
                      Cantidad = d.CANTIDAD,
                      PrecioCompra = d.PRECIO_COMPRA,
                      PrecioVenta = d.PRECIO_VENTA,
                      CantidadRecibida = d.CANTIDAD_RECIBIDA,
                  })
            .ToListAsync()
        };

        return View(vm);
    }

    // ======================
    // POST: /Compras/Recibir/{id}
    // ======================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recibir(string id, CompraRecibirVM vm)
    {
        if (id != vm.CompraId) return BadRequest();

        var compra = await _db.COMPRA
            .Include(c => c.DETALLE_COMPRA)
            .FirstOrDefaultAsync(c => c.COMPRA_ID == id);

        if (compra == null) return NotFound();

        // Solo desde CONFIRMADA
        if (compra.ESTADO_COMPRA_ID != "CON")
        {
            TempData["err"] = "Solo compras CONFIRMADAS pueden pasar a RECIBIDA.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Debe venir la colección de líneas
        if (vm.Lineas == null || vm.Lineas.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No hay líneas para recibir.");
            return View(vm); // devolvemos la vista con errores
        }

        // Mapeo rápido: ProductoId -> CantidadRecibida
        var mapRec = vm.Lineas.ToDictionary(x => x.ProductoId, x => x.CantidadRecibida);
        var mapVenta = vm.Lineas.ToDictionary(x => x.ProductoId, x => x.PrecioVenta);

        // Validaciones fuertes por renglón
        foreach (var d in compra.DETALLE_COMPRA)
        {
            if (!mapRec.TryGetValue(d.PRODUCTO_ID, out var recVm))
            {
                ModelState.AddModelError(string.Empty, $"Falta cantidad recibida de producto {d.PRODUCTO_ID}.");
                continue;
            }

            // PrecioCompra debe existir desde CONFIRMADA
            if (d.PRECIO_COMPRA == null || d.PRECIO_COMPRA < 0)
                ModelState.AddModelError(string.Empty, $"El producto {d.PRODUCTO_ID} no tiene precio de compra válido.");

            var rec = recVm ?? -1;
            if (rec < 0)
                ModelState.AddModelError(string.Empty, $"Ingresa la cantidad recibida para {d.PRODUCTO_ID}.");
            if (rec > d.CANTIDAD)
                ModelState.AddModelError(string.Empty, $"La cantidad recibida de {d.PRODUCTO_ID} no puede superar la pedida ({d.CANTIDAD}).");

            if (!mapVenta.TryGetValue(d.PRODUCTO_ID, out var pv) || pv is null)
            {
                ModelState.AddModelError(string.Empty, $"Ingresa el precio de venta para {d.PRODUCTO_ID}.");
            }
            else if (d.PRECIO_COMPRA.HasValue && pv.Value < d.PRECIO_COMPRA.Value)
            {
                ModelState.AddModelError(string.Empty,
                    $"El precio de venta de {d.PRODUCTO_ID} no puede ser menor al precio de compra ({d.PRECIO_COMPRA:0.##}).");
            }
        }

        if (!ModelState.IsValid)
            return View(vm); // mostrar errores en la misma vista

        // Aplicar cantidades y recalcular SUBTOTAL = recibida × precio
        foreach (var d in compra.DETALLE_COMPRA)
        {
            var rec = mapRec[d.PRODUCTO_ID] ?? 0;
            d.CANTIDAD_RECIBIDA = rec;
            d.SUBTOTAL = (d.PRECIO_COMPRA ?? 0) * rec;
            // GUARDAR PRECIO_VENTA
            var pv = mapVenta[d.PRODUCTO_ID];
            if (pv.HasValue) d.PRECIO_VENTA = pv.Value;
        }

        // Cambiar estado a REC; faltantes se consideran cancelados (política)
        //compra.ESTADO_COMPRA_ID = "REC";
        compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        compra.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync();

        // ====== Actualizar PRECIO_HISTORICO si el usuario ingresó PrecioVenta ======
        var cambiosPrecio = vm.Lineas
            .Where(l => l.PrecioVenta.HasValue) // solo los que el usuario llenó
            .Select(l => new { l.ProductoId, Precio = l.PrecioVenta!.Value })
            .ToList();

        if (cambiosPrecio.Count > 0)
        {
            var user = User?.Identity?.Name ?? "system";
            var now = DateTime.Now;
            var ids = cambiosPrecio.Select(c => c.ProductoId).Distinct().ToList();

            // Traer vigentes actuales para cerrarlos si cambia el valor
            var vigentes = await _db.PRECIO_HISTORICO
                .Where(ph => ids.Contains(ph.PRODUCTO_ID) && ph.HASTA == null && !ph.ELIMINADO && ph.ESTADO)
                .ToListAsync();

            foreach (var c in cambiosPrecio)
            {
                var vigente = vigentes.FirstOrDefault(v => v.PRODUCTO_ID == c.ProductoId);

                // Si ya es el mismo precio vigente, no hacemos nada
                if (vigente != null && vigente.PRECIO == c.Precio) continue;

                // Cerrar vigente (si había)
                if (vigente != null)
                {
                    vigente.HASTA = now;
                    vigente.USUARIO_MODIFICACION = user;
                    vigente.FECHA_MODIFICACION = now;
                }

                // Insertar nuevo vigente
                var nuevo = new PRECIO_HISTORICO
                {
                    PRECIO_ID = Guid.NewGuid().ToString("N").Substring(0, 10),
                    PRODUCTO_ID = c.ProductoId,
                    PRECIO = c.Precio,
                    DESDE = now,
                    ESTADO = true,
                    USUARIO_CREACION = user,
                    FECHA_CREACION = now
                };
                _db.PRECIO_HISTORICO.Add(nuevo);
            }

            await _db.SaveChangesAsync();
        }
        // ====== /PRECIO_HISTORICO ======

        TempData["ok"] = "Recepción registrada. Ahora puedes cargar a inventario.";
        // Sugerencia de pago (recibida × precio_compra)
        var montoSugerido = await _db.DETALLE_COMPRA
            .Where(d => d.COMPRA_ID == id)
            .SumAsync(d => ((decimal?)(d.CANTIDAD_RECIBIDA ?? 0)) * (d.PRECIO_COMPRA ?? 0m));

        // Bandera para abrir modal de pago al volver a Details
        TempData["SwalPC_Flag"] = "pc_ready";
        TempData["SwalPC_Monto"] = (montoSugerido ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture);
        // Mantén tu mensaje OK existente
        TempData["ok"] = "Recepción registrada. Ahora puedes cargar a inventario.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ===============================================
    // POST: /Compras/PagarCompra/{id}
    // ===============================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PagarCompra(string id, CancellationToken ct)
    {
        // 1) Cargar compra
        var compra = await _db.COMPRA.FirstOrDefaultAsync(c => c.COMPRA_ID == id, ct);
        if (compra == null) return NotFound();

        // Solo tiene sentido pagar si la compra ya fue REC (o CON si decides permitir anticipo a proveedor)
        if (compra.ESTADO_COMPRA_ID is not ("REC" or "CON"))
        {
            TempData["err"] = "La compra aún no está lista para registrar el pago.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 2) Leer formulario 
        string metodo = (Request.Form["MetodoPagoId"].ToString() ?? "").Trim();
        string montoStr = (Request.Form["MontoPago"].ToString() ?? "0").Trim();

        static decimal ParseMoney(string s)
        {
            s = (s ?? "").Trim().Replace("Q", "", StringComparison.OrdinalIgnoreCase).Replace(",", "");
            return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        if (string.IsNullOrWhiteSpace(metodo))
        {
            TempData["err"] = "Debe seleccionar el método de pago.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var monto = ParseMoney(montoStr);
        if (monto <= 0)
        {
            TempData["err"] = "Monto a pagar inválido.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 3) Monto sugerido (opcional) = total recibido * precio_compra 
        var totalRecibido = await _db.DETALLE_COMPRA
            .Where(d => d.COMPRA_ID == id)
            .SumAsync(d => ((decimal?)(d.CANTIDAD_RECIBIDA ?? 0)) * (d.PRECIO_COMPRA ?? 0m), ct);


        // 4) Transacción: RECIBO + CAJA (EGRESO)
        await using var trx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var user = User?.Identity?.Name ?? "system";

            // 4.1) RECIBO (folio libre para compras)
            var recibo = new RECIBO
            {
                RECIBO_ID = NewReciboId(), // "OCxxxxxxxx" o reusa otro prefijo si prefieres
                VENTA_ID = id,             // referenciamos la COMPRA_ID
                METODO_PAGO_ID = metodo,
                MONTO = monto,
                FECHA = DateTime.Now,
                USUARIO_CREACION = user,
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            };
            _db.RECIBO.Add(recibo);
            await _db.SaveChangesAsync(ct);

            // 4.2) Movimiento de CAJA (EGRESO) si hay sesión
            var sesion = await GetSesionCajaActivaAsync(user);
            if (!string.IsNullOrWhiteSpace(sesion))
            {
                var mov = new MOVIMIENTO_CAJA
                {
                    MOVIMIENTO_ID = await SiguienteMovimientoCajaIdAsync(),
                    SESION_ID = sesion,
                    TIPO = "EGRESO",
                    MONTO = monto,
                    REFERENCIA = id,
                    FECHA = DateTime.Now,
                    USUARIO_CREACION = user,
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                };
                _db.MOVIMIENTO_CAJA.Add(mov);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                await _bitacora.LogAsync("CAJA_SESION", "WARN", user,
                    $"Pago de COMPRA {id} por Q{monto:N2} sin sesión de caja activa.", ct);
            }

            var cOMPRA = await _db.COMPRA.FirstOrDefaultAsync(c => c.COMPRA_ID == id, ct);
            if (compra != null)
            {
                compra.ESTADO_COMPRA_ID = "REC";
                compra.USUARIO_MODIFICACION = user;
                compra.FECHA_MODIFICACION = DateTime.Now;
                await _db.SaveChangesAsync(ct);
            }

            await trx.CommitAsync(ct);
            TempData["ok"] = $"Pago de la compra registrado por Q{monto:N2}.";
        }
        catch (Exception ex)
        {
            await trx.RollbackAsync(ct);
            TempData["err"] = "No se pudo registrar el pago: " + ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ===============================================
    // POST /Compras/CargarInventario/{id}
    // Idempotente: solo si ESTADO=REC y CARGADA_INVENTARIO=0
    // Efectos: KARDEX + INVENTARIO + (COMPRA -> CARGADA=1, FECHA_CARGA, CER)
    // ===============================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CargarInventario(string id)
    {
        // 1) Traer cabecera
        var compra = await _db.COMPRA.FirstOrDefaultAsync(c => c.COMPRA_ID == id);
        if (compra == null) return NotFound();

        // 2) Idempotencia y regla de estado
        if (compra.ESTADO_COMPRA_ID != "REC" || compra.CARGADA_INVENTARIO)
        {
            TempData["err"] = "La compra no está lista o ya fue cargada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 3) Traer renglones
        var detalles = await _db.DETALLE_COMPRA.Where(d => d.COMPRA_ID == id).ToListAsync();

        // 4) Validación final: precio y recibida presentes
        if (detalles.Any(d => d.PRECIO_COMPRA == null || d.CANTIDAD_RECIBIDA == null))
        {
            TempData["err"] = "Hay líneas sin precio o sin cantidad recibida. Revisa antes de cargar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 5) Transacción para garantizar atomicidad
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var user = User?.Identity?.Name ?? "system";

            foreach (var d in detalles)
            {
                // 5.1) KARDEX: un movimiento por línea
                var mov = new KARDEX
                {
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10],
                    PRODUCTO_ID = d.PRODUCTO_ID,
                    FECHA = DateTime.Now,
                    TIPO_MOVIMIENTO = "ENTRADA",
                    CANTIDAD = d.CANTIDAD_RECIBIDA!.Value,
                    COSTO_UNITARIO = d.PRECIO_COMPRA!.Value, // se mantiene para registro histórico
                    REFERENCIA = id,
                    USUARIO_CREACION = user,
                    ESTADO = true
                };
                _db.KARDEX.Add(mov);

                // 5.2) INVENTARIO: sumar stock (crear si no existe)
                var inv = await _db.INVENTARIO
                    .FirstOrDefaultAsync(i => i.PRODUCTO_ID == d.PRODUCTO_ID);

                if (inv == null)
                {
                    inv = new INVENTARIO
                    {
                        INVENTARIO_ID = Guid.NewGuid().ToString("N")[..10],
                        PRODUCTO_ID = d.PRODUCTO_ID,
                        STOCK_ACTUAL = 0,
                        STOCK_MINIMO = 0,
                        COSTO_UNITARIO = 0,
                        ESTADO = true,
                        USUARIO_CREACION = user
                    };
                    _db.INVENTARIO.Add(inv);
                }

                // Sumar lo recibido
                var recibida = d.CANTIDAD_RECIBIDA!.Value;
                inv.STOCK_ACTUAL += recibida;

                // 🔹 NUEVO: actualizar costo unitario SOLO si se recibió algo (> 0)
                //     y se tiene PRECIO_VENTA registrado en el detalle
                if (recibida > 0 && d.PRECIO_VENTA.HasValue)
                {
                    inv.COSTO_UNITARIO = d.PRECIO_VENTA.Value;
                }
            }

            // 5.3) Marcar compra como cargada y cerrar
            compra.CARGADA_INVENTARIO = true;
            compra.FECHA_CARGA = DateTime.Now;
            compra.ESTADO_COMPRA_ID = "CER";
            compra.USUARIO_MODIFICACION = user;
            compra.FECHA_MODIFICACION = DateTime.Now;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["ok"] = "Inventario actualizado con el último precio de venta y compra cerrada.";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            TempData["err"] = "Error al cargar inventario: " + ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }


    // -------------------------------------------
    // POST /Compras/Anular/{id}
    // Disponible hasta REC (si no cargada/ni cerrada)
    // -------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(string id)
    {
        var compra = await _db.COMPRA.FindAsync(id);
        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID == "CER" || compra.CARGADA_INVENTARIO)
        {
            TempData["err"] = "No se puede anular una compra cerrada o ya cargada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (compra.ESTADO_COMPRA_ID is "BOR" or "REV" or "APR" or "ENV" or "CON" or "REC")
        {
            compra.ESTADO_COMPRA_ID = "ANU";
            compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
            compra.FECHA_MODIFICACION = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["ok"] = "Compra anulada.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["err"] = "No se puede anular en el estado actual.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ==========Reporte PDF================================
    [HttpGet]
    public async Task<IActionResult> ReportePDF(
    string? Search,
    string? Estado,
    string? Sort = "fecha",
    string? Dir = "desc")
    {
        // Normalización básica
        Sort = string.IsNullOrWhiteSpace(Sort) ? "fecha" : Sort.Trim().ToLower();
        Dir = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
        bool asc = Dir == "asc";

        // Base query
        var q = _db.COMPRA
            .AsNoTracking()
            .Include(c => c.PROVEEDOR)
            .AsQueryable();

        // Filtro Search
        if (!string.IsNullOrWhiteSpace(Search))
        {
            string s = Search.Trim();
            q = q.Where(c =>
                c.COMPRA_ID.Contains(s) ||
                c.PROVEEDOR_ID.Contains(s) ||
                (c.PROVEEDOR != null && c.PROVEEDOR.EMPRESA.Contains(s))
            );
        }

        // Filtro Estado
        if (!string.IsNullOrWhiteSpace(Estado))
        {
            string e = Estado.Trim().ToUpper();
            q = q.Where(c => c.ESTADO_COMPRA_ID == e);
        }

        // Orden dinámico (mismo patrón que Index)
        q = (Sort, asc) switch
        {
            ("id", true) => q.OrderBy(c => c.COMPRA_ID),
            ("id", false) => q.OrderByDescending(c => c.COMPRA_ID),

            ("proveedor", true) => q.OrderBy(c => c.PROVEEDOR != null ? c.PROVEEDOR.EMPRESA : c.PROVEEDOR_ID)
                                   .ThenBy(c => c.PROVEEDOR_ID),
            ("proveedor", false) => q.OrderByDescending(c => c.PROVEEDOR != null ? c.PROVEEDOR.EMPRESA : c.PROVEEDOR_ID)
                                    .ThenByDescending(c => c.PROVEEDOR_ID),

            ("fecha", true) => q.OrderBy(c => c.FECHA_COMPRA),
            ("fecha", false) => q.OrderByDescending(c => c.FECHA_COMPRA),

            ("entrega", true) => q.OrderBy(c => c.FECHA_ENTREGA_COMPRA),
            ("entrega", false) => q.OrderByDescending(c => c.FECHA_ENTREGA_COMPRA),

            ("estado", true) => q.OrderBy(c => c.ESTADO_COMPRA_ID),
            ("estado", false) => q.OrderByDescending(c => c.ESTADO_COMPRA_ID),

            ("inv", true) => q.OrderBy(c => c.CARGADA_INVENTARIO),
            ("inv", false) => q.OrderByDescending(c => c.CARGADA_INVENTARIO),

            _ => q.OrderByDescending(c => c.FECHA_COMPRA)
        };

        // Proyección al mismo RowVM del Index
        var items = await q
            .Select(c => new CompraIndexRowVM
            {
                COMPRA_ID = c.COMPRA_ID,
                PROVEEDOR_ID = c.PROVEEDOR_ID,
                PROVEEDOR_NOMBRE = c.PROVEEDOR != null ? c.PROVEEDOR.EMPRESA : null,
                FECHA_COMPRA = c.FECHA_COMPRA,
                FECHA_ENTREGA_COMPRA = c.FECHA_ENTREGA_COMPRA,
                ESTADO_COMPRA_ID = c.ESTADO_COMPRA_ID,
                CARGADA_INVENTARIO = c.CARGADA_INVENTARIO,
            })
            .ToListAsync();

        int totalCompras = items.Count;
        int totalCargadas = items.Count(x => x.CARGADA_INVENTARIO);
        int totalNoCargadas = items.Count(x => !x.CARGADA_INVENTARIO);

        var vm = new ReporteViewModel<CompraIndexRowVM>
        {
            Items = items,
            Search = Search,
            Sort = Sort,
            Dir = Dir,

            ReportTitle = "Reporte de Compras",
            CompanyInfo = "CreArte Manualidades | Sololá, Guatemala | creartemanualidades2021@gmail.com",
            GeneratedBy = User?.Identity?.Name ?? "Usuario no autenticado",
            LogoUrl = Url.Content("~/Imagenes/logoCreArte.png")
        };

        vm.AddTotal("TotalCompras", totalCompras);
        vm.AddTotal("CargadasInventario", totalCargadas);
        vm.AddTotal("NoCargadasInventario", totalNoCargadas);

        if (!string.IsNullOrWhiteSpace(Estado))
            vm.ExtraFilters["Estado"] = Estado;

        var pdf = new Rotativa.AspNetCore.ViewAsPdf("ReporteCompras", vm)
        {
            FileName = $"ReporteCompras.pdf",
            ContentDisposition = Rotativa.AspNetCore.Options.ContentDisposition.Inline,
            PageSize = Rotativa.AspNetCore.Options.Size.Letter,
            PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
            PageMargins = new Rotativa.AspNetCore.Options.Margins
            {
                Left = 10,
                Right = 10,
                Top = 10,
                Bottom = 10
            },
            CustomSwitches =
                $"--footer-center \"Página [page] de [toPage]\"" +
                $" --footer-right \"CreArte Manualidades © {DateTime.Now:yyyy}\"" +
                $" --footer-font-size 9 --footer-spacing 3 --footer-line"
        };

        return pdf;
    }

}
