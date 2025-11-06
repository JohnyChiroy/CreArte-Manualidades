using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;

namespace CreArte.ModelsPartial
{
    // ===============================
    // VENTA - Cabecera + Detalles
    // ===============================
    public class VentaCreateEditVM
    {
        // Cabecera mínima según tu tabla VENTA
        public string? VentaId { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un cliente.")]
        public string ClienteId { get; set; } = null!; // = PERSONA_ID

        [Required(ErrorMessage = "Debe seleccionar un usuario vendedor.")]
        public string UsuarioId { get; set; } = null!;

        [DataType(DataType.DateTime)]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Seleccione el método de pago.")]
        public string? MetodoPagoId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto de pago debe ser mayor a 0.")]
        public decimal MontoPago { get; set; }

        // Totales (solo para cálculo en UI; en BD solo se guarda TOTAL)
        public List<VentaDetalleVM> Detalles { get; set; } = new();

        public decimal Subtotal => Math.Round(Detalles.Sum(d => d.Subtotal), 2);
        public decimal ImpuestoTotal => 0m; // ajusta si luego aplicas IVA
        public decimal DescuentoTotal => 0m;
        public decimal Total => Math.Round(Subtotal + ImpuestoTotal - DescuentoTotal, 2);

        // Combos
        public List<SelectListItem> ClientesCombo { get; set; } = new();
        public List<SelectListItem> ProductosCombo { get; set; } = new(); // opcional si lo usas para fallback
    }

    // ===============================
    //  DETALLE DE VENTA
    // ===============================
    public class VentaDetalleVM
    {
        // Por tu FK compuesta, debemos llevar ambos:
        [Required] public string InventarioId { get; set; } = null!;
        [Required] public string ProductoId { get; set; } = null!;

        public string? NombreProducto { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Cantidad inválida.")]
        public int Cantidad { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Precio inválido.")]
        public decimal PrecioUnitario { get; set; }

        public decimal Descuento { get; set; } = 0m; // futuro
        public decimal Impuesto { get; set; } = 0m;  // futuro

        public decimal Subtotal => Math.Round((Cantidad * PrecioUnitario) - Descuento + Impuesto, 2);
    }

    // ===============================================
    // Ítem de la grilla (una venta)
    // ===============================================
    public class VentaIndexItemVM
    {
        public string VentaId { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public string ClienteNombre { get; set; } = null!;
        public string UsuarioNombre { get; set; } = null!;
        public decimal Total { get; set; }
        public bool Estado { get; set; }  // true = activa, false = anulada (por si usas ese campo)
    }

    // ===============================================
    // ViewModel completo (filtros + paginación)
    // ===============================================
    public class VentaIndexVM
    {
        public List<VentaIndexItemVM> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }        // Buscar por ID o cliente
        public string? Cliente { get; set; }
        public string? Usuario { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public decimal? TotalMin { get; set; }
        public decimal? TotalMax { get; set; }

        // Orden
        public string? Sort { get; set; } = "fecha";
        public string? Dir { get; set; } = "desc";

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }


    // ===============================
    // REVERSIÓN DE VENTA (ANULACIÓN O DEVOLUCIÓN)
    // ===============================
    public class VentaReversionVM
    {
        public string VentaId { get; set; } = null!;
        public string UsuarioId { get; set; } = null!;
        public string TipoReversion { get; set; } = "DEVOLUCION"; // o ANULACION
        public string Motivo { get; set; } = null!;

        public List<VentaReversionDetalleVM> Detalles { get; set; } = new();
    }

    public class VentaReversionDetalleVM
    {
        public string InventarioId { get; set; } = null!;
        public string ProductoId { get; set; } = null!;
        public string NombreProducto { get; set; } = null!;
        public int CantidadVendida { get; set; }
        public int CantidadDevuelta { get; set; } // a devolver
        public decimal PrecioUnitario { get; set; }
    }

    // =============================
    // DETALLE DE VENTA (para vista Details)
    // =============================
    public class VentaLineaVM
    {
        public string ProductoId { get; set; } = null!;
        public string ProductoNombre { get; set; } = null!;
        public string? ImagenProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);
    }

    public class VentaDetailsVM
    {
        public string VentaId { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public string ClienteNombre { get; set; } = null!;
        public string UsuarioNombre { get; set; } = null!;
        //public string? Observaciones { get; set; }
        public decimal Total { get; set; }
        public bool Estado { get; set; }   // true = activa, false = anulada
        public string? MetodoPago { get; set; }

        public List<VentaLineaVM> Lineas { get; set; } = new();
    }

    public class ReciboCreateVM
    {
        public string VentaId { get; set; } = null!;
        public string UsuarioId { get; set; } = null!; // vendedor actual (para auditoría / caja)
        public string MetodoPagoId { get; set; } = null!;
        public decimal Monto { get; set; }

        // Info de ayuda para el modal
        public decimal TotalVenta { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal Pendiente => Math.Max(0, TotalVenta - TotalPagado);

        // Combos
        public List<SelectListItem> MetodosPagoCombo { get; set; } = new();
    }
    public class ReciboVentaVM
    {
        public string VentaId { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string UsuarioNombre { get; set; } = "";
        public string MetodoPago { get; set; } = "";
        public decimal MontoPagado { get; set; }
        public decimal Total { get; set; }
        public List<ReciboLineaVM> Lineas { get; set; } = new();
    }
    public class ReciboLineaVM
    {
        public string Producto { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }

}
