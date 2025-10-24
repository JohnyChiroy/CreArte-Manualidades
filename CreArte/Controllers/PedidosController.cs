using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;            // /ModelsPartial/PedidoViewModels.cs
using CreArte.Services.Bitacora;        // /Services/Bitacora/BitacoraService.cs
using CreArte.Services.Pedidos;         // /Services/Pedidos/PedidoCalculadora.cs
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

    // Genera IDs tipo PE00000000
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
            FECHA_ENTREGA_PEDIDO = vm.FechaEntregaDeseada,  // DateOnly?
            OBSERVACIONES_PEDIDO = vm.Observaciones,
            USUARIO_CREACION = UserName(),
            FECHA_CREACION = DateTime.Now,
            ESTADO = true
        };

        // Mapear detalle (si CANTIDAD en entidad es INT → uso RoundToInt)
        pedido.DETALLE_PEDIDO = vm.Detalles.Select(d => new DETALLE_PEDIDO
        {
            PEDIDO_ID = pedido.PEDIDO_ID,
            PRODUCTO_ID = d.ProductoId!,
            CANTIDAD = RoundToInt(d.Cantidad),                 // evita ambigüedad
            PRECIO_PEDIDO = d.PrecioPedido,
            SUBTOTAL = Math.Round(d.Cantidad * d.PrecioPedido, 2),
            USUARIO_CREACION = UserName(),
            FECHA_CREACION = DateTime.Now,
            ESTADO = true
        }).ToList();

        // Recalcula totales + anticipo (25% si total >= 300)
        PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);

        _db.PEDIDO.Add(pedido);
        await _db.SaveChangesAsync(ct);

        await _bitacora.LogAsync("PEDIDO", "INSERT", UserName(), $"Creó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = $"Pedido {pedido.PEDIDO_ID} creado (Total Q{pedido.TOTAL_PEDIDO:N2}).";
        return RedirectToAction("Edit", new { id = pedido.PEDIDO_ID });
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

        var vm = new PedidoCreateEditVM
        {
            PedidoId = pedido.PEDIDO_ID,
            ClienteId = pedido.CLIENTE_ID,
            FechaEntregaDeseada = pedido.FECHA_ENTREGA_PEDIDO,  // DateOnly?
            Observaciones = pedido.OBSERVACIONES_PEDIDO,
            EstadoPedidoId = pedido.ESTADO_PEDIDO_ID,
            AnticipoEstado = pedido.ANTICIPO_ESTADO,
            ClientesCombo = await GetClientesAsync(),
            ItemsCombo = await GetItemsElaboracionAsync(),
            Detalles = pedido.DETALLE_PEDIDO.Select(d => new PedidoDetalleVM
            {
                ProductoId = d.PRODUCTO_ID,
                ProductoNombre = _db.PRODUCTO.Where(p => p.PRODUCTO_ID == d.PRODUCTO_ID)
                                             .Select(p => p.PRODUCTO_NOMBRE)
                                             .FirstOrDefault(),
                Cantidad = d.CANTIDAD,         // si la entidad es int, aquí sube a decimal sin problema
                PrecioPedido = d.PRECIO_PEDIDO ?? 0m
            }).ToList()
        };

        return View(vm);
    }

    // ======================================================
    // POST: /Pedidos/Edit/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, PedidoCreateEditVM vm, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);

        if (pedido == null) return NotFound();

        // Regla del negocio: no permitir edición si el anticipo está pagado
        if (string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "No se puede editar: el anticipo ya fue pagado.";
            return RedirectToAction("Edit", new { id });
        }

        // Cabecera
        pedido.CLIENTE_ID = vm.ClienteId!;
        pedido.FECHA_ENTREGA_PEDIDO = vm.FechaEntregaDeseada; // DateOnly?
        pedido.OBSERVACIONES_PEDIDO = vm.Observaciones;
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        // Detalle (sincronización simple: borrar + volver a insertar)
        _db.DETALLE_PEDIDO.RemoveRange(pedido.DETALLE_PEDIDO);
        pedido.DETALLE_PEDIDO.Clear();

        foreach (var d in vm.Detalles)
        {
            pedido.DETALLE_PEDIDO.Add(new DETALLE_PEDIDO
            {
                PEDIDO_ID = pedido.PEDIDO_ID,
                PRODUCTO_ID = d.ProductoId!,
                CANTIDAD = RoundToInt(d.Cantidad),
                PRECIO_PEDIDO = d.PrecioPedido,
                SUBTOTAL = Math.Round(d.Cantidad * d.PrecioPedido, 2),
                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            });
        }

        // Recalcula totales + anticipo
        PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Editó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = $"Pedido {pedido.PEDIDO_ID} actualizado (Total Q{pedido.TOTAL_PEDIDO:N2}).";
        return RedirectToAction("Edit", new { id });
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
        return RedirectToAction("Edit", new { id });
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
        return RedirectToAction("Edit", new { id });
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
            return RedirectToAction("Edit", new { id });
        }

        pedido.ANTICIPO_ESTADO = "PAGADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Marcó anticipo PAGADO en pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Anticipo marcado como PAGADO.";
        return RedirectToAction("Edit", new { id });
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
            return RedirectToAction("Edit", new { id });
        }

        pedido.ESTADO_PEDIDO_ID = "PROGRAMADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Programó producción del pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Producción programada.";
        return RedirectToAction("Edit", new { id });
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
        return RedirectToAction("Edit", new { id });
    }
}
