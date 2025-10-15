using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // -------- Listado/Tabla ----------
    public class InventarioListItemVM
    {
        public string INVENTARIO_ID { get; set; } = null!;
        public string PRODUCTO_ID { get; set; } = null!;
        public string ProductoNombre { get; set; } = null!;
        public string? ImagenUrl { get; set; }
        public int STOCK_ACTUAL { get; set; }
        public int STOCK_MINIMO { get; set; }
        public decimal COSTO_UNITARIO { get; set; }
        public DateTime? FECHA_VENCIMIENTO { get; set; }
        public bool ESTADO { get; set; }
        public bool StockBajo => STOCK_ACTUAL <= STOCK_MINIMO;
    }

    public class InventarioIndexVM
    {
        // ---------------- Filtros ----------------
        public string? Search { get; set; }          // Producto, código, inventario
        public bool SoloActivos { get; set; }        // ESTADO = true
        public bool SoloStockBajo { get; set; }      // STOCK_ACTUAL <= STOCK_MINIMO
        public DateTime? VenceAntesDe { get; set; }  // filtro por FECHA_VENCIMIENTO

        // ---------------- Orden ------------------
        // Claves permitidas: id, producto, stock, minimo, costo, vence, estado
        public string? Sort { get; set; } = "producto";
        public string? Dir { get; set; } = "asc"; // asc|desc

        // --------------- Paginación --------------
        public int Page { get; set; } = 1;          // página actual (1-based)
        public int PageSize { get; set; } = 20;     // filas por página
        public int TotalItems { get; set; }         // total de filas (para armar paginador)
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize));

        // UI: opciones del combo "por página" (para TagHelper asp-items)
        public IEnumerable<SelectListItem> PageSizeOptions =>
            new[] { 10, 20, 50, 100 }.Select(s => new SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString()
            });

        // --------------- Resultados --------------
        public List<InventarioListItemVM> Items { get; set; } = new();
    }

    // -------- Detalles ----------
    public class InventarioDetailsVM
    {
        public string INVENTARIO_ID { get; set; } = null!;
        public string PRODUCTO_ID { get; set; } = null!;
        public string ProductoNombre { get; set; } = null!;
        public string? ImagenUrl { get; set; }
        public int STOCK_ACTUAL { get; set; }
        public int STOCK_MINIMO { get; set; }
        public decimal COSTO_UNITARIO { get; set; }
        public DateTime? FECHA_VENCIMIENTO { get; set; }
        public bool ESTADO { get; set; }
    }

    // -------- Ajustes manuales (Entrada/Salida/Ajuste) ----------
    public class InventarioAjusteVM
    {
        [Required] public string PRODUCTO_ID { get; set; } = null!;

        // Usaremos estos valores: ENTRADA | SALIDA  (y seguimos soportando "AJUSTE" si hicieras algo puntual)
        [Required, RegularExpression("ENTRADA|SALIDA|AJUSTE")]
        public string TIPO_MOVIMIENTO { get; set; } = "AJUSTE";

        [Required, Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser > 0")]
        public int CANTIDAD { get; set; }

        // Para ENTRADA puedes dejarlo en 0 o null; en SALIDA no lo exigimos.
        public decimal? COSTO_UNITARIO { get; set; }

        // este es el campo que se grabará en KARDEX.REFERENCIA
        //[Display(Name = "Razón"), StringLength(300)]
        //public string? Razon { get; set; }

        [Display(Name = "Razón"), StringLength(300)]
        [Required(ErrorMessage = "La razón es obligatoria.")]
        public string? Razon { get; set; }
    }

    // ===================== AJUSTE DE PRECIO =====================
    //No modifica stock, SOLO cambia el costo en INVENTARIO
    //Inserta en KARDEX TIPO_MOVIMIENTO = "AJUSTE PRECIO" con CANTIDAD=0
    public class InventarioAjustePrecioVM
    {
        [Required] public string PRODUCTO_ID { get; set; } = null!;

        // (Opcional para mostrar en la vista; no es obligatorio tenerlo)
        [Display(Name = "Costo actual")]
        public decimal? CostoActual { get; set; }

        [Required, Display(Name = "Nuevo costo unitario")]
        [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
        public decimal NuevoCostoUnitario { get; set; }

        //[Display(Name = "Razón"), StringLength(300)]
        //public string? Razon { get; set; }

        [Display(Name = "Razón"), StringLength(300)]
        [Required(ErrorMessage = "La razón es obligatoria.")]
        public string? Razon { get; set; }
    }
}
