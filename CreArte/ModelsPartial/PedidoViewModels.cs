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
        public string ClienteNombre { get; set; } = default!; // Ahora mostramos ClienteId como nombre
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
        [Range(0.01, 9999999)] public decimal Cantidad { get; set; }   // VM usa decimal; controller castea a INT si tu entidad lo es
        [Range(0.00, 9999999)] public decimal PrecioPedido { get; set; }
        public decimal Subtotal => Math.Round(Cantidad * PrecioPedido, 2);
    }

    public class PedidoCreateEditVM
    {
        public string? PedidoId { get; set; }
        [Required] public string? ClienteId { get; set; }
        public DateOnly? FechaEntregaDeseada { get; set; }    // DB: DATE
        public string? Observaciones { get; set; }

        public List<PedidoDetalleVM> Detalles { get; set; } = new();

        public List<SelectListItem> ClientesCombo { get; set; } = new();
        public List<SelectListItem> ItemsCombo { get; set; } = new();

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
        public decimal ManoDeObra { get; set; }
        public decimal Margen { get; set; }
    }

    public class ProduccionProgramarVM
    {
        public string PedidoId { get; set; } = default!;
        public DateTime FechaInicio { get; set; } = DateTime.Today;
        public DateTime FechaFin { get; set; } = DateTime.Today.AddDays(2);
    }
}
