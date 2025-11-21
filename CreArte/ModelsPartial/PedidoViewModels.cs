using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CreArte.ModelsPartial
{
    public class PedidoIndexItemVM
    {
        public string PedidoId { get; set; } = default!;
        public DateTime FechaPedido { get; set; }
        public string EstadoPedidoId { get; set; } = default!;
        public DateOnly? FechaEntrega { get; set; }        // DB: DATE
        public string ClienteId { get; set; } = default!;
        public string ClienteNombre { get; set; } = default!;
        public decimal TotalPedido { get; set; }
        public string? AnticipoEstado { get; set; }
        public bool RequiereAnticipo { get; set; }
    }

    public class PedidoIndexVM
    {
        public List<PedidoIndexItemVM> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }
        public string? Estado { get; set; }
        public string? Cliente { get; set; }
        public bool? RequiereAnticipo { get; set; }
        public string? AnticipoEstado { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public decimal? TotalMin { get; set; }
        public decimal? TotalMax { get; set; }

        // Orden
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc";

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Combos
        public List<SelectListItem> Estados { get; set; } = new();
        public List<SelectListItem> Clientes { get; set; } = new();
        public List<SelectListItem> Anticipos { get; set; } = new()
        {
            new("",""),
            new("PENDIENTE","PENDIENTE"),
            new("PAGADO","PAGADO"),
            new("DEVUELTO","DEVUELTO")
        };
    }

    public class PedidoDetalleVM
    {
        [Required] public string? ProductoId { get; set; }
        public string? ProductoNombre { get; set; }

        // VM usa decimal; controller castea a INT si tu entidad lo es
        [Range(0.01, 9999999)] public decimal Cantidad { get; set; }

        [Range(0.00, 9999999)] public decimal PrecioPedido { get; set; }

        public decimal PrecioUnitario { get; set; }

        public decimal Subtotal => Math.Round(Cantidad * PrecioPedido, 2);
    }

    public class PedidoCreateEditVM
    {
        public string? PedidoId { get; set; }

        [Required(ErrorMessage = "Seleccione un cliente.")]
        public string? ClienteId { get; set; }

        [Display(Name = "Fecha de entrega")]
        [DataType(DataType.Date)]
        [Required(ErrorMessage = "Seleccione la fecha de entrega.")]
        public DateOnly? FechaEntregaDeseada { get; set; }    
        public string? Observaciones { get; set; }

        public List<PedidoDetalleVM> Detalles { get; set; } = new();

        public List<SelectListItem> ClientesCombo { get; set; } = new();
        public List<SelectListItem> ItemsCombo { get; set; } = new();

        // Totales de la UI (solo para mostrar mientras editas la grilla)
        public decimal TotalPedido => Math.Round(Detalles.Sum(d => d.Subtotal), 2);
        public bool RequiereAnticipo => TotalPedido >= 300m;
        public decimal AnticipoMinimo => RequiereAnticipo ? Math.Round(TotalPedido * 0.25m, 2) : 0m;

        public string? EstadoPedidoId { get; set; }
        public string? AnticipoEstado { get; set; }
        public bool BloquearEdicion => string.Equals(AnticipoEstado, "PAGADO", StringComparison.OrdinalIgnoreCase);
    }

    public class PedidoCotizarVM
    {
        public string PedidoId { get; set; } = default!;
        // Si luego quieres reactivar mano de obra / margen, los dejamos aquí (no usados por ahora)
        public decimal ManoDeObra { get; set; }
        public decimal Margen { get; set; }
    }

    public class PedidoDetailsVM
    {
        // Encabezado
        public string PedidoId { get; set; } = null!;
        public string EstadoId { get; set; } = null!;
        public string? EstadoNombre { get; set; }
        public string ClienteId { get; set; } = null!;
        public string ClienteNombre { get; set; } = null!;
        public DateTime FechaPedido { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public string? Observaciones { get; set; }

        // Anticipo
        public bool RequiereAnticipo { get; set; }
        public string? AnticipoEstado { get; set; }
        public decimal AnticipoMinimo { get; set; }

        // Totales
        public decimal TotalPedido { get; set; }

        // Cálculo pedido: saldo a cobrar (TOTAL_PEDIDO – ANTICIPO_MÍNIMO)
        public decimal SaldoPendiente { get; set; }

        // Permisos de acción (para botones en la vista)
        public bool PuedeCotizar { get; set; }       // BORRADOR → COTIZADO
        public bool PuedeAprobar { get; set; }       // COTIZADO → APROBADO
        public bool PuedePagarAnticipo { get; set; } // si requiere y está PENDIENTE
        public bool PuedeFinalizar { get; set; }     // PROGRAMADO → EN_PRODU → TERMINADO
        public bool PuedeEntregar { get; set; }      // TERMINADO → ENTREGADO
        public bool PuedeCancelar { get; set; }      // APROBADO o EN_PRODU
        public bool PuedeRechazar { get; set; }      // COTIZADO o APROBADO (si aplica)

        public List<PedidoLineaVM> Lineas { get; set; } = new();
    }

    public class PedidoLineaVM
    {
        public string ProductoId { get; set; } = null!;
        public string ProductoNombre { get; set; } = null!;
        public string? ImagenProducto { get; set; }

        public decimal Cantidad { get; set; }
        public decimal PrecioPedido { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class ReciboPedidoCreateVM
    {
        public string PedidoId { get; set; } = null!;
        public string UsuarioId { get; set; } = null!;
        public string MetodoPagoId { get; set; } = null!;
        public decimal Monto { get; set; }                   // a cobrar en la entrega

        // Info para el modal
        public decimal TotalPedido { get; set; }             // TOTAL_PEDIDO
        public decimal TotalPagado { get; set; }             // suma de recibos vinculados al pedido
        public decimal Pendiente => Math.Max(0, TotalPedido - TotalPagado);

        // Combos
        public List<SelectListItem> MetodosPagoCombo { get; set; } = new();
    }
}
