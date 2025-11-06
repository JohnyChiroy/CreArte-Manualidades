using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CreArte.ModelsPartial
{

    // ================================
    // Ítem de la grilla (una sesión)
    // ================================
    public class CajaIndexItemVM
    {
        public string CajaSesionId { get; set; } = null!;
        public string UsuarioNombre { get; set; } = null!;
        public DateTime FechaApertura { get; set; }
        public DateTime? FechaCierre { get; set; }
        public decimal MontoInicial { get; set; }
        public decimal TotalIngresos { get; set; }
        public decimal TotalEgresos { get; set; }
        public decimal SaldoFinal => Math.Round(MontoInicial + TotalIngresos - TotalEgresos, 2);
        public bool Abierta { get; set; }
    }

    // ==========================================
    // ViewModel de página (filtros + paginación)
    // ==========================================
    public class CajaIndexVM
    {
        // Resultados
        public List<CajaIndexItemVM> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }              // Busca por ID de sesión o referencia
        public string? Usuario { get; set; }             // Nombre de usuario (texto)
        public bool? Abierta { get; set; }               // true: abiertas, false: cerradas
        public DateTime? Desde { get; set; }             // Rango fecha APERTURA desde
        public DateTime? Hasta { get; set; }             // Rango fecha APERTURA hasta
        public decimal? MontoMin { get; set; }           // Rango de saldo/ingresos (opcional)
        public decimal? MontoMax { get; set; }

        // Orden (col: id, apertura, cierre, usuario, inicial, ingresos, egresos, saldo, estado)
        public string? Sort { get; set; } = "apertura";
        public string? Dir { get; set; } = "desc";

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    // ===========================================================
    // APERTURA
    // ===========================================================
    public class CajaAperturaVM
    {
        [Required(ErrorMessage = "Debe indicar el usuario.")]
        public string UsuarioId { get; set; } = null!;

        [Required(ErrorMessage = "Debe ingresar el monto inicial.")]
        [Range(0, double.MaxValue, ErrorMessage = "Monto inválido.")]
        public decimal MontoInicial { get; set; }

        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }
        public bool BloquearMonto { get; set; }
        public string? InfoAnterior { get; set; }

    }

    // ===========================================================
    // CIERRE
    // ===========================================================
    public class CajaCierreVM
    {
        [Required] public string CajaSesionId { get; set; } = null!;
        public string UsuarioId { get; set; } = null!;
        public string UsuarioNombre { get; set; } = null!;
        public decimal TotalIngresos { get; set; }
        public decimal TotalEgresos { get; set; }
        public decimal MontoInicial { get; set; }
        public decimal SaldoFinal => Math.Round(MontoInicial + TotalIngresos - TotalEgresos, 2);
        public DateTime FechaApertura { get; set; }
        public string? Observaciones { get; set; }
    }

    // ===========================================================
    // DETALLE DE MOVIMIENTOS
    // ===========================================================
    public class CajaDetalleMovimientoVM
    {
        public string MovimientoId { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public string TipoMovimiento { get; set; } = null!;
        public string Referencia { get; set; } = null!;
        public decimal Monto { get; set; }
        public string UsuarioNombre { get; set; } = null!;
    }
}
