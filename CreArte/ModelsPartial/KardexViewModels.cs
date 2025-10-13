using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq; // <-- necesario para PageSizeOptions

namespace CreArte.ModelsPartial
{
    // ------------------ Por producto (sin cambios) ------------------
    public class KardexProductoFilterVM
    {
        [Required] public string PRODUCTO_ID { get; set; } = null!;
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
    }
    public class KardexMovimientoVM
    {
        public string KARDEX_ID { get; set; } = null!;
        public DateTime FECHA { get; set; }
        public string TIPO_MOVIMIENTO { get; set; } = null!;
        public int CANTIDAD { get; set; }
        public decimal? COSTO_UNITARIO { get; set; }
        public string? REFERENCIA { get; set; }
        public int SALDO_ACUMULADO { get; set; }
    }
    public class KardexProductoVM
    {
        public string PRODUCTO_ID { get; set; } = null!;
        public string ProductoNombre { get; set; } = null!;
        public string? ImagenUrl { get; set; }
        public KardexProductoFilterVM Filtros { get; set; } = new();
        public int SaldoInicial { get; set; }
        public List<KardexMovimientoVM> Movimientos { get; set; } = new();
        public int TotalEntradas { get; set; }
        public int TotalSalidas { get; set; }
        public int TotalAjustes { get; set; }
        public int SaldoFinal => SaldoInicial + TotalEntradas + TotalAjustes - TotalSalidas;
    }

    // ===============================================
    // INDEX GENERAL (lista plana con filtros/orden/paginación)
    // ===============================================
    public class KardexListItemVM
    {
        public string KARDEX_ID { get; set; } = null!;
        public DateTime FECHA { get; set; }
        public string PRODUCTO_ID { get; set; } = null!;
        public string ProductoNombre { get; set; } = null!;
        public string TIPO_MOVIMIENTO { get; set; } = null!;
        public int CANTIDAD { get; set; }
        public decimal? COSTO_UNITARIO { get; set; }
        public string? REFERENCIA { get; set; }
    }

    public class KardexIndexVM
    {
        // -------- Filtros --------
        public string? SearchProducto { get; set; }   // Nombre o PRODUCTO_ID
        public string? TipoMovimiento { get; set; }   // ENTRADA|SALIDA|AJUSTE
        public DateTime? Desde { get; set; }          // 00:00
        public DateTime? Hasta { get; set; }          // 23:59:59.999...
        public string? Referencia { get; set; }       // p.ej. CO00000021

        // -------- Orden --------
        // claves esperadas: "fecha" (defecto), "producto", "tipo", "cantidad", "costo", "ref", "id"
        public string? Sort { get; set; } = "fecha";
        // "asc" o "desc"
        public string? Dir { get; set; } = "desc";

        // -------- Paginación --------
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public IEnumerable<SelectListItem> PageSizeOptions =>
            new[] { 10, 20, 50, 100 }.Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString()
            });

        public int TotalRows { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRows / Math.Max(1, PageSize));

        // -------- Resultados --------
        public List<KardexListItemVM> Items { get; set; } = new();

        // (Opcional) para el combo de tipos en la vista
        public IEnumerable<SelectListItem> TiposMovimiento => new[]
        {
            new SelectListItem { Value = "", Text="(Todos)"},
            new SelectListItem { Value = "ENTRADA", Text="ENTRADA"},
            new SelectListItem { Value = "SALIDA",  Text="SALIDA"},
            new SelectListItem { Value = "AJUSTE",  Text="AJUSTE"},
        };
    }
}
