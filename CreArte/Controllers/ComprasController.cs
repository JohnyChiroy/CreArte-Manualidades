using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial; // <-- VMs

public class ComprasController : Controller
{
    private readonly CreArteDbContext _db;

    public ComprasController(CreArteDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // GET /Compras?Search=&Estado=&Sort=&Dir=&Page=&PageSize=
    // Listado con búsqueda, filtro por estado, orden y paginación
    // - Mapea PROVEEDOR_NOMBRE desde PROVEEDOR.EMPRESA
    // - Coherente con tu Index (Opción B)
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? Search,
        string? Estado,          // BOR/REV/APR/ENV/CON/REC/CER/ANU
        string? Sort = "fecha",  // id, proveedor, fecha, estado, inv
        string? Dir = "desc",   // asc/desc
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
    // Muestra cabecera + renglones + flags de botones
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
            .Select(d => new CompraLineaVM
            {
                ProductoId = d.PRODUCTO_ID,
                Cantidad = d.CANTIDAD,
                PrecioCompra = d.PRECIO_COMPRA,
                FechaVencimiento = d.FECHA_VENCIMIENTO,
                CantidadRecibida = d.CANTIDAD_RECIBIDA
            }).ToListAsync();

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

            // Flags de UI según flujo
            PuedeAgregarEliminar = compra.ESTADO_COMPRA_ID is "BOR" or "REV" or "APR" or "ENV",
            PuedeEditarPrecio = compra.ESTADO_COMPRA_ID is "CON",
            PuedeMarcarRecibida = compra.ESTADO_COMPRA_ID is "CON",
            PuedeCargarInventario = compra.ESTADO_COMPRA_ID == "REC" && !compra.CARGADA_INVENTARIO,
            PuedeAnular = (compra.ESTADO_COMPRA_ID != "CER" && compra.ESTADO_COMPRA_ID != "ANU"
                           && compra.ESTADO_COMPRA_ID != "REC")
                          || (compra.ESTADO_COMPRA_ID == "REC" && !compra.CARGADA_INVENTARIO)
        };

