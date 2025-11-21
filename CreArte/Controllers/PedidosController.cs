using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Bitacora;
using CreArte.Services.Pedidos;
using DocumentFormat.OpenXml.Vml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
        => Guid.NewGuid().ToString("N").Substring(0, 10);

    // IDs de 10 caracteres (coherentes con varchar(10))
    private static string NewId10() => Guid.NewGuid().ToString("N")[..10];

    // Muestra la causa real del error (inner/base exception)
    private static string RootError(Exception ex)
        => (ex.GetBaseException()?.Message ?? ex.Message);

    // === Regla de negocio: duplicar total por costo de elaboración y recalcular anticipo ===

    private static void DoblarTotalYRecalcularAnticipo(PEDIDO pedido)
    {
        // Base = total calculado por la calculadora (suma de subtotales de líneas)
        var baseTotal = pedido.TOTAL_PEDIDO;
        var elaboracion = baseTotal;          // costo de elaboración = base
        pedido.TOTAL_PEDIDO = baseTotal + elaboracion;

        // Anticipo sobre total final
        if (pedido.TOTAL_PEDIDO >= 300m)
        {
            pedido.REQUIERE_ANTICIPO = true;
            pedido.ANTICIPO_MINIMO = Math.Round(pedido.TOTAL_PEDIDO * 0.25m, 2);
            if (string.IsNullOrWhiteSpace(pedido.ANTICIPO_ESTADO))
                pedido.ANTICIPO_ESTADO = "PENDIENTE";
        }
        else
        {
            pedido.REQUIERE_ANTICIPO = false;
            pedido.ANTICIPO_MINIMO = 0m;
            pedido.ANTICIPO_ESTADO = "NO APLICA";
        }
    }

    // ------------------------------------------------------
    // Combo de clientes mostrando NOMBRE (PERSONA)
    private async Task<List<SelectListItem>> GetClientesAsync()
    {
        return await (from cli in _db.CLIENTE
                      join per in _db.PERSONA on cli.CLIENTE_ID equals per.PERSONA_ID
                      where cli.ESTADO == true
                      orderby per.PERSONA_PRIMERNOMBRE, per.PERSONA_PRIMERAPELLIDO
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

    // ===== Helpers de pago =====
    private async Task<List<SelectListItem>> GetMetodosPagoComboAsync(CancellationToken ct)
    {
        return await _db.METODO_PAGO
            .Where(m => m.ESTADO == true && m.ELIMINADO == false)
            .OrderBy(m => m.METODO_PAGO_NOMBRE)
            .Select(m => new SelectListItem
            {
                Value = m.METODO_PAGO_ID,          
                Text = m.METODO_PAGO_NOMBRE  
            })
            .ToListAsync(ct);
    }

    private async Task<decimal> TotalPagadoPedidoAsync(string pedidoId, CancellationToken ct = default)
    {
        // Asumimos que tu tabla RECIBO tiene columna PEDIDO_ID (la usaste para anticipo)
        return await _db.RECIBO
            .Where(r => r.ESTADO == true && r.VENTA_ID == pedidoId)
            .Select(r => (decimal?)r.MONTO)
            .SumAsync(ct) ?? 0m;
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

    // Sesión de caja activa 
    private async Task<string?> GetSesionCajaActivaAsync(string usuarioId)
    {
        return await _db.CAJA_SESION
            .Where(c => c.ESTADO == true)
            .OrderByDescending(c => c.FECHA_APERTURA)
            .Select(c => c.SESION_ID)
            .FirstOrDefaultAsync();
    }

    // RC secuencial para recibo 
    private async Task<string> SiguienteReciboIdAsync()
    {
        const string prefijo = "RC";
        const int ancho = 8;
        var ids = await _db.RECIBO.Select(r => r.RECIBO_ID)
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

    // NUEVO: ID de recibo (mismo estilo que usas en Revertir: prefijo + Guid recortado)
    private static string NewReciboId() => "RCB" + Guid.NewGuid().ToString("N").Substring(0, 7);

    // ======================================================
    // GET: /Pedidos  (listado con filtros/orden/paginación)
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PedidoIndexVM vm)
    {
        var q = _db.PEDIDO.AsNoTracking().AsQueryable();

        // Filtros
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

        // Orden
        bool asc = string.Equals(vm.Dir, "asc", StringComparison.OrdinalIgnoreCase);
        switch ((vm.Sort ?? "id").ToLower())
        {
            case "fecha":
                q = asc ? q.OrderBy(p => p.FECHA_PEDIDO) : q.OrderByDescending(p => p.FECHA_PEDIDO);
                break;
            case "cliente":
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
            default:
                q = asc ? q.OrderBy(p => p.PEDIDO_ID) : q.OrderByDescending(p => p.PEDIDO_ID);
                break;
        }

        // Paginación
        vm.TotalItems = await q.CountAsync();
        if (vm.PageSize <= 0) vm.PageSize = 10;
        vm.TotalPages = (int)Math.Ceiling((double)vm.TotalItems / vm.PageSize);
        if (vm.Page <= 0) vm.Page = 1;
        if (vm.TotalPages > 0 && vm.Page > vm.TotalPages) vm.Page = vm.TotalPages;

        // Proyección
        var data = await q
            .Skip((vm.Page - 1) * vm.PageSize)
            .Take(vm.PageSize)
            .Select(p => new PedidoIndexItemVM
            {
                PedidoId = p.PEDIDO_ID,
                FechaPedido = p.FECHA_PEDIDO,
                EstadoPedidoId = p.ESTADO_PEDIDO_ID,
                FechaEntrega = p.FECHA_ENTREGA_PEDIDO,
                ClienteId = p.CLIENTE_ID,
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

        // Combos filtros
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
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new PedidoCreateEditVM
        {
            ClientesCombo = await GetClientesAsync(),
            ItemsCombo = await GetItemsElaboracionAsync(),
            EstadoPedidoId = "BORRADOR"
        };

        var tipoProductoFinalId = await _db.TIPO_PRODUCTO
            .Where(t => t.TIPO_PRODUCTO_NOMBRE == "PRODUCTO FINAL")
            .Select(t => t.TIPO_PRODUCTO_ID)
            .FirstOrDefaultAsync();

        var prods = await (
            from p in _db.PRODUCTO
            where p.ESTADO == true && p.TIPO_PRODUCTO_ID != tipoProductoFinalId
            select new
            {
                id = p.PRODUCTO_ID,
                nombre = p.PRODUCTO_NOMBRE,
                imagen = p.IMAGEN_PRODUCTO,
                stock = (decimal?)_db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .Select(i => i.STOCK_ACTUAL)
                            .FirstOrDefault() ?? 0m,
                // <- precioVenta = COSTO_UNITARIO
                precioVenta = _db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .Select(i => (decimal?)i.COSTO_UNITARIO)
                            .FirstOrDefault() ?? 0m
            }
        )
        .OrderBy(x => x.nombre)
        .ToListAsync();

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

        if (!vm.FechaEntregaDeseada.HasValue)
        {
            ModelState.AddModelError(nameof(vm.FechaEntregaDeseada), "Seleccione la fecha de entrega.");
        }
        else
        {
            var hoy = DateOnly.FromDateTime(DateTime.Now.Date);
            if (vm.FechaEntregaDeseada.Value < hoy)
                ModelState.AddModelError(nameof(vm.FechaEntregaDeseada), "La fecha de entrega debe ser hoy o posterior.");
        }

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
            FECHA_ENTREGA_PEDIDO = vm.FechaEntregaDeseada!.Value,
            OBSERVACIONES_PEDIDO = vm.Observaciones,
            USUARIO_CREACION = UserName(),
            FECHA_CREACION = DateTime.Now,
            ESTADO = true
        };

        // Autorrelleno de precio: si el usuario deja 0, usamos COSTO_UNITARIO
        var detalles = new List<DETALLE_PEDIDO>();
        foreach (var d in vm.Detalles)
        {
            if (string.IsNullOrWhiteSpace(d.ProductoId)) continue;

            var precioUi = d.PrecioPedido;
            if (precioUi <= 0)
            {
                // respaldo de precio desde inventario (COSTO_UNITARIO)
                var costo = await _db.INVENTARIO
                    .Where(i => i.PRODUCTO_ID == d.ProductoId)
                    .Select(i => (decimal?)i.COSTO_UNITARIO)
                    .FirstOrDefaultAsync(ct) ?? 0m;

                precioUi = costo;
            }

            var cantInt = RoundToInt(d.Cantidad);
            detalles.Add(new DETALLE_PEDIDO
            {
                DETALLE_PEDIDO_ID = NewDetallePedidoId(),
                PEDIDO_ID = pedido.PEDIDO_ID,
                PRODUCTO_ID = d.ProductoId!,
                CANTIDAD = cantInt,
                PRECIO_PEDIDO = precioUi,
                SUBTOTAL = Math.Round(cantInt * precioUi, 2),
                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            });
        }
        pedido.DETALLE_PEDIDO = detalles;

        // 1) Calcula total base a partir de detalle
        PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);
        // 2) Costo de elaboración (doble) + anticipo sobre total final (si así definiste la regla)
        DoblarTotalYRecalcularAnticipo(pedido);

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
            clienteNombre = pedido.CLIENTE_ID;

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

        // ========= VM =========
        var total = (pedido.TOTAL_PEDIDO != 0m) ? pedido.TOTAL_PEDIDO : lineas.Sum(x => x.Subtotal);
        var vm = new PedidoDetailsVM
        {
            PedidoId = pedido.PEDIDO_ID,
            EstadoId = pedido.ESTADO_PEDIDO_ID,
            EstadoNombre = pedido.ESTADO_PEDIDO_ID,
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

        //ViewBag.MetodosPagoCombo = await GetMetodosPagoComboAsync(ct);
        ViewBag.MetodosPagoCombo = await _db.METODO_PAGO
            .Where(m => m.ESTADO == true && m.ELIMINADO == false)
            .OrderBy(m => m.METODO_PAGO_NOMBRE)
            .Select(m => new SelectListItem { Value = m.METODO_PAGO_ID, Text = m.METODO_PAGO_NOMBRE })
            .ToListAsync(ct);


        // ========= Permisos según estado =========
        var st = pedido.ESTADO_PEDIDO_ID?.ToUpperInvariant() ?? "";

        vm.PuedeCotizar = st == "BORRADOR";
        vm.PuedeAprobar = st == "COTIZADO";
        // (Si usas Programar como paso explícito, puedes añadirlo)
        // vm.PuedeProgramar = st == "APROBADO";
        vm.PuedeFinalizar = st == "PROGRAMADO" || st == "EN_PRODU" || st == "APROBADO"; // si habilitas Finalizar directo desde APROBADO
        vm.PuedeEntregar = st == "TERMINADO";
        vm.PuedePagarAnticipo = pedido.REQUIERE_ANTICIPO && string.Equals(pedido.ANTICIPO_ESTADO, "PENDIENTE", StringComparison.OrdinalIgnoreCase);

        // Cancelar: solo APROBADO o EN_PRODU (flujo final acordado)
        vm.PuedeCancelar = st is "APROBADO" or "EN_PRODU";

        // Rechazar en COTIZADO (y si quieres en APROBADO)
        vm.PuedeRechazar = st is "COTIZADO";

        // ========= SALDO PENDIENTE =========
        // Regla: Saldo = TOTAL_PEDIDO - ANTICIPO_MINIMO (redondeado a 2 decimales y nunca < 0)
        var saldo = Math.Round(vm.TotalPedido - vm.AnticipoMinimo, 2);
        if (saldo < 0) saldo = 0m;
        vm.SaldoPendiente = saldo;

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

        if (pedido.ESTADO_PEDIDO_ID is not ("BORRADOR" or "COTIZADO"))
        {
            TempData["err"] = "Solo puedes modificar pedidos en estado BORRADOR O COTIZADO";
            return RedirectToAction(nameof(Details), new { id });
        }

        var productos = await _db.PRODUCTO
            .Where(p => p.ESTADO == true && p.TIPO_PRODUCTO.TIPO_PRODUCTO_NOMBRE != "PRODUCTO FINAL")
            .Select(p => new
            {
                id = p.PRODUCTO_ID,
                nombre = p.PRODUCTO_NOMBRE,
                imagen = p.IMAGEN_PRODUCTO,
                stock = (decimal?)_db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .Select(i => i.STOCK_ACTUAL)
                            .FirstOrDefault() ?? 0m,
                costo = (decimal?)_db.INVENTARIO
                            .Where(i => i.PRODUCTO_ID == p.PRODUCTO_ID)
                            .OrderByDescending(i => i.FECHA_CREACION)
                            .Select(i => i.COSTO_UNITARIO)
                            .FirstOrDefault() ?? 0m
            })
            .OrderBy(p => p.nombre)
            .ToListAsync();

        ViewBag.ProductosJson = System.Text.Json.JsonSerializer.Serialize(
            productos,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }
        );

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
// ==============================================
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(string id, PedidoCreateEditVM vm, CancellationToken ct)
{
    var pedido = await _db.PEDIDO
        .Include(p => p.DETALLE_PEDIDO)
        .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);

    if (pedido == null) return NotFound();

        if (pedido.ESTADO_PEDIDO_ID is not ("BORRADOR" or "COTIZADO"))
        {
            TempData["err"] = "Solo puedes modificar pedidos en estado BORRADOR O COTIZADO.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ===== Validación fecha de entrega: requerida y >= hoy =====
        if (!vm.FechaEntregaDeseada.HasValue)
    {
        TempData["err"] = "Seleccione la fecha de entrega.";
        return RedirectToAction(nameof(Edit), new { id });
    }
    var hoy = DateOnly.FromDateTime(DateTime.Now.Date);
    if (vm.FechaEntregaDeseada.Value < hoy)
    {
        TempData["err"] = "La fecha de entrega debe ser hoy o posterior.";
        return RedirectToAction(nameof(Edit), new { id });
    }

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

    // ===== Snapshot para determinar cambios y transición de anticipo =====
    var snap = new
    {
        pedido.TOTAL_PEDIDO,
        pedido.ESTADO_PEDIDO_ID,
        OBS = pedido.OBSERVACIONES_PEDIDO ?? "",
        pedido.CLIENTE_ID,
        FECHA_ENTREGA = pedido.FECHA_ENTREGA_PEDIDO,
        pedido.REQUIERE_ANTICIPO,
        pedido.ANTICIPO_MINIMO,
        ANT_EST = pedido.ANTICIPO_ESTADO ?? ""
    };

    // ===== Cabecera =====
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

    // ===== Detalle (UPSERT por PRODUCTO_ID) =====
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

    // ===== Totales y Anticipo (regla 300 y 25%) =====
    PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);
    DoblarTotalYRecalcularAnticipo(pedido); // aplica costo de elaboración (=base) y recalcula anticipo

    // ===== Transición de estado de anticipo según nuevo total =====
    // Si ahora REQUIERE anticipo y no está PAGADO, forzar PENDIENTE si antes no aplicaba o estaba vacío.
    if (pedido.REQUIERE_ANTICIPO)
    {
        if (!string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            if (!snap.REQUIERE_ANTICIPO
                || string.IsNullOrWhiteSpace(snap.ANT_EST)
                || string.Equals(snap.ANT_EST, "NO APLICA", StringComparison.OrdinalIgnoreCase))
            {
                pedido.ANTICIPO_ESTADO = "PENDIENTE";
                pedido.USUARIO_MODIFICACION = UserName();
                pedido.FECHA_MODIFICACION = DateTime.Now;
                hayCambiosCab = true;
            }
        }
    }
    else
    {
        // Ya no requiere anticipo → normalizar a NO APLICA
        if (!string.Equals(pedido.ANTICIPO_ESTADO ?? "", "NO APLICA", StringComparison.OrdinalIgnoreCase))
        {
            pedido.ANTICIPO_ESTADO = "NO APLICA";
            pedido.ANTICIPO_MINIMO = 0m;
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;
            hayCambiosCab = true;
        }
    }

    // ===== ¿Hubo cambios? =====
    bool cambioSupervisado =
        snap.TOTAL_PEDIDO != pedido.TOTAL_PEDIDO ||
        snap.ESTADO_PEDIDO_ID != pedido.ESTADO_PEDIDO_ID ||
        snap.OBS != (pedido.OBSERVACIONES_PEDIDO ?? "") ||
        snap.CLIENTE_ID != pedido.CLIENTE_ID ||
        snap.FECHA_ENTREGA != pedido.FECHA_ENTREGA_PEDIDO ||
        snap.REQUIERE_ANTICIPO != pedido.REQUIERE_ANTICIPO ||
        snap.ANTICIPO_MINIMO != pedido.ANTICIPO_MINIMO ||
        snap.ANT_EST != (pedido.ANTICIPO_ESTADO ?? "");

    if (cambioSupervisado && !hayCambiosCab)
    {
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;
        hayCambiosCab = true;
    }

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
    // GET: /Pedidos/Cotizar/{id}
    // ======================================================
    [HttpGet]
    public async Task<IActionResult> Cotizar(string id)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id);
        if (pedido == null) return NotFound();

        // Solo BORRADOR puede entrar a pantalla de cotización (opcional, informativo)
        if (!string.Equals(pedido.ESTADO_PEDIDO_ID, "BORRADOR", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "Solo un pedido BORRADOR puede ser cotizado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new PedidoCotizarVM { PedidoId = id };
        return View(vm);
    }

    // ======================================================
    // POST: /Pedidos/Cotizar/{id}
    //   - Recalcula totales/anticipo SIN doblar
    //   - Estado: BORRADOR -> COTIZADO
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cotizar(string id, PedidoCotizarVM vm, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        if (!string.Equals(pedido.ESTADO_PEDIDO_ID, "BORRADOR", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "Solo un pedido BORRADOR puede ser cotizado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Recalcular totales/anticipo base (sin doblar)
        PedidoCalculadora.RecalcularTotalesYAnticipo(pedido);

        pedido.ESTADO_PEDIDO_ID = "COTIZADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Cotizó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = $"Cotización lista. Total Q{pedido.TOTAL_PEDIDO:N2}" +
                         (pedido.REQUIERE_ANTICIPO ? $" | Anticipo 25%: Q{pedido.ANTICIPO_MINIMO:N2}" : "");
        return RedirectToAction(nameof(Details), new { id });
    }

    // ======================================================
    // POST: /Pedidos/Aprobar/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        // Si requiere anticipo, debe estar PAGADO
        if (pedido.REQUIERE_ANTICIPO &&
            !string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "Para aprobar el pedido, el anticipo requerido debe estar PAGADO.";
            return RedirectToAction("Details", new { id });
        }

        // 🔒 VALIDACIÓN DE STOCK EN SERVIDOR (evita CHECK de inventario < 0)
        var faltantes = new List<string>();
        foreach (var d in pedido.DETALLE_PEDIDO)
        {
            var inv = await _db.INVENTARIO
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.PRODUCTO_ID == d.PRODUCTO_ID, ct);

            var disponible = inv?.STOCK_ACTUAL ?? 0;
            var req = RoundToInt(Convert.ToDecimal(d.CANTIDAD));
            if (disponible < req)
            {
                // Obtiene nombre del producto (opcional)
                var nombre = await _db.PRODUCTO
                    .Where(p => p.PRODUCTO_ID == d.PRODUCTO_ID)
                    .Select(p => p.PRODUCTO_NOMBRE)
                    .FirstOrDefaultAsync(ct) ?? d.PRODUCTO_ID;

                faltantes.Add($"{nombre} (Disp: {disponible}, Requerido: {req})");
            }
        }
        if (faltantes.Count > 0)
        {
            TempData["err"] = "No se puede aprobar: stock insuficiente en:\n• " +
                              string.Join("\n• ", faltantes);
            return RedirectToAction("Details", new { id });
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Estado
            pedido.ESTADO_PEDIDO_ID = "APROBADO";
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;

            foreach (var d in pedido.DETALLE_PEDIDO)
            {
                var prodId = d.PRODUCTO_ID;
                var cant = RoundToInt(Convert.ToDecimal(d.CANTIDAD));

                var yaReservado = await _db.KARDEX.AnyAsync(k =>
                    k.PRODUCTO_ID == prodId &&
                    k.REFERENCIA == pedido.PEDIDO_ID &&
                    k.TIPO_MOVIMIENTO == "RESERVA PEDIDO", ct);

                if (!yaReservado)
                {
                    // KARDEX (con PK válida de 10 chars)
                    _db.KARDEX.Add(new KARDEX
                    {
                        KARDEX_ID = NewId10(),
                        PRODUCTO_ID = prodId,
                        FECHA = DateTime.Now,
                        TIPO_MOVIMIENTO = "RESERVA PEDIDO",
                        CANTIDAD = cant,
                        COSTO_UNITARIO = 0m,
                        REFERENCIA = pedido.PEDIDO_ID,
                        USUARIO_CREACION = UserName(),
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    });

                    // INVENTARIO: descuenta stock
                    var inv = await _db.INVENTARIO.FirstOrDefaultAsync(i => i.PRODUCTO_ID == prodId, ct);
                    if (inv == null)
                    {
                        inv = new INVENTARIO
                        {
                            INVENTARIO_ID = NewId10(),
                            PRODUCTO_ID = prodId,
                            STOCK_ACTUAL = 0,
                            STOCK_MINIMO = 0,
                            COSTO_UNITARIO = 0,
                            ESTADO = true,
                            USUARIO_CREACION = UserName(),
                            FECHA_CREACION = DateTime.Now
                        };
                        _db.INVENTARIO.Add(inv);
                    }
                    inv.STOCK_ACTUAL -= cant; // ya validamos suficiente stock arriba
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _bitacora.LogAsync("KARDEX", "INSERT", UserName(), $"Reserva de insumos por pedido {pedido.PEDIDO_ID}", ct);
            await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Aprobó pedido {pedido.PEDIDO_ID}", ct);

            TempData["ok"] = "Pedido aprobado y reservado.";
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["err"] = "Error al aprobar y reservar: " + RootError(ex);
            return RedirectToAction("Details", new { id });
        }
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
            return RedirectToAction(nameof(Details), new { id });
        }

        var st = (pedido.ESTADO_PEDIDO_ID ?? "").ToUpperInvariant();
        if (st is "ENTREGADO" or "CANCELADO" or "RECHAZADO")
        {
            TempData["err"] = "No se puede registrar anticipo en el estado actual del pedido.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!string.Equals(pedido.ANTICIPO_ESTADO, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "El anticipo ya fue procesado previamente.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ======== Lectura del modal ========
        string metodoId = (Request.Form["MetodoPagoId"].ToString() ?? "").Trim(); // MP0000...
        string montoStr = (Request.Form["MontoPago"].ToString() ?? "0").Trim();
        string efectivoStr = (Request.Form["EfectivoRecibido"].ToString() ?? "").Trim();

        static decimal ParseMoney(string s)
        {
            s = (s ?? "").Trim().Replace("Q", "", StringComparison.OrdinalIgnoreCase).Replace(",", "");
            return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        if (string.IsNullOrWhiteSpace(metodoId))
        {
            TempData["err"] = "Debe seleccionar el método de pago.";
            return RedirectToAction(nameof(Details), new { id });
        }

        decimal monto = ParseMoney(montoStr);
        decimal anticipo = pedido.ANTICIPO_MINIMO <= 0 ? 0m : pedido.ANTICIPO_MINIMO;

        if (monto <= 0 || Math.Abs(monto - anticipo) > 0.01m)
        {
            TempData["err"] = $"Monto de anticipo inválido. Debe ser Q{anticipo:N2}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Si ya está totalmente pagado, no tiene sentido cobrar anticipo
        if (await TotalPagadoPedidoAsync(pedido.PEDIDO_ID, ct) >= pedido.TOTAL_PEDIDO)
        {
            TempData["err"] = "El pedido ya no tiene saldo pendiente.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Resolver el nombre del método por el ID (p.ej. MP00000001 -> "EFECTIVO")
        string metodoNombre = await _db.METODO_PAGO
            .Where(x => x.METODO_PAGO_ID == metodoId)
            .Select(x => x.METODO_PAGO_NOMBRE)
            .FirstOrDefaultAsync(ct) ?? metodoId;

        bool esEfectivo = metodoNombre.Equals("EFECTIVO", StringComparison.OrdinalIgnoreCase);
        if (esEfectivo)
        {
            var recibido = ParseMoney(efectivoStr);
            if (recibido < monto)
            {
                TempData["err"] = "El efectivo recibido no puede ser menor al monto del anticipo.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        await using var trx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // ==== CAJA (INGRESO) con referencia "ANTICIPO PED:{id}"
            var sesion = await GetSesionCajaActivaAsync(UserName());
            if (!string.IsNullOrWhiteSpace(sesion))
            {
                var mov = new MOVIMIENTO_CAJA
                {
                    MOVIMIENTO_ID = await SiguienteMovimientoCajaIdAsync(),
                    SESION_ID = sesion,
                    TIPO = "INGRESO",
                    MONTO = monto,
                    REFERENCIA = $"ANTICIPO {pedido.PEDIDO_ID}",
                    FECHA = DateTime.Now,
                    USUARIO_CREACION = UserName(),
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                };
                _db.MOVIMIENTO_CAJA.Add(mov);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                await _bitacora.LogAsync("CAJA_SESION", "WARN", UserName(),
                    $"Anticipo Q{monto:N2} para pedido {pedido.PEDIDO_ID} sin sesión de caja activa.", ct);
            }

            // ==== Marcar el anticipo como PAGADO
            pedido.ANTICIPO_ESTADO = "PAGADO";
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;

            await _db.SaveChangesAsync(ct);

            await _bitacora.LogAsync("CAJA", "INSERT", UserName(),
                $"Anticipo PED:{pedido.PEDIDO_ID} por Q{monto:N2} (método: {metodoNombre}).", ct);

            await trx.CommitAsync(ct);

            TempData["ok_anticipo"] = $"Anticipo registrado por Q{monto:N2}.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            await trx.RollbackAsync(ct);
            TempData["err"] = "No se pudo registrar el anticipo: " + ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // ======================================================
    // POST: /Pedidos/EnProduccion/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnProduccion(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        var st = (pedido.ESTADO_PEDIDO_ID ?? "").ToUpperInvariant();

        if (st == "EN_PRODU")
        {
            TempData["ok"] = "El pedido ya está en producción.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (st != "APROBADO")
        {
            TempData["err"] = "Para iniciar producción, el pedido debe estar APROBADO.";
            return RedirectToAction(nameof(Details), new { id });
        }

        pedido.ESTADO_PEDIDO_ID = "EN_PRODU";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Inició producción de {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Pedido marcado como EN_PRODUCCIÓN.";
        return RedirectToAction(nameof(Details), new { id });
    }


    // ======================================================
    // POST: /Pedidos/Finalizar/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        var st = (pedido.ESTADO_PEDIDO_ID ?? "").ToUpperInvariant();
        if (st != "EN_PRODU")
        {
            TempData["err"] = "Para finalizar, el pedido debe estar EN_PRODU.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (pedido.REQUIERE_ANTICIPO &&
            !string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "No se puede finalizar: el anticipo requerido está PENDIENTE.";
            return RedirectToAction(nameof(Details), new { id });
        }

        using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var d in pedido.DETALLE_PEDIDO)
            {
                var prodId = d.PRODUCTO_ID;

                // Buscar la reserva para este producto y pedido
                var reserva = await _db.KARDEX
                    .Where(k => k.PRODUCTO_ID == prodId
                                && k.REFERENCIA == pedido.PEDIDO_ID
                                && k.TIPO_MOVIMIENTO == "RESERVA PEDIDO")
                    .OrderBy(k => k.FECHA)
                    .FirstOrDefaultAsync(ct);

                var costoUnit = d.PRECIO_PEDIDO; 

                if (reserva != null)
                {
                    // Convertir en la MISMA fila - no mueve stock
                    reserva.TIPO_MOVIMIENTO = "SALIDA PEDIDO";
                    reserva.FECHA = DateTime.Now;
                    reserva.USUARIO_MODIFICACION = UserName();
                    reserva.FECHA_MODIFICACION = DateTime.Now;

                    // actualizar COSTO_UNITARIO en la fila convertida
                    reserva.COSTO_UNITARIO = costoUnit;
                }
                else
                {
                    // registrar SALIDA sin tocar stock 
                    _db.KARDEX.Add(new KARDEX
                    {
                        KARDEX_ID = NewId10(),
                        PRODUCTO_ID = prodId,
                        FECHA = DateTime.Now,
                        TIPO_MOVIMIENTO = "SALIDA PEDIDO",
                        CANTIDAD = RoundToInt(Convert.ToDecimal(d.CANTIDAD)),
                        // guardar costo unitario desde el detalle
                        COSTO_UNITARIO = costoUnit,
                        REFERENCIA = pedido.PEDIDO_ID,
                        USUARIO_CREACION = UserName(),
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    });
                }
            }

            // Estado final
            pedido.ESTADO_PEDIDO_ID = "TERMINADO";
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _bitacora.LogAsync("KARDEX", "UPDATE", UserName(), $"Convertida RESERVA→SALIDA por pedido {pedido.PEDIDO_ID}", ct);
            await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Finalizó pedido {pedido.PEDIDO_ID}", ct);

            TempData["ok"] = "Pedido finalizado. Reserva convertida a SALIDA PEDIDO (sin mover stock nuevamente).";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["err"] = "Error al finalizar: " + ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // ======================================================
    // POST: /Pedidos/Entregar/{id}
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entregar(string id, CancellationToken ct)
    {
        // 1) Cargar pedido
        var pedido = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        // 2) Guardas de negocio
        if (!string.Equals(pedido.ESTADO_PEDIDO_ID, "TERMINADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "El pedido debe estar TERMINADO para poder entregarlo.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (pedido.REQUIERE_ANTICIPO && !string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "No se puede entregar: anticipo requerido no pagado.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 3) Lectura y validación del modal de pago
        string metodo = (Request.Form["MetodoPagoId"].ToString() ?? "").Trim();
        string montoStr = (Request.Form["MontoPago"].ToString() ?? "0").Trim();
        string efectivoStr = (Request.Form["EfectivoRecibido"].ToString() ?? "").Trim();

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
            TempData["err"] = "Monto a cobrar inválido.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (metodo.Equals("EFECTIVO", StringComparison.OrdinalIgnoreCase))
        {
            var recibido = ParseMoney(efectivoStr);
            if (recibido < monto)
            {
                TempData["err"] = "El efectivo recibido no puede ser menor al monto a cobrar.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // 3.1) Validar contra saldo pendiente real
        //     (sumamos el anticipo pagado porque NO se registró como RECIBO)
        var pagadoActual = await TotalPagadoPedidoAsync(id, ct);
        if (pedido.REQUIERE_ANTICIPO && string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            pagadoActual += Math.Max(0m, pedido.ANTICIPO_MINIMO);
        }
        var pendienteReal = Math.Max(0, pedido.TOTAL_PEDIDO - pagadoActual);
        if (monto > pendienteReal + 0.01m)
        {
            TempData["err"] = $"El monto excede el saldo pendiente (Q{pendienteReal:N2}).";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 4) Todo en una transacción: RECIBO + CAJA + ENTREGADO
        await using var trx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 4.1) RECIBO
            var rcId = await SiguienteReciboIdAsync();
            var recibo = new RECIBO
            {
                RECIBO_ID = rcId,
                VENTA_ID = pedido.PEDIDO_ID,          // referencia al pedido
                METODO_PAGO_ID = metodo,
                MONTO = monto,                         // ← aquí ya llega SOLO el saldo
                FECHA = DateTime.Now,
                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            };
            _db.RECIBO.Add(recibo);
            await _db.SaveChangesAsync(ct);

            // 4.2) CAJA (INGRESO)
            var sesion = await GetSesionCajaActivaAsync(UserName());
            if (!string.IsNullOrWhiteSpace(sesion))
            {
                var mov = new MOVIMIENTO_CAJA
                {
                    MOVIMIENTO_ID = await SiguienteMovimientoCajaIdAsync(),
                    SESION_ID = sesion,
                    TIPO = "INGRESO",
                    MONTO = monto,                     // ← se registra el saldo cobrado
                    REFERENCIA = pedido.PEDIDO_ID,
                    FECHA = DateTime.Now,
                    USUARIO_CREACION = UserName(),
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                };
                _db.MOVIMIENTO_CAJA.Add(mov);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                await _bitacora.LogAsync("CAJA_SESION", "WARN", UserName(),
                    $"Pago Q{monto:N2} para pedido {pedido.PEDIDO_ID} sin sesión de caja activa.", ct);
            }

            // 4.3) ENTREGADO
            pedido.ESTADO_PEDIDO_ID = "ENTREGADO";
            if (!pedido.FECHA_ENTREGA_PEDIDO.HasValue)
                pedido.FECHA_ENTREGA_PEDIDO = DateOnly.FromDateTime(DateTime.Now);
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            await _bitacora.LogAsync("RECIBO", "INSERT", UserName(),
                $"Pago en entrega PED:{pedido.PEDIDO_ID} por Q{monto:N2}.", ct);
            await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(),
                $"Marcó ENTREGADO el pedido {pedido.PEDIDO_ID}", ct);

            await trx.CommitAsync(ct);

            // 5) OK
            var nuevoPagado = pagadoActual + monto;
            var nuevoPend = Math.Max(0, pedido.TOTAL_PEDIDO - nuevoPagado);
            TempData["ok_entregar"] = $"Pedido ENTREGADO y pago registrado (Q{monto:N2}). Pendiente: Q{nuevoPend:N2}.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            await trx.RollbackAsync(ct);
            TempData["err"] = "No se pudo completar la entrega: " + ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // ======================================================
    // POST: /Pedidos/Cancelar/{id}
    //   - Solo permite cancelar en APROBADO o EN_PRODU (según flujo final)
    //   - No realiza movimientos en INVENTARIO ni en KARDEX
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO
            .Include(p => p.DETALLE_PEDIDO)
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        var st = (pedido.ESTADO_PEDIDO_ID ?? "").ToUpperInvariant();
        if (st is not ("APROBADO" or "EN_PRODU"))
        {
            TempData["err"] = "Este pedido solo puede cancelarse en estado APROBADO o EN_PRODU.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Reponer stock por cada RESERVA PEDIDO
            foreach (var d in pedido.DETALLE_PEDIDO)
            {
                var reservas = await _db.KARDEX
                    .Where(k => k.PRODUCTO_ID == d.PRODUCTO_ID
                                && k.REFERENCIA == pedido.PEDIDO_ID
                                && k.TIPO_MOVIMIENTO == "RESERVA PEDIDO")
                    .ToListAsync(ct);

                foreach (var r in reservas)
                {
                    // Reponer stock
                    var inv = await _db.INVENTARIO.FirstOrDefaultAsync(i => i.PRODUCTO_ID == r.PRODUCTO_ID, ct);
                    if (inv != null) inv.STOCK_ACTUAL += r.CANTIDAD;

                    // Eliminar o anular la reserva
                    _db.KARDEX.Remove(r);
                }
            }

            pedido.ESTADO_PEDIDO_ID = "CANCELADO";
            pedido.USUARIO_MODIFICACION = UserName();
            pedido.FECHA_MODIFICACION = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _bitacora.LogAsync("KARDEX", "DELETE", UserName(), $"Reversión de reservas por cancelación {pedido.PEDIDO_ID}", ct);
            await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Canceló el pedido {pedido.PEDIDO_ID}", ct);

            TempData["ok"] = "Pedido cancelado. Reservas revertidas y stock repuesto.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ======================================================
    // POST: /Pedidos/Rechazar/{id}
    //   - Solo permite RECHAZAR si está COTIZADO
    //   - No toca inventario ni kardex
    // ======================================================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(string id, CancellationToken ct)
    {
        var pedido = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == id, ct);
        if (pedido == null) return NotFound();

        if (!string.Equals(pedido.ESTADO_PEDIDO_ID, "COTIZADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "Solo se puede rechazar un pedido en estado COTIZADO.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
        {
            TempData["err"] = "No es posible rechazar: el anticipo ya fue PAGADO. Cancela o gestiona la devolución primero.";
            return RedirectToAction(nameof(Details), new { id });
        }

        pedido.ESTADO_PEDIDO_ID = "RECHAZADO";
        pedido.USUARIO_MODIFICACION = UserName();
        pedido.FECHA_MODIFICACION = DateTime.Now;

        await _db.SaveChangesAsync(ct);
        await _bitacora.LogAsync("PEDIDO", "UPDATE", UserName(), $"Rechazó pedido {pedido.PEDIDO_ID}", ct);

        TempData["ok"] = "Pedido rechazado correctamente.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ==========================================
    // GET: /Pedidos/PagoEntregaModal?pedidoId=PE00000001
    // Devuelve parcial con total, anticipo pagado y saldo pendiente
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> PagoEntregaModal(string pedidoId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pedidoId))
            return BadRequest("PedidoId requerido.");

        var ped = await _db.PEDIDO.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PEDIDO_ID == pedidoId, ct);

        if (ped == null) return NotFound("Pedido no encontrado.");

        // debe estar TERMINADO para entregar
        if (!string.Equals(ped.ESTADO_PEDIDO_ID, "TERMINADO", StringComparison.OrdinalIgnoreCase))
            return BadRequest("El pedido debe estar TERMINADO para poder entregarlo.");

        var pagado = await TotalPagadoPedidoAsync(pedidoId, ct);

        var vm = new ReciboPedidoCreateVM
        {
            PedidoId = pedidoId,
            UsuarioId = UserName(),
            TotalPedido = ped.TOTAL_PEDIDO,
            TotalPagado = pagado,
            MetodosPagoCombo = await GetMetodosPagoComboAsync(ct)
        };

        return PartialView("_PagoPedidoModal", vm);
    }

    // ==========================================
    // POST: /Pedidos/RegistrarPagoEntrega
    // Registra recibo (PEDIDO_ID), movimiento de caja y responde JSON
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPagoEntrega(ReciboPedidoCreateVM vm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vm.PedidoId))
            return Json(new { ok = false, error = "Pedido no especificado." });

        var ped = await _db.PEDIDO.FirstOrDefaultAsync(p => p.PEDIDO_ID == vm.PedidoId, ct);
        if (ped == null)
            return Json(new { ok = false, error = "No se encontró el pedido." });

        if (!string.Equals(ped.ESTADO_PEDIDO_ID, "TERMINADO", StringComparison.OrdinalIgnoreCase))
            return Json(new { ok = false, error = "El pedido debe estar TERMINADO para entregar." });

        // Totales
        var pagadoActual = await TotalPagadoPedidoAsync(vm.PedidoId, ct);
        var pendiente = Math.Max(0, ped.TOTAL_PEDIDO - pagadoActual);

        // Si ya no hay pendiente, devolvemos ok sin cobrar
        if (pendiente <= 0 && vm.Monto <= 0)
        {
            return Json(new
            {
                ok = true,
                reciboId = (string?)null,
                totalPagado = pagadoActual.ToString("N2"),
                pendiente = "0.00",
                skipCobro = true
            });
        }

        // Validaciones del modal
        if (string.IsNullOrWhiteSpace(vm.MetodoPagoId))
            return Json(new { ok = false, error = "Seleccione el método de pago." });

        if (vm.Monto <= 0)
            return Json(new { ok = false, error = "El monto debe ser mayor a cero." });

        if (vm.Monto > pendiente + 0.01m)
            return Json(new { ok = false, error = $"El monto excede el saldo pendiente (Q{pendiente:N2})." });

        await using var trx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1) RECIBO ligado al PEDIDO
            var recibo = new RECIBO
            {
                RECIBO_ID = NewReciboId(),
                VENTA_ID = ped.PEDIDO_ID,
                METODO_PAGO_ID = vm.MetodoPagoId,
                MONTO = vm.Monto,
                FECHA = DateTime.Now,
                USUARIO_CREACION = UserName(),
                FECHA_CREACION = DateTime.Now,
                ESTADO = true
            };
            _db.RECIBO.Add(recibo);
            await _db.SaveChangesAsync(ct);

            // 2) CAJA (INGRESO) si hay sesión activa
            var sesion = await GetSesionCajaActivaAsync(vm.UsuarioId);
            if (!string.IsNullOrWhiteSpace(sesion))
            {
                var mov = new MOVIMIENTO_CAJA
                {
                    MOVIMIENTO_ID = await SiguienteMovimientoCajaIdAsync(),
                    SESION_ID = sesion,
                    TIPO = "INGRESO",
                    MONTO = vm.Monto,
                    REFERENCIA = vm.PedidoId,
                    FECHA = DateTime.Now,
                    USUARIO_CREACION = UserName(),
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                };
                _db.MOVIMIENTO_CAJA.Add(mov);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                await _bitacora.LogAsync("CAJA_SESION", "WARN", UserName(),
                    $"Pago Q{vm.Monto:N2} para pedido {ped.PEDIDO_ID} sin sesión de caja activa.", ct);
            }

            await _bitacora.LogAsync("RECIBO", "INSERT", UserName(),
                $"Pago en entrega PED:{ped.PEDIDO_ID} por Q{vm.Monto:N2}.", ct);

            await trx.CommitAsync(ct);

            var nuevoPagado = pagadoActual + vm.Monto;
            var nuevoPend = Math.Max(0, ped.TOTAL_PEDIDO - nuevoPagado);

            return Json(new
            {
                ok = true,
                reciboId = NewReciboId(),
                totalPagado = nuevoPagado.ToString("N2"),
                pendiente = nuevoPend.ToString("N2")
            });
        }
        catch (Exception ex)
        {
            await trx.RollbackAsync(ct);
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // ====================recibo PDF======================
    [HttpGet]
    public async Task<IActionResult> ReciboPedido(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        // ===== Cabecera + cliente (desde PEDIDO) =====
        var pedido = await (
            from p in _db.PEDIDO
            join c in _db.CLIENTE on p.CLIENTE_ID equals c.CLIENTE_ID
            join per in _db.PERSONA on c.CLIENTE_ID equals per.PERSONA_ID
            where p.PEDIDO_ID == id
            select new
            {
                p.PEDIDO_ID,
                p.FECHA_PEDIDO,
                CLIENTE = ((per.PERSONA_PRIMERNOMBRE ?? "") + " " + (per.PERSONA_PRIMERAPELLIDO ?? "")).Trim(),
                TOTAL = p.TOTAL_PEDIDO,
                p.REQUIERE_ANTICIPO,
                p.ANTICIPO_MINIMO,
                p.ANTICIPO_ESTADO
            }
        ).FirstOrDefaultAsync();

        if (pedido == null) return NotFound();

        // ===== Líneas del pedido =====
        var lineas = await (
            from d in _db.DETALLE_PEDIDO
            join prod in _db.PRODUCTO on d.PRODUCTO_ID equals prod.PRODUCTO_ID
            where d.PEDIDO_ID == id
            select new
            {
                Producto = prod.PRODUCTO_NOMBRE ?? d.PRODUCTO_ID,
                Cantidad = d.CANTIDAD,
                PrecioUnitario = (decimal?)d.PRECIO_PEDIDO ?? 0m,
                Subtotal = (decimal?)d.SUBTOTAL
                           ?? ((decimal)d.CANTIDAD) * ((decimal?)d.PRECIO_PEDIDO ?? 0m)
            }
        ).ToListAsync();

        // ===== Último RECIBO (pago en la entrega) =====
        // Nota: en tu flujo guardaste PEDIDO_ID en RECIBO.VENTA_ID
        var mp = await _db.RECIBO
            .Where(r => r.VENTA_ID == id && r.ESTADO == true)
            .OrderByDescending(r => r.FECHA)
            .Select(r => new { r.METODO_PAGO_ID, r.MONTO, r.USUARIO_CREACION })
            .FirstOrDefaultAsync();

        // Anticipo pagado (si aplica y está marcado como PAGADO)
        var anticipoPagado = (pedido.REQUIERE_ANTICIPO == true
            && string.Equals(pedido.ANTICIPO_ESTADO, "PAGADO", StringComparison.OrdinalIgnoreCase))
            ? pedido.ANTICIPO_MINIMO
            : 0m;

        var pagoEntrega = mp?.MONTO ?? 0m;
        var totalRecibido = anticipoPagado + pagoEntrega;

        var vm = new
        {
            PedidoId = pedido.PEDIDO_ID,
            Fecha = pedido.FECHA_PEDIDO,
            ClienteNombre = pedido.CLIENTE,
            UsuarioNombre = mp?.USUARIO_CREACION ?? "-",
            TotalPedido = pedido.TOTAL,
            // Nuevos campos para el recibo
            AnticipoPagado = anticipoPagado,
            PagoEntrega = pagoEntrega,
            TotalRecibido = totalRecibido,
            MetodoPagoEntrega = mp?.METODO_PAGO_ID ?? "—",
            Lineas = lineas
                .Select(l => new
                {
                    l.Producto,
                    l.Cantidad,
                    PrecioUnitario = l.PrecioUnitario,
                    Subtotal = l.Subtotal
                })
                .ToList()
        };

        return new Rotativa.AspNetCore.ViewAsPdf("ReciboPedido", vm)
        {
            FileName = $"Recibo_{pedido.PEDIDO_ID}.pdf",
            ContentDisposition = Rotativa.AspNetCore.Options.ContentDisposition.Inline,
            PageMargins = new Rotativa.AspNetCore.Options.Margins(15, 10, 15, 10),
            PageSize = Rotativa.AspNetCore.Options.Size.A5,
            PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
        };
    }

    //====================REPORTE PDF
    [HttpGet]
    public async Task<IActionResult> ReportePDF(
    string? Search,
    string? Estado,
    string? Cliente,
    bool? RequiereAnticipo,
    string? AnticipoEstado,
    DateTime? Desde,
    DateTime? Hasta,
    decimal? TotalMin,
    decimal? TotalMax,
    string? Sort = "id",
    string? Dir = "asc")
    {
        var q = _db.PEDIDO.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            string s = Search.Trim();
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

        if (!string.IsNullOrWhiteSpace(Estado))
            q = q.Where(p => p.ESTADO_PEDIDO_ID == Estado);

        if (!string.IsNullOrWhiteSpace(Cliente))
        {
            string c = Cliente.Trim();
            q = q.Where(p => _db.PERSONA.Any(per =>
                per.PERSONA_ID == p.CLIENTE_ID &&
                ((per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                 (per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                 (per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                 (per.PERSONA_SEGUNDOAPELLIDO ?? "")).Contains(c)
            ));
        }

        if (RequiereAnticipo.HasValue)
            q = q.Where(p => p.REQUIERE_ANTICIPO == RequiereAnticipo.Value);

        if (!string.IsNullOrWhiteSpace(AnticipoEstado))
            q = q.Where(p => (p.ANTICIPO_ESTADO ?? "") == AnticipoEstado);

        if (Desde.HasValue)
            q = q.Where(p => p.FECHA_PEDIDO >= Desde.Value);

        if (Hasta.HasValue)
        {
            var hasta = Hasta.Value.Date.AddDays(1).AddTicks(-1);
            q = q.Where(p => p.FECHA_PEDIDO <= hasta);
        }

        if (TotalMin.HasValue) q = q.Where(p => p.TOTAL_PEDIDO >= TotalMin.Value);
        if (TotalMax.HasValue) q = q.Where(p => p.TOTAL_PEDIDO <= TotalMax.Value);

        bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
        switch ((Sort ?? "id").ToLower())
        {
            case "fecha":
                q = asc ? q.OrderBy(p => p.FECHA_PEDIDO) : q.OrderByDescending(p => p.FECHA_PEDIDO);
                break;
            case "cliente":
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
            default:
                q = asc ? q.OrderBy(p => p.PEDIDO_ID) : q.OrderByDescending(p => p.PEDIDO_ID);
                break;
        }

        var data = await q
            .Select(p => new PedidoIndexItemVM
            {
                PedidoId = p.PEDIDO_ID,
                FechaPedido = p.FECHA_PEDIDO,
                EstadoPedidoId = p.ESTADO_PEDIDO_ID,
                FechaEntrega = p.FECHA_ENTREGA_PEDIDO,
                ClienteId = p.CLIENTE_ID,
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

        int totalPedidos = data.Count;
        int conAnticipo = data.Count(x => x.RequiereAnticipo);
        int sinAnticipo = data.Count(x => !x.RequiereAnticipo);
        decimal totalMontos = data.Sum(x => x.TotalPedido);

        var vm = new ReporteViewModel<PedidoIndexItemVM>
        {
            Items = data,
            Search = Search,
            Sort = Sort,
            Dir = Dir,

            ReportTitle = "Reporte de Pedidos",
            CompanyInfo = "CreArte Manualidades | Sololá, Guatemala | creartemanualidades2021@gmail.com",
            GeneratedBy = User?.Identity?.Name ?? "Usuario no autenticado",
            LogoUrl = Url.Content("~/Imagenes/logoCreArte.png")
        };

        vm.AddTotal("TotalPedidos", totalPedidos);
        vm.AddTotal("ConAnticipo", conAnticipo);
        vm.AddTotal("SinAnticipo", sinAnticipo);
        vm.AddTotal("MontoTotal", totalMontos);

        if (!string.IsNullOrWhiteSpace(Estado)) vm.ExtraFilters["Estado pedido"] = Estado;
        if (!string.IsNullOrWhiteSpace(Cliente)) vm.ExtraFilters["Cliente"] = Cliente;
        if (RequiereAnticipo.HasValue) vm.ExtraFilters["Requiere anticipo"] = RequiereAnticipo.Value ? "Sí" : "No";
        if (!string.IsNullOrWhiteSpace(AnticipoEstado)) vm.ExtraFilters["Estado anticipo"] = AnticipoEstado;
        if (Desde.HasValue || Hasta.HasValue)
            vm.ExtraFilters["Rango fechas"] =
                $"{Desde?.ToString("dd/MM/yyyy") ?? "—"} a {Hasta?.ToString("dd/MM/yyyy") ?? "—"}";
        if (TotalMin.HasValue || TotalMax.HasValue)
            vm.ExtraFilters["Rango total"] =
                $"{TotalMin?.ToString("Q0.00") ?? "—"} a {TotalMax?.ToString("Q0.00") ?? "—"}";

        var pdf = new Rotativa.AspNetCore.ViewAsPdf("ReportePedidos", vm)
        {
            FileName = $"ReportePedidos.pdf",
            ContentDisposition = Rotativa.AspNetCore.Options.ContentDisposition.Inline,
            PageSize = Rotativa.AspNetCore.Options.Size.Letter,
            PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
            PageMargins = new Rotativa.AspNetCore.Options.Margins
            {
                Left = 10,
                Right = 10,
                Top = 15,
                Bottom = 15
            },
            CustomSwitches =
                $"--footer-center \"Página [page] de [toPage]\"" +
                $" --footer-right \"CreArte Manualidades © {DateTime.Now:yyyy}\"" +
                $" --footer-font-size 9 --footer-spacing 3 --footer-line"
        };

        return pdf;
    }

}
