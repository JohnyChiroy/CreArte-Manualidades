using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;            
using CreArte.Services.Bitacora;        
using CreArte.Services.Pedidos;         
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class PedidosController : Controller
{
    private readonly CreArteDbContext _db;
    private readonly IBitacoraService _bitacora;

    public PedidosController(CreArteDbContext db, IBitacoraService bitacora)
    {
        _db = db;
        _bitacora = bitacora;
    }

    //  nombre de usuario para auditoría
    private string UserName() => User?.Identity?.Name ?? "sistema";

    //  redondeo explícito a INT para evitar ambigüedad de Math.Round
    private static int RoundToInt(decimal value)
        => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    // Genera IDs para PEDIDO - PE00000000
    private async Task<string> SiguientePedidoIdAsync()
    {
        const string prefijo = "PE";  
        const int ancho = 8;          

        // Trae los IDs que empiezan con el prefijo
        var ids = await _db.PEDIDO
            .Select(p => p.PEDIDO_ID)
            .Where(id => id.StartsWith(prefijo))
            .ToListAsync();

        int maxNum = 0;
        var rx = new Regex(@"^" + prefijo + @"(?<n>\d+)$");

        foreach (var id in ids)
        {
            var m = rx.Match(id ?? "");
            if (m.Success && int.TryParse(m.Groups["n"].Value, out int n))
                if (n > maxNum) maxNum = n;
        }

        var siguiente = maxNum + 1;
        return prefijo + siguiente.ToString(new string('0', ancho));
    }


    // --- Genera IDs para DETALLE_PEDIDO ---
    private static string NewDetallePedidoId()
    {
        // "N" = GUID sin guiones (32 chars).
        return Guid.NewGuid().ToString("N").Substring(0, 10);
    }

    // ------------------------------------------------------
    // Combo de clientes mostrando NOMBRE (PERSONA)

    private async Task<List<SelectListItem>> GetClientesAsync()
    {
        return await (from cli in _db.CLIENTE
                      join per in _db.PERSONA on cli.CLIENTE_ID equals per.PERSONA_ID
                      where cli.ESTADO == true                      // BIT → bool
                      orderby per.PERSONA_PRIMERAPELLIDO, per.PERSONA_PRIMERNOMBRE
                      select new SelectListItem
                      {
                          Value = cli.CLIENTE_ID,
                          Text = (
                              (per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                              (per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                              (per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                              (per.PERSONA_SEGUNDOAPELLIDO ?? "")
                          ).Trim()
                      }).ToListAsync();
    }

    // ------------------------------------------------------
    // Combo de ítems para elaboración (excluye PRODUCTO FINAL)
  
    private async Task<List<SelectListItem>> GetItemsElaboracionAsync()
    {
        // Busca el ID del tipo "PRODUCTO FINAL"
        var tipoProductoFinalId = await _db.TIPO_PRODUCTO
            .Where(t => t.TIPO_PRODUCTO_NOMBRE == "PRODUCTO FINAL")
            .Select(t => t.TIPO_PRODUCTO_ID)
            .FirstOrDefaultAsync();

        return await _db.PRODUCTO
            .Where(p => p.ESTADO == true && p.TIPO_PRODUCTO_ID != tipoProductoFinalId)
            .OrderBy(p => p.PRODUCTO_NOMBRE)
            .Select(p => new SelectListItem { Value = p.PRODUCTO_ID, Text = p.PRODUCTO_NOMBRE })
            .ToListAsync();
    }

    // ======================================================
    // GET: /Pedidos
    // LISTADO con filtros, orden y paginación
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PedidoIndexVM vm)
    {
        var q = _db.PEDIDO.AsNoTracking().AsQueryable();

        // -------- Filtros --------
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            string s = vm.Search.Trim();
            q = q.Where(p =>
                p.PEDIDO_ID.Contains(s) ||
                p.CLIENTE_ID.Contains(s) ||
                (p.OBSERVACIONES_PEDIDO ?? "").Contains(s) ||
                _db.PERSONA.Any(per =>
                    per.PERSONA_ID == p.CLIENTE_ID &&
                    ((per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                     (per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                     (per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                     (per.PERSONA_SEGUNDOAPELLIDO ?? "")).Contains(s)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(vm.Estado))
            q = q.Where(p => p.ESTADO_PEDIDO_ID == vm.Estado);

        if (!string.IsNullOrWhiteSpace(vm.Cliente))
        {
            string c = vm.Cliente.Trim();
            q = q.Where(p => _db.PERSONA.Any(per =>
                per.PERSONA_ID == p.CLIENTE_ID &&
                ((per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                 (per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                 (per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                 (per.PERSONA_SEGUNDOAPELLIDO ?? "")).Contains(c)
            ));
        }

        if (vm.RequiereAnticipo.HasValue)
            q = q.Where(p => p.REQUIERE_ANTICIPO == vm.RequiereAnticipo.Value);

        if (!string.IsNullOrWhiteSpace(vm.AnticipoEstado))
            q = q.Where(p => (p.ANTICIPO_ESTADO ?? "") == vm.AnticipoEstado);

        if (vm.Desde.HasValue)
            q = q.Where(p => p.FECHA_PEDIDO >= vm.Desde.Value);

        if (vm.Hasta.HasValue)
        {
            var hasta = vm.Hasta.Value.Date.AddDays(1).AddTicks(-1);
            q = q.Where(p => p.FECHA_PEDIDO <= hasta);
        }

        if (vm.TotalMin.HasValue) q = q.Where(p => p.TOTAL_PEDIDO >= vm.TotalMin.Value);
        if (vm.TotalMax.HasValue) q = q.Where(p => p.TOTAL_PEDIDO <= vm.TotalMax.Value);

        // -------- Orden --------
        bool asc = string.Equals(vm.Dir, "asc", StringComparison.OrdinalIgnoreCase);
        switch ((vm.Sort ?? "id").ToLower())
        {
            case "fecha":
                q = asc ? q.OrderBy(p => p.FECHA_PEDIDO) : q.OrderByDescending(p => p.FECHA_PEDIDO);
                break;

            case "cliente":
                // Orden alfabético por primer apellido del cliente (PERSONA)
                q = asc
                    ? q.OrderBy(p => _db.PERSONA
                          .Where(per => per.PERSONA_ID == p.CLIENTE_ID)
                          .Select(per => per.PERSONA_PRIMERAPELLIDO)
                          .FirstOrDefault())
                       .ThenBy(p => p.PEDIDO_ID)
                    : q.OrderByDescending(p => _db.PERSONA
                          .Where(per => per.PERSONA_ID == p.CLIENTE_ID)
                          .Select(per => per.PERSONA_PRIMERAPELLIDO)
                          .FirstOrDefault())
                       .ThenByDescending(p => p.PEDIDO_ID);
                break;

            case "estado":
                q = asc ? q.OrderBy(p => p.ESTADO_PEDIDO_ID).ThenBy(p => p.PEDIDO_ID)
                        : q.OrderByDescending(p => p.ESTADO_PEDIDO_ID).ThenByDescending(p => p.PEDIDO_ID);
                break;

            case "total":
                q = asc ? q.OrderBy(p => p.TOTAL_PEDIDO).ThenBy(p => p.PEDIDO_ID)
                        : q.OrderByDescending(p => p.TOTAL_PEDIDO).ThenByDescending(p => p.PEDIDO_ID);
                break;

            case "anticipo":
                q = asc ? q.OrderBy(p => p.REQUIERE_ANTICIPO).ThenBy(p => p.ANTICIPO_ESTADO).ThenBy(p => p.PEDIDO_ID)
                        : q.OrderByDescending(p => p.REQUIERE_ANTICIPO).ThenByDescending(p => p.ANTICIPO_ESTADO).ThenByDescending(p => p.PEDIDO_ID);
                break;

            default: // id
                q = asc ? q.OrderBy(p => p.PEDIDO_ID) : q.OrderByDescending(p => p.PEDIDO_ID);
                break;
        }

        // -------- Paginación --------
        vm.TotalItems = await q.CountAsync();
        if (vm.PageSize <= 0) vm.PageSize = 10;
        vm.TotalPages = (int)Math.Ceiling((double)vm.TotalItems / vm.PageSize);
        if (vm.Page <= 0) vm.Page = 1;
        if (vm.TotalPages > 0 && vm.Page > vm.TotalPages) vm.Page = vm.TotalPages;

        // -------- Proyección a VM --------
        var data = await q
            .Skip((vm.Page - 1) * vm.PageSize)
            .Take(vm.PageSize)
            .Select(p => new PedidoIndexItemVM
            {
                PedidoId = p.PEDIDO_ID,
                FechaPedido = p.FECHA_PEDIDO,
                EstadoPedidoId = p.ESTADO_PEDIDO_ID,
                FechaEntrega = p.FECHA_ENTREGA_PEDIDO,   // DateOnly?
                ClienteId = p.CLIENTE_ID,
                // Nombre de cliente desde PERSONA; fallback a CLIENTE_ID si no encuentra
                ClienteNombre = _db.PERSONA
                    .Where(per => per.PERSONA_ID == p.CLIENTE_ID)
                    .Select(per => (
                        (per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (per.PERSONA_SEGUNDOAPELLIDO ?? "")
                    ).Trim())
                    .FirstOrDefault() ?? p.CLIENTE_ID,
                TotalPedido = p.TOTAL_PEDIDO,
                AnticipoEstado = p.ANTICIPO_ESTADO,
                RequiereAnticipo = p.REQUIERE_ANTICIPO
            })
            .ToListAsync();

        vm.Items = data;

        // -------- Combos para filtros --------
        vm.Estados = await _db.ESTADO_PEDIDO
            .OrderBy(e => e.ESTADO_PEDIDO_ID)
            .Select(e => new SelectListItem { Value = e.ESTADO_PEDIDO_ID, Text = e.ESTADO_PEDIDO_NOMBRE ?? e.ESTADO_PEDIDO_ID })
            .ToListAsync();

        vm.Clientes = await (from cli in _db.CLIENTE
                             join per in _db.PERSONA on cli.CLIENTE_ID equals per.PERSONA_ID
                             where cli.ESTADO == true
                             orderby per.PERSONA_PRIMERAPELLIDO, per.PERSONA_PRIMERNOMBRE
                             select new SelectListItem
                             {
                                 Value = cli.CLIENTE_ID,
                                 Text = (
                                     (per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                                     (per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                                     (per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                                     (per.PERSONA_SEGUNDOAPELLIDO ?? "")
                                 ).Trim()
                             }).ToListAsync();

        return View(vm);
    }

    // ======================================================
    // GET: /Pedidos/Create
    // - Catálogo desde PRODUCTO (≠ PRODUCTO FINAL)
    // - Adjunta stock y costo desde INVENTARIO
    // - Serializa a ViewBag.ProductosJson para la vista
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // 1) Combos base
        var vm = new PedidoCreateEditVM
        {
            ClientesCombo = await GetClientesAsync(),
            ItemsCombo = await GetItemsElaboracionAsync(),   // Sigue filtrando ≠ PRODUCTO FINAL
            EstadoPedidoId = "BORRADOR"
        };

        // 2) Traer ID del tipo "PRODUCTO FINAL" para excluirlo del catálogo JSON
        var tipoProductoFinalId = await _db.TIPO_PRODUCTO
            .Where(t => t.TIPO_PRODUCTO_NOMBRE == "PRODUCTO FINAL")
            .Select(t => t.TIPO_PRODUCTO_ID)
            .FirstOrDefaultAsync();

        // 3) Catálogo: PRODUCTO activo ≠ FINAL + join (o subquery) con INVENTARIO
        //    >>> AJUSTA nombres de columnas de INVENTARIO según tu esquema:
        //    - Stock actual:  INVENTARIO.STOCK_ACTUAL        (o EXISTENCIA_ACTUAL)
        //    - Costo unitario:INVENTARIO.COSTO_UNITARIO      (o COSTO_PROMEDIO)
        var prods = await (
            from p in _db.PRODUCTO
            where p.ESTADO == true && p.TIPO_PRODUCTO_ID != tipoProductoFinalId
            select new
            {
                id = p.PRODUCTO_ID,
                nombre = p.PRODUCTO_NOMBRE,
                imagen = p.IMAGEN_PRODUCTO,          // TODO: cambia al campo real de imagen si aplica
                                                         // Stock y costo por subquery; si no hay fila de inventario, 0 y 0
                stock = (decimal?)_db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .Select(i => i.STOCK_ACTUAL)              // TODO: EXISTENCIA_ACTUAL si ese es tu campo
                            .FirstOrDefault() ?? 0m,
                costo = (decimal?)_db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .Select(i => i.COSTO_UNITARIO)            // TODO: COSTO_PROMEDIO si ese es tu campo
                            .FirstOrDefault() ?? 0m
            }
        )
        .OrderBy(x => x.nombre)
        .ToListAsync();

        // 4) Serializar para la vista (Create.cshtml)
        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(prods);

        return View(vm);
    }


    // ======================================================
    // POST: /Pedidos/Create
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PedidoCreateEditVM vm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vm.ClienteId))
            ModelState.AddModelError(nameof(vm.ClienteId), "Seleccione un cliente.");

        if (vm.Detalles == null || vm.Detalles.Count == 0)
            ModelState.AddModelError("", "Debe agregar al menos un ítem al pedido.");

        if (!ModelState.IsValid)
        {
            vm.ClientesCombo = await GetClientesAsync();
            vm.ItemsCombo = await GetItemsElaboracionAsync();
            return View(vm);
        }

        var pedido = new PEDIDO
        {
            PEDIDO_ID = await SiguientePedidoIdAsync(),
            FECHA_PEDIDO = DateTime.Now,
            CLIENTE_ID = vm.ClienteId!,
            ESTADO_PEDIDO_ID = "BORRADOR",
            FECHA_ENTREGA_PEDIDO = vm.FechaEntregaDeseada,
            OBSERVACIONES_PEDIDO = vm.Observaciones,
            USUARIO_CREACION = UserName(),
            FECHA_CREACION = DateTime.Now,
            ESTADO = true
        };

        pedido.DETALLE_PEDIDO = vm.Detalles.Select(d => new DETALLE_PEDIDO
        {
            DETALLE_PEDIDO_ID = NewDetallePedidoId(),
            PEDIDO_ID = pedido.PEDIDO_ID,
            PRODUCTO_ID = d.ProductoId!,
            CANTIDAD = RoundToInt(d.Cantidad),
            PRECIO_PEDIDO = d.PrecioPedido,
            SUBTOTAL = Math.Round(d.Cantidad * d.PrecioPedido, 2),
            USUARIO_CREACION = UserName(),
            FECHA_CREACION = DateTime.Now,
            ESTADO = true
        }).ToList();

        // Recalcula totales + anticipo (25% si total >= 300)
        PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);

        // ====== NUEVO: fijar ANTICIPO_ESTADO según REQUIERE_ANTICIPO ======
        if (!pedido.REQUIERE_ANTICIPO)
        {
            pedido.ANTICIPO_MINIMO = 0m;
            pedido.ANTICIPO_ESTADO = "NO APLICA"; // evita nulos
        }
        else
        {
            // Si requiere anticipo y no hay estado aún, arranca en PENDIENTE
            if (string.IsNullOrWhiteSpace(pedido.ANTICIPO_ESTADO))
                pedido.ANTICIPO_ESTADO = "PENDIENTE";
        }

        _db.PEDIDO.Add(pedido);
        await _db.SaveChangesAsync(ct);

        await _bitacora.LogAsync("PEDIDO", "INSERT", UserName(), $"Creó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = $"Pedido {pedido.PEDIDO_ID} creado (Total Q{pedido.TOTAL_PEDIDO:N2}).";
        return RedirectToAction("Index");
    }

    // ======================================================
    // GET: Details
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);

        if (pedido == null) return NotFound();

        // ========= Nombre del cliente (CLIENTE_ID = PERSONA_ID) =========
        var persona = await _db.PERSONA
            .Where(x => x.PERSONA_ID == pedido.CLIENTE_ID)
            .Select(x => new
            {
                x.PERSONA_PRIMERNOMBRE,
                x.PERSONA_SEGUNDONOMBRE,
                x.PERSONA_TERCERNOMBRE,
                x.PERSONA_PRIMERAPELLIDO,
                x.PERSONA_SEGUNDOAPELLIDO,
                x.PERSONA_APELLIDOCASADA
            })
            .FirstOrDefaultAsync(ct);

        string clienteNombre = "";
        if (persona != null)
        {
            // Une solo partes no vacías
            var parts = new[]
            {
            persona.PERSONA_PRIMERNOMBRE,
            persona.PERSONA_SEGUNDONOMBRE,
            persona.PERSONA_TERCERNOMBRE,
            persona.PERSONA_PRIMERAPELLIDO,
            persona.PERSONA_SEGUNDOAPELLIDO,
            persona.PERSONA_APELLIDOCASADA
        }.Where(s => !string.IsNullOrWhiteSpace(s));
            clienteNombre = string.Join(" ", parts);
        }
        if (string.IsNullOrWhiteSpace(clienteNombre))
            clienteNombre = pedido.CLIENTE_ID; // fallback

        // ========= Líneas con nombre/imagen del producto =========
        var lineas = await (
            from d in _db.DETALLE_PEDIDO
            join p in _db.PRODUCTO on d.PRODUCTO_ID equals p.PRODUCTO_ID
            where d.PEDIDO_ID == id
            select new PedidoLineaVM
            {
                ProductoId = p.PRODUCTO_ID,
                ProductoNombre = p.PRODUCTO_NOMBRE,
                ImagenProducto = p.IMAGEN_PRODUCTO,
                Cantidad = d.CANTIDAD,
                PrecioPedido = d.PRECIO_PEDIDO ?? 0m,
                Subtotal = d.SUBTOTAL
            })
            .ToListAsync(ct);

        var total = lineas.Sum(l => l.Subtotal);

        // ========= VM =========
        var vm = new PedidoDetailsVM
        {
            PedidoId = pedido.PEDIDO_ID,
            EstadoId = pedido.ESTADO_PEDIDO_ID,
            EstadoNombre = pedido.ESTADO_PEDIDO_ID, // si tienes catálogo, reemplaza por el nombre
            ClienteId = pedido.CLIENTE_ID,
            ClienteNombre = clienteNombre,
            FechaPedido = pedido.FECHA_PEDIDO,
            FechaEntrega = pedido.FECHA_ENTREGA_PEDIDO.HasValue
                ? pedido.FECHA_ENTREGA_PEDIDO.Value.ToDateTime(TimeOnly.MinValue)
                : (DateTime?)null,
            Observaciones = pedido.OBSERVACIONES_PEDIDO,

            RequiereAnticipo = pedido.REQUIERE_ANTICIPO,
            AnticipoEstado = pedido.ANTICIPO_ESTADO ?? "NO APLICA",
            AnticipoMinimo = pedido.ANTICIPO_MINIMO,

            TotalPedido = total,
            Lineas = lineas
        };

        // ========= Permisos según estado =========
        var st = pedido.ESTADO_PEDIDO_ID?.ToUpperInvariant() ?? "";

        vm.PuedeCotizar = st == "BORRADOR";
        vm.PuedeAprobar = st == "COTIZADO";
        vm.PuedeProgramar = st == "APROBADO";
        vm.PuedeFinalizar = st == "PROGRAMADO" || st == "EN_PRODU";
        vm.PuedeEntregar = st == "TERMINADO";
        vm.PuedeCerrar = st == "ENTREGADO";
        vm.PuedePagarAnticipo = pedido.REQUIERE_ANTICIPO && string.Equals(pedido.ANTICIPO_ESTADO, "PENDIENTE", StringComparison.OrdinalIgnoreCase);

        // Cancelar en estados tempranos (ajústalo a tu flujo)
        vm.PuedeCancelar = st is "BORRADOR" or "COTIZADO" or "APROBADO" or "PROGRAMADO" or "EN_PRODU";

        // Rechazar en cotizado o aprobado (si aplica a tu negocio)
        vm.PuedeRechazar = st is "COTIZADO" or "APROBADO";

        return View(vm);
    }


    // ======================================================
    // GET: /Pedidos/Edit/{id}
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id);

        if (pedido == null) return NotFound();

        // ===== Catálogo de productos para la vista (≠ PRODUCTO FINAL) =====
        // Ajusta el filtro según tu modelo. Si tienes TIPO_PRODUCTO_NOMBRE, úsalo.
        var productos = await _db.PRODUCTO
            .Where(p => p.ESTADO == true && p.TIPO_PRODUCTO.TIPO_PRODUCTO_NOMBRE != "PRODUCTO FINAL") 
            .Select(p => new
            {
                id = p.PRODUCTO_ID,
                nombre = p.PRODUCTO_NOMBRE,
                imagen = p.IMAGEN_PRODUCTO, 
                                            // STOCK actual
                stock = (decimal?)_db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .Select(i => i.STOCK_ACTUAL)        
                            .FirstOrDefault() ?? 0m,
                // COSTO sugerido desde inventario (último costo)
                costo = (decimal?)_db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .OrderByDescending(i => i.FECHA_CREACION)    
                            .Select(i => i.COSTO_UNITARIO)      
                            .FirstOrDefault() ?? 0m
            })
            .OrderBy(p => p.nombre)
            .ToListAsync();

        // JSON para el JS (imagen/stock/costo)
        var json = System.Text.Json.JsonSerializer.Serialize(
            productos,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }
        );
        ViewBag.ProductosJson = json;

        // Combo del select (texto = nombre; value = id)
        var itemsCombo = productos
            .Select(p => new SelectListItem { Value = p.id, Text = p.nombre })
            .ToList();

        var vm = new PedidoCreateEditVM
        {
            PedidoId = pedido.PEDIDO_ID,
            ClienteId = pedido.CLIENTE_ID,
            FechaEntregaDeseada = pedido.FECHA_ENTREGA_PEDIDO,
            Observaciones = pedido.OBSERVACIONES_PEDIDO,
            EstadoPedidoId = pedido.ESTADO_PEDIDO_ID,
            AnticipoEstado = pedido.ANTICIPO_ESTADO,
            ClientesCombo = await GetClientesAsync(),
            ItemsCombo = itemsCombo,
            Detalles = pedido.DETALLE_PEDIDO.Select(d => new PedidoDetalleVM
            {
                ProductoId = d.PRODUCTO_ID,
                
                ProductoNombre = _db.PRODUCTO.Where(p => p.PRODUCTO_ID == d.PRODUCTO_ID)
                                             .Select(p => p.PRODUCTO_NOMBRE)
                                             .FirstOrDefault(),
                Cantidad = d.CANTIDAD,
                PrecioPedido = d.PRECIO_PEDIDO ?? 0m
            }).ToList()
        };

        return View(vm);
    }

    // ==============================================
    // POST: /Pedidos/Edit/{id}
    // Sincroniza cabecera y detalle SIN borrar todo
    // ==============================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, PedidoCreateEditVM vm, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);

        if (pedido == null) return NotFound();

        if (string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "No se puede editar: el anticipo ya fue pagado.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var lineasValidas = (vm.Detalles ?? new List<PedidoDetalleVM>())
            .Where(d => !string.IsNullOrWhiteSpace(d.ProductoId) && d.Cantidad > 0)
            .ToList();

        if (lineasValidas.Count == 0)
        {
            TempData["err"] = "Debe agregar al menos una línea válida (producto y cantidad).";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var setProd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in lineasValidas)
        {
            if (!setProd.Add(l.ProductoId!))
            {
                TempData["err"] = $"El producto {l.ProductoId} está repetido en el detalle.";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        // ===== SNAPSHOT de campos supervisados (para auditar cambios) =====
        var snap = new
        {
            pedido.TOTAL_PEDIDO,
            pedido.FECHA_PEDIDO,
            pedido.ESTADO_PEDIDO_ID,
            OBS = pedido.OBSERVACIONES_PEDIDO ?? "",
            pedido.CLIENTE_ID,
            pedido.REQUIERE_ANTICIPO,
            pedido.ANTICIPO_MINIMO,
            ANT_EST = pedido.ANTICIPO_ESTADO ?? ""
        };

        // ===== CABECERA =====
        bool hayCambiosCab = false;

        if (!string.Equals(pedido.CLIENTE_ID, vm.ClienteId, StringComparison.Ordinal))
        {
            pedido.CLIENTE_ID = vm.ClienteId!;
            hayCambiosCab = true;
        }

        if (pedido.FECHA_ENTREGA_PEDIDO != vm.FechaEntregaDeseada)
        {
            pedido.FECHA_ENTREGA_PEDIDO = vm.FechaEntregaDeseada;
            hayCambiosCab = true;
        }

        if (!string.Equals(pedido.OBSERVACIONES_PEDIDO ?? "", vm.Observaciones ?? "", StringComparison.Ordinal))
        {
            pedido.OBSERVACIONES_PEDIDO = vm.Observaciones;
            hayCambiosCab = true;
        }

        if (hayCambiosCab)
        {
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;
        }

        // ===== DETALLE (UPSERT por PRODUCTO_ID) =====
        bool hayCambiosDet = false;

        var existentesPorProd = pedido.DETALLE_PEDIDO
            .ToDictionary(d => d.PRODUCTO_ID, d => d, StringComparer.OrdinalIgnoreCase);

        var productosVM = new HashSet<string>(lineasValidas.Select(x => x.ProductoId!), StringComparer.OrdinalIgnoreCase);

        foreach (var l in lineasValidas)
        {
            var cantInt = RoundToInt(l.Cantidad);
            var nuevoSub = Math.Round(l.Cantidad * l.PrecioPedido, 2);

            if (existentesPorProd.TryGetValue(l.ProductoId!, out var det))
            {
                bool cambia = false;

                if (det.CANTIDAD != cantInt) { det.CANTIDAD = cantInt; cambia = true; }
                if ((det.PRECIO_PEDIDO ?? 0m) != l.PrecioPedido) { det.PRECIO_PEDIDO = l.PrecioPedido; cambia = true; }
                if (det.SUBTOTAL != nuevoSub) { det.SUBTOTAL = nuevoSub; cambia = true; }

                if (cambia)
                {
                    det.USUARIO_MODIFICACION = UserName();
                    det.FECHA_MODIFICACION = DateTime.Now;
                    hayCambiosDet = true;
                }
            }
            else
            {
                var nuevo = new DETALLE_PEDIDO
                {
                    DETALLE_PEDIDO_ID = NewDetallePedidoId(),
                    PEDIDO_ID = pedido.PEDIDO_ID,
                    PRODUCTO_ID = l.ProductoId!,
                    CANTIDAD = cantInt,
                    PRECIO_PEDIDO = l.PrecioPedido,
                    SUBTOTAL = nuevoSub,
                    USUARIO_CREACION = UserName(),
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                };
                _db.DETALLE_PEDIDO.Add(nuevo);
                hayCambiosDet = true;
            }
        }

        foreach (var det in pedido.DETALLE_PEDIDO.ToList())
        {
            if (!productosVM.Contains(det.PRODUCTO_ID))
            {
                _db.DETALLE_PEDIDO.Remove(det);
                hayCambiosDet = true;
            }
        }

        // ===== Recalcular totales/anticipo =====
        PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);

        // ===== AJUSTE ANTICIPO (mismo criterio que en Create) =====
        if (!pedido.REQUIERE_ANTICIPO)
        {
            // No requiere anticipo → normaliza campos
            var prevMin = pedido.ANTICIPO_MINIMO;
            var prevEst = pedido.ANTICIPO_ESTADO;

            pedido.ANTICIPO_MINIMO = 0m;
            pedido.ANTICIPO_ESTADO = "NO APLICA";

            // Si cambió algo, marca modificación de cabecera
            if (prevMin != pedido.ANTICIPO_MINIMO || !string.Equals(prevEst ?? "", pedido.ANTICIPO_ESTADO, StringComparison.Ordinal))
            {
                pedido.USUARIO_MODIFICACION = UserName();
                pedido.FECHA_MODIFICACION = DateTime.Now;
                hayCambiosCab = true;
            }
        }
        else
        {
            // Requiere anticipo → si no hay estado, arranca en PENDIENTE
            if (string.IsNullOrWhiteSpace(pedido.ANTICIPO_ESTADO))
            {
                pedido.ANTICIPO_ESTADO = "PENDIENTE";
                pedido.USUARIO_MODIFICACION = UserName();
                pedido.FECHA_MODIFICACION = DateTime.Now;
                hayCambiosCab = true;
            }
        }

        // ===== Detectar cambios en campos supervisados =====
        bool cambioSupervisado =
            snap.TOTAL_PEDIDO != pedido.TOTAL_PEDIDO ||
            snap.FECHA_PEDIDO != pedido.FECHA_PEDIDO ||
            snap.ESTADO_PEDIDO_ID != pedido.ESTADO_PEDIDO_ID ||
            snap.OBS != (pedido.OBSERVACIONES_PEDIDO ?? "") ||
            snap.CLIENTE_ID != pedido.CLIENTE_ID ||
            snap.REQUIERE_ANTICIPO != pedido.REQUIERE_ANTICIPO ||
            snap.ANTICIPO_MINIMO != pedido.ANTICIPO_MINIMO ||
            snap.ANT_EST != (pedido.ANTICIPO_ESTADO ?? "");

        if (cambioSupervisado)
        {
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;
            hayCambiosCab = true;
        }

        // ===== ¿Hubo algo que guardar? =====
        bool hayCambios = hayCambiosCab || hayCambiosDet;

        if (!hayCambios)
        {
            TempData["SwalOneBtnFlag"] = "nochange";
            TempData["SwalTitle"] = "Sin cambios";
            TempData["SwalText"] = "No se modificó ningún dato.";
            return RedirectToAction(nameof(Edit), new { id = pedido.PEDIDO_ID });
        }

        await _db.SaveChangesAsync(ct);

        await _bitacora.LogAsync(
            "PEDIDO", "UPDATE", UserName(),
            $"Editó pedido {pedido.PEDIDO_ID} (cabecera: {(hayCambiosCab ? "sí" : "no")}, detalle: {(hayCambiosDet ? "sí" : "no")}).",
            ct);

        TempData["SwalOneBtnFlag"] = "updated";
        TempData["SwalTitle"] = "¡Pedido actualizado!";
        TempData["SwalText"] = $"El pedido \"{pedido.PEDIDO_ID}\" se actualizó correctamente.";

        return RedirectToAction(nameof(Edit), new { id = pedido.PEDIDO_ID });
    }


    // ======================================================
    // GET/POST: /Pedidos/Cotizar/{id}
    //   *GET muestra formulario (si agregas MO/Margen)
    //   *POST marca como COTIZADO y recalcula totales
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Cotizar(string id)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id);
        if (pedido == null) return NotFound();

        var vm = new PedidoCotizarVM { PedidoId = id };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cotizar(string id, PedidoCotizarVM vm, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);
        pedido.ESTADO_PEDIDO_ID = "COTIZADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Cotizó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = $"Cotización lista. Total Q{pedido.TOTAL_PEDIDO:N2}" +
                         (pedido.REQUIERE_ANTICIPO ? $" | Anticipo 25%: Q{pedido.ANTICIPO_MINIMO:N2}" : "");
        return RedirectToAction("Details", new { id });
    }

    // ======================================================
    // POST: /Pedidos/Aprobar/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        pedido.ESTADO_PEDIDO_ID = "APROBADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Aprobó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = pedido.REQUIERE_ANTICIPO
            ? $"Pedido aprobado. Requiere anticipo de Q{pedido.ANTICIPO_MINIMO:N2}."
            : "Pedido aprobado.";
        return RedirectToAction("Details", new { id });
    }

    // ======================================================
    // POST: /Pedidos/PagarAnticipo/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PagarAnticipo(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        if (!pedido.REQUIERE_ANTICIPO)
        {
            TempData["err"] = "Este pedido no requiere anticipo.";
            return RedirectToAction("Details", new { id });
        }

        pedido.ANTICIPO_ESTADO = "PAGADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Marcó anticipo PAGADO en pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Anticipo marcado como PAGADO.";
        return RedirectToAction("Details", new { id });
    }

    // ======================================================
    // POST: /Pedidos/Programar/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Programar(string id, ProduccionProgramarVM vm, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        if (pedido.REQUIERE_ANTICIPO && !string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "No se puede programar: anticipo requerido no pagado.";
            return RedirectToAction("Details", new { id });
        }

        pedido.ESTADO_PEDIDO_ID = "PROGRAMADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Programó producción del pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Producción programada.";
        return RedirectToAction("Details", new { id });
    }

    // ======================================================
    // POST: /Pedidos/Finalizar/{id}
    //   - Descarga insumos (SALIDA PEDIDO)
    //   - Carga producto final (ENTRADA PEDIDO)
    //   - Cambia a TERMINADO
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        // Marcamos estado intermedio (opcional)
        pedido.ESTADO_PEDIDO_ID = "EN_PRODU";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Inició producción de {pedido.PEDIDO_ID}", ct);

        // -------- SALIDA PEDIDO: descarga insumos --------
        foreach (var d in pedido.DETALLE_PEDIDO)
        {
            var cant = RoundToInt(Convert.ToDecimal(d.CANTIDAD)); // cast seguro

            _db.KARDEX.Add(new KARDEX
            {
                PRODUCTO_ID = d.PRODUCTO_ID,
                FECHA = DateTime.Now,
                TIPO_MOVIMIENTO = "SALIDA PEDIDO",
                CANTIDAD = cant,
                COSTO_UNITARIO = 0m, // TODO: si manejas costo promedio, calcula aquí
                REFERENCIA = pedido.PEDIDO_ID,
                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            });

            var inv = await _db.INVENTARIO.FirstOrDefaultAsync(i => i.PRODUCTO_ID == d.PRODUCTO_ID, ct);
            if (inv != null) inv.STOCK_ACTUAL -= cant;
        }
        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("KARDEX", "INSERT", UserName(), $"Descargó insumos por pedido {pedido.PEDIDO_ID}", ct);

        // -------- ENTRADA PEDIDO: carga producto final --------
        foreach (var d in pedido.DETALLE_PEDIDO)
        {
            var cant = RoundToInt(Convert.ToDecimal(d.CANTIDAD));

            _db.KARDEX.Add(new KARDEX
            {
                PRODUCTO_ID = d.PRODUCTO_ID, // TODO: si el producto final es otro ID, cámbialo aquí
                FECHA = DateTime.Now,
                TIPO_MOVIMIENTO = "ENTRADA PEDIDO",
                CANTIDAD = cant,
                COSTO_UNITARIO = 0m,
                REFERENCIA = pedido.PEDIDO_ID,
                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            });

            var inv = await _db.INVENTARIO.FirstOrDefaultAsync(i => i.PRODUCTO_ID == d.PRODUCTO_ID, ct);
            if (inv != null) inv.STOCK_ACTUAL += cant;
        }

        // Estado final
        pedido.ESTADO_PEDIDO_ID = "TERMINADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("KARDEX", "INSERT", UserName(), $"Cargó producto final por pedido {pedido.PEDIDO_ID}", ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Finalizó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Pedido finalizado. Movimientos de inventario registrados.";
        return RedirectToAction("Details", new { id });
    }

    // ======================================================
    // POST: /Pedidos/Entregar/{id}
    //   - Reglas sugeridas:
    //     * Debe estar TERMINADO (o PROGRAMADO/EN_PRODU si quieres permitir antes)
    //     * Si requiere anticipo, debe estar PAGADO
    //     * Marca estado = ENTREGADO y fecha de modificación
    //     * Si no tiene fecha de entrega, la fija a hoy
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entregar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        // Si requiere anticipo, debe estar pagado
        if (pedido.REQUIERE_ANTICIPO && !string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "No se puede entregar: anticipo requerido no pagado.";
            return RedirectToAction("Details", new { id });
        }

        // Regla de estado previo: normalmente sólo si está TERMINADO
        if (!string.Equals(pedido.ESTADO_PEDIDO_ID, "TERMINADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "El pedido debe estar TERMINADO para poder entregarlo.";
            return RedirectToAction("Details", new { id });
        }

        // Actualización
        pedido.ESTADO_PEDIDO_ID = "ENTREGADO";
        if (!pedido.FECHA_ENTREGA_PEDIDO.HasValue)
            pedido.FECHA_ENTREGA_PEDIDO = DateOnly.FromDateTime(DateTime.Now);

        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Marcó ENTREGADO el pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Pedido entregado correctamente.";
        return RedirectToAction("Details", new { id });
    }

    // ======================================================
    // POST: /Pedidos/Cerrar/{id}
    //   - Reglas sugeridas:
    //     * Sólo si está ENTREGADO
    //     * Cambia estado a CERRADO y audita modificación
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        if (!string.Equals(pedido.ESTADO_PEDIDO_ID, "ENTREGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "Para cerrar el pedido primero debe estar ENTREGADO.";
            return RedirectToAction("Details", new { id });
        }

        pedido.ESTADO_PEDIDO_ID = "CERRADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Cerró el pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Pedido cerrado correctamente.";
        return RedirectToAction("Details", new { id });
    }

}