        return View(vm);
    }

    // =====================================
    // Helper: Genera IDs tipo CO00000000 (prefijo + 8 dígitos)
    // =====================================
    private async Task<string> SiguienteCompraIdAsync()
    {
        const string prefijo = "CO";
        const int ancho = 8; // CO + 8 dígitos

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


    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // 1) Genera ID (solo lectura en la vista + hidden real)
        var nuevoId = await SiguienteCompraIdAsync();

        // 2) Catálogo mínimo de productos para <select> y preview de imagen
        var productosMin = await _db.PRODUCTO
            .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
            .ToListAsync();

        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);

        // 3) Proveedores (igual que antes)
        ViewBag.Proveedores = new SelectList(_db.PROVEEDOR, "PROVEEDOR_ID", "EMPRESA");

        var vm = new CompraCreateVM
        {
            CompraId = nuevoId,
            Lineas = new List<CompraLineaVM> { new CompraLineaVM() }
        };
        return View(vm);
    }

    // -------------------------------------------
    // POST /Compras/Create
    // Crea BOR, admite líneas sin precio (se exigirá en CON)
    // -------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompraCreateVM vm)
    {
        // Re-cargar combos si hay error
        var productosMin = await _db.PRODUCTO
            .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
            .ToListAsync();
        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);
        ViewBag.Proveedores = new SelectList(_db.PROVEEDOR, "PROVEEDOR_ID", "EMPRESA");

        if (!ModelState.IsValid) return View(vm);

        // Generar ID si faltara
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

        var user = User?.Identity?.Name ?? "system";

        // Cabecera
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

        // Detalles
        foreach (var l in lineasValidas)
        {
            var det = new DETALLE_COMPRA
            {
                DETALLE_COMPRA_ID = Guid.NewGuid().ToString("N")[..10],
                COMPRA_ID = vm.CompraId,
                PRODUCTO_ID = l.ProductoId,
                CANTIDAD = l.Cantidad,
                PRECIO_COMPRA = null,
                FECHA_VENCIMIENTO = null,
                SUBTOTAL = 0m,
                USUARIO_CREACION = user,
                ESTADO = true
            };
            _db.DETALLE_COMPRA.Add(det);
        }

        await _db.SaveChangesAsync();

        TempData["SwalTitle"] = "Compra registrada";
        TempData["SwalText"] = $"La compra \"{vm.CompraId}\" se guardó exitosamente en estado BORRADOR.";
        TempData["SwalIndexUrl"] = Url.Action("Index", "Compras");   // Confirmar
        TempData["SwalCreateUrl"] = Url.Action("Create", "Compras");   // Denegar (nuevo)

        return RedirectToAction(nameof(Create));

    }

    // =====================================
    // GET: /Compras/Edit/{id}
    // - Solo BOR/REV/APR
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

        // Catálogo productos (para selects e imagen)
        var productosMin = await _db.PRODUCTO
            .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
            .ToListAsync();
        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);

        // Catálogo proveedores (editable)
        var proveedores = await _db.PROVEEDOR
            .Select(p => new { id = p.PROVEEDOR_ID, nombre = p.EMPRESA })
            .ToListAsync();
        ViewBag.Proveedores = new SelectList(proveedores, "id", "nombre", compra.PROVEEDOR_ID);

        // Detalle actual
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
            ProveedorId = compra.PROVEEDOR_ID,                             // <- editable
            ProveedorNombre = compra.PROVEEDOR?.EMPRESA ?? compra.PROVEEDOR_ID,
            Observaciones = compra.OBSERVACIONES_COMPRA,
            Lineas = detalles.Count > 0 ? detalles : new List<CompraLineaEditVM> { new CompraLineaEditVM() }
        };

        return View(vm);
    }


    // =====================================
    // POST: /Compras/Edit
    // - Aplica altas/bajas/cambios de líneas
    // =====================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CompraEditVM vm)
    {
        var compra = await _db.COMPRA
            .Include(c => c.DETALLE_COMPRA)
            .FirstOrDefaultAsync(c => c.COMPRA_ID == vm.CompraId);

        if (compra == null) return NotFound();

        if (compra.ESTADO_COMPRA_ID is not ("BOR" or "REV" or "APR"))
        {
            TempData["err"] = "La compra ya no puede modificarse en su estado actual.";
            return RedirectToAction(nameof(Details), new { id = vm.CompraId });
        }

        // ===== Recargar catálogos si hay que volver a la vista =====
        var productosMin = await _db.PRODUCTO
            .Select(p => new { id = p.PRODUCTO_ID, nombre = p.PRODUCTO_NOMBRE, imagen = p.IMAGEN_PRODUCTO })
            .ToListAsync();
        ViewBag.Productos = new SelectList(productosMin, "id", "nombre");
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(productosMin);

        var proveedores = await _db.PROVEEDOR
            .Select(p => new { id = p.PROVEEDOR_ID, nombre = p.EMPRESA })
            .ToListAsync();
        ViewBag.Proveedores = new SelectList(proveedores, "id", "nombre", vm.ProveedorId);

        // ===== Validaciones =====
        // Proveedor requerido y debe existir
        var provId = (vm.ProveedorId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(provId) || !await _db.PROVEEDOR.AnyAsync(p => p.PROVEEDOR_ID == provId))
        {
            ModelState.AddModelError(nameof(vm.ProveedorId), "Debe seleccionar un proveedor válido.");
        }

        var lineasValidas = vm.Lineas?
            .Where(l => !string.IsNullOrWhiteSpace(l.ProductoId) && l.Cantidad > 0)
            .ToList() ?? new();

        if (lineasValidas.Count == 0)
            ModelState.AddModelError(string.Empty, "Agrega al menos un producto con cantidad mayor a 0.");

        if (!ModelState.IsValid) return View(vm);

        // ===== Aplicar cambios =====
        // Cabecera
        compra.PROVEEDOR_ID = provId; // <- actualizar proveedor
        compra.OBSERVACIONES_COMPRA = string.IsNullOrWhiteSpace(vm.Observaciones) ? null : vm.Observaciones.Trim();
        compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        compra.FECHA_MODIFICACION = DateTime.Now;

        // Detalle (actualizar / crear / eliminar)
        var existentes = compra.DETALLE_COMPRA.ToDictionary(d => d.DETALLE_COMPRA_ID);
        var keepIds = new HashSet<string>(lineasValidas.Where(l => !string.IsNullOrEmpty(l.DetalleCompraId))
                                                       .Select(l => l.DetalleCompraId!));

        // Eliminar los que ya no vienen
        foreach (var del in existentes.Values.Where(d => !keepIds.Contains(d.DETALLE_COMPRA_ID)).ToList())
            _db.DETALLE_COMPRA.Remove(del);

        // Actualizar existentes y crear nuevos
        foreach (var l in lineasValidas)
        {
            if (!string.IsNullOrEmpty(l.DetalleCompraId) && existentes.TryGetValue(l.DetalleCompraId, out var det))
            {
                det.PRODUCTO_ID = l.ProductoId;
                det.CANTIDAD = l.Cantidad;
                det.SUBTOTAL = (det.PRECIO_COMPRA ?? 0m) * det.CANTIDAD; // referencial
            }
            else
            {
                _db.DETALLE_COMPRA.Add(new DETALLE_COMPRA
                {
                    DETALLE_COMPRA_ID = Guid.NewGuid().ToString("N")[..10],
                    COMPRA_ID = vm.CompraId,
                    PRODUCTO_ID = l.ProductoId,
                    CANTIDAD = l.Cantidad,
                    PRECIO_COMPRA = null,
                    FECHA_VENCIMIENTO = null,
                    SUBTOTAL = 0m,
                    USUARIO_CREACION = User?.Identity?.Name ?? "system",
                    ESTADO = true
                });
            }
        }

        await _db.SaveChangesAsync();
        TempData["ok"] = "Cambios guardados correctamente.";
        return RedirectToAction(nameof(Details), new { id = vm.CompraId });
    }

    // -------------------------------------------
    // POST /Compras/ToReview/{id}
    // BOR -> REV (idempotente si ya está en REV)
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

        compra.ESTADO_COMPRA_ID = "ENV";
        compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        compra.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync();
        TempData["ok"] = "Compra enviada al proveedor.";
        return RedirectToAction(nameof(Details), new { id });
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

        var lineas = await _db.DETALLE_COMPRA
            .Where(d => d.COMPRA_ID == id)
            .Select(d => new CompraLineaVM
            {
                ProductoId = d.PRODUCTO_ID,
                Cantidad = d.CANTIDAD,
                PrecioCompra = d.PRECIO_COMPRA,
                FechaVencimiento = d.FECHA_VENCIMIENTO
            }).ToListAsync();

        var vm = new CompraConfirmarVM
        {
            CompraId = id,
            FechaEntregaCompra = compra.FECHA_ENTREGA_COMPRA ?? DateOnly.FromDateTime(DateTime.Today),
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

        if (!ModelState.IsValid) return View(vm);

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
                .Select(d => new CompraLineaVM
                {
                    ProductoId = d.PRODUCTO_ID,
                    Cantidad = d.CANTIDAD,
                    PrecioCompra = d.PRECIO_COMPRA,
                    CantidadRecibida = d.CANTIDAD_RECIBIDA
                }).ToListAsync()
        };

        return View(vm);
    }

    // ======================
    // POST: /Compras/Recibir/{id}
    // Valida cantidades recibidas, recalcula SUBTOTAL (recibida × precio)
    // y cambia estado a REC. NO toca inventario todavía.
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
        }

        if (!ModelState.IsValid)
            return View(vm); // mostrar errores en la misma vista

        // Aplicar cantidades y recalcular SUBTOTAL = recibida × precio
        foreach (var d in compra.DETALLE_COMPRA)
        {
            var rec = mapRec[d.PRODUCTO_ID] ?? 0;
            d.CANTIDAD_RECIBIDA = rec;
            d.SUBTOTAL = (d.PRECIO_COMPRA ?? 0) * rec;
        }

        // Cambiar estado a REC; faltantes se consideran cancelados (política)
        compra.ESTADO_COMPRA_ID = "REC";
        compra.USUARIO_MODIFICACION = User?.Identity?.Name ?? "system";
        compra.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync();

        TempData["ok"] = "Recepción registrada. Ahora puedes cargar a inventario.";
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
                    KARDEX_ID = Guid.NewGuid().ToString("N")[..10], // ID de 10 chars
                    PRODUCTO_ID = d.PRODUCTO_ID,
                    FECHA = DateTime.Now,
                    TIPO_MOVIMIENTO = "EntradaCompra",              // tipifiquemos así
                    CANTIDAD = d.CANTIDAD_RECIBIDA!.Value,         // ya validado
                    COSTO_UNITARIO = d.PRECIO_COMPRA!.Value,       // ya validado
                    REFERENCIA = id,                                // COMPRA_ID para rastreo
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
                        COSTO_UNITARIO = 0,      // por política, NO lo actualizamos aquí
                        ESTADO = true,
                        USUARIO_CREACION = user
                    };
                    _db.INVENTARIO.Add(inv);
                }

                // Sumar lo recibido
                inv.STOCK_ACTUAL += d.CANTIDAD_RECIBIDA!.Value;
                // IMPORTANTE: NO tocar inv.COSTO_UNITARIO (tu política así lo define)
            }

            // 5.3) Marcar compra como cargada y cerrar
            compra.CARGADA_INVENTARIO = true;
            compra.FECHA_CARGA = DateTime.Now;
            compra.ESTADO_COMPRA_ID = "CER";
            compra.USUARIO_MODIFICACION = user;
            compra.FECHA_MODIFICACION = DateTime.Now;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["ok"] = "Inventario actualizado y compra cerrada.";
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
}
