using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;

namespace CreArte.ModelsPartial
{
    // ------------------ Por producto ------------------
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
        // Encabezado
        public string PRODUCTO_ID { get; set; } = null!;
        public string ProductoNombre { get; set; } = null!;
        public string? ImagenUrl { get; set; }
        public string? INVENTARIO_ID { get; set; }

        // Filtros y datos
        public KardexProductoFilterVM Filtros { get; set; } = new();
        public int SaldoInicial { get; set; }
        public List<KardexMovimientoVM> Movimientos { get; set; } = new();

        // Totales por tipo
        public int TotalEntradas { get; set; }             // solo ENTRADA
        public int TotalSalidas { get; set; }              // solo SALIDA
        public int TotalAjustesEntrada { get; set; }       // AJUSTE ENTRADA
        public int TotalAjustesSalida { get; set; }       // AJUSTE SALIDA
        public int TotalAjustesPrecio { get; set; }       // conteo de AJUSTE PRECIO (cantidad = 0)

        // Saldo final = inicial + (entradas + ajustes entrada) - (salidas + ajustes salida)
        public int SaldoFinal => SaldoInicial
                                 + TotalEntradas + TotalAjustesEntrada
                                 - TotalSalidas - TotalAjustesSalida;
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
        public string? SearchProducto { get; set; }
        public string? TipoMovimiento { get; set; }   // ENTRADA|SALIDA|AJUSTE ENTRADA|AJUSTE SALIDA|AJUSTE PRECIO
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public string? Referencia { get; set; }

        // -------- Orden --------
        public string? Sort { get; set; } = "fecha";
        public string? Dir { get; set; } = "desc";

        // -------- Paginación --------
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public IEnumerable<SelectListItem> PageSizeOptions =>
            new[] { 10, 20, 50, 100 }.Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString() });

        public int TotalRows { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRows / Math.Max(1, PageSize));

        // -------- Resultados --------
        public List<KardexListItemVM> Items { get; set; } = new();

        // (Opcional) combo tipos en la vista Index
        public IEnumerable<SelectListItem> TiposMovimiento => new[]
        {
            new SelectListItem { Value = "",                Text="(Todos)"},
            new SelectListItem { Value = "ENTRADA",         Text="ENTRADA"},
            new SelectListItem { Value = "SALIDA",          Text="SALIDA"},
            new SelectListItem { Value = "AJUSTE ENTRADA",  Text="AJUSTE ENTRADA"},
            new SelectListItem { Value = "AJUSTE SALIDA",   Text="AJUSTE SALIDA"},
            new SelectListItem { Value = "AJUSTE PRECIO",   Text="AJUSTE PRECIO"},
        };
    }
}
