using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CreArte.ModelsPartial
{
    // =========================================================================
    // 1) Utilidades y tipos genéricos para gráficos
    // -------------------------------------------------------------------------
    // Estas clases te permiten reutilizar estructuras comunes entre reportes:
    //  - Serie: para gráficos con varias series (línea/área/barras apiladas).
    //  - CategoriaValor: para Top N (label + valor).
    //  - EtiquetaValor: para segmentos (pie/doughnut).
    //  - Paleta: colores de tu marca para usar en Chart.js.
    // =========================================================================

    /// <summary>
    /// Serie para gráficos multi-series (línea, barras, etc.)
    /// </summary>
    public class Serie
    {
        /// <summary>Nombre de la serie (aparece en la leyenda)</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Valores (un valor por cada etiqueta del eje X)</summary>
        public List<decimal> Data { get; set; } = new();

        /// <summary>Color de trazo/relleno (hex o rgba) acorde a tu paleta</summary>
        public string Color { get; set; } = "#2E5AAC";
    }

    /// <summary>
    /// Par "categoría/valor" típico para Top N.
    /// </summary>
    public class CategoriaValor
    {
        public string Categoria { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    /// <summary>
    /// Par "etiqueta/valor" para pie/doughnut.
    /// </summary>
    public class EtiquetaValor
    {
        public string Etiqueta { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    /// <summary>
    /// Paleta de colores CreArte (ajústala si tienes variables globales).
    /// </summary>
    public static class Paleta
    {
        public const string Magenta = "#BF265E";
        public const string Dorado = "#EFBD21";
        public const string Azul = "#2E5AAC";
        public const string Verde = "#4CAF50";
        public const string Celeste = "#6EC8F2";
        public const string Gris = "#6B7280";

        /// <summary>Secuencia útil para gráficos con varios segmentos</summary>
        public static readonly List<string> Base = new()
        {
            Azul, Dorado, Magenta, Celeste, Verde, "#8ED081", "#A78BFA"
        };
    }

    // =========================================================================
    // 2) Reporte: VENTAS
    // -------------------------------------------------------------------------
    // - Línea: Ventas por día/mes (Labels + Serie única o múltiples series).
    // - Barras: Top N productos (categoría/valor).
    // - Pie: Distribución por método de pago.
    // =========================================================================
    public class VentasReportVM
    {
        // --- Línea: Ventas por período ---
        public List<string> LabelsPeriodo { get; set; } = new();     // eje X
        public List<Serie> SeriesPeriodo { get; set; } = new();      // 1 o más

        // --- Barras: Top N productos ---
        public List<CategoriaValor> TopProductos { get; set; } = new();

        // --- Pie: Métodos de pago ---
        public List<EtiquetaValor> MetodosPago { get; set; } = new();

        // --- Totales/resumen opcional (para tarjetas pequeñas en el partial) ---
        public decimal TotalPeriodo { get; set; }
        public int TicketsPeriodo { get; set; }
        public decimal TicketPromedio => TicketsPeriodo > 0 ? Math.Round(TotalPeriodo / TicketsPeriodo, 2) : 0;

        // --- Filtros que podrías usar en la UI ---
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public string? SucursalId { get; set; }  // si en el futuro manejas multi-sucursal
    }

    // =========================================================================
    // 3) Reporte: INVENTARIO
    // -------------------------------------------------------------------------
    // - Barras: Stock actual vs. Mínimo por producto/categoría.
    // - Línea: Rotación (consumo/ventas) por mes.
    // - Tabla/resumen: Productos con quiebre o bajo mínimo.
    // =========================================================================
    public class InventarioReportVM
    {
        // --- Barras comparativas (Stock vs Mínimo) ---
        public List<string> LabelsProductos { get; set; } = new();  // productos o categorías
        public List<decimal> StockActual { get; set; } = new();
        public List<decimal> StockMinimo { get; set; } = new();

        // --- Línea/área: Rotación por mes ---
        public List<string> LabelsMes { get; set; } = new();
        public List<Serie> SeriesRotacion { get; set; } = new(); // p.ej. "Ventas", "Salidas por pedido"

        // --- Resumen de quiebres/bajo mínimo ---
        public int ProductosBajoMinimo { get; set; }
        public int ProductosEnQuiebre { get; set; }
        public int TotalProductos { get; set; }
    }

    // =========================================================================
    // 4) Reporte: PEDIDOS
    // -------------------------------------------------------------------------
    // - Doughnut: Pedidos por estado (Nuevo, EnProceso, Listo, Entregado, Cancelado).
    // - Barras: Cumplimiento vs. Atraso por semana/mes.
    // - KPIs: Tiempo medio de preparación/entrega.
    // =========================================================================
    public class PedidosReportVM
    {
        // --- Segmentación por estado ---
        public List<EtiquetaValor> PedidosPorEstado { get; set; } = new();

        // --- Cumplimiento (barras) ---
        public List<string> LabelsPeriodo { get; set; } = new();  // semanas o meses
        public List<decimal> Cumplidos { get; set; } = new();
        public List<decimal> Atrasados { get; set; } = new();

        // --- KPIs ---
        public double TiempoMedioPreparacionHoras { get; set; }
        public double TiempoMedioEntregaHoras { get; set; }

        // --- Filtros ---
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
    }

    // =========================================================================
    // 5) Reporte: COMPRAS
    // -------------------------------------------------------------------------
    // - Línea: Gasto por mes.
    // - Barras: Top Proveedores por monto/cantidad.
    // - KPI: Lead time promedio de compra.
    // =========================================================================
    public class ComprasReportVM
    {
        // --- Línea: Gasto mensual ---
        public List<string> LabelsMes { get; set; } = new();
        public List<decimal> GastoPorMes { get; set; } = new();

        // --- Top proveedores ---
        public List<CategoriaValor> TopProveedoresPorMonto { get; set; } = new();
        public List<CategoriaValor> TopProveedoresPorCantidad { get; set; } = new();

        // --- KPI: lead time (días entre orden y recepción) ---
        public double LeadTimePromedioDias { get; set; }
    }

    // =========================================================================
    // 6) Reporte: USUARIOS (seguridad/uso del sistema)
    // -------------------------------------------------------------------------
    // - Barras: Usuarios por rol.
    // - Línea: Inicios de sesión por día (últimos N días).
    // - Tabla/KPI: Intentos fallidos, bloqueos.
    // =========================================================================
    public class UsuariosReportVM
    {
        // --- Usuarios por rol ---
        public List<CategoriaValor> UsuariosPorRol { get; set; } = new();

        // --- Inicio de sesión por día ---
        public List<string> LabelsDia { get; set; } = new();
        public List<decimal> LoginsPorDia { get; set; } = new();

        // --- KPIs de seguridad ---
        public int IntentosFallidosPeriodo { get; set; }
        public int UsuariosBloqueadosPeriodo { get; set; }
        public List<CategoriaValor> UsuariosPorArea { get; set; } = new();
    }

    // =========================================================================
    // 7) Reporte: EMPLEADOS
    // -------------------------------------------------------------------------
    // - Barras: Empleados por área/puesto.
    // - Línea: Asistencias por mes.
    // - KPI: Rotación (ingresos vs. bajas).
    // =========================================================================
    public class EmpleadosReportVM
    {
        // --- Empleados por área/puesto ---
        public List<CategoriaValor> EmpleadosPorPuesto { get; set; } = new();
        public List<CategoriaValor> EmpleadosPorArea { get; set; } = new();

        // --- Asistencias (por mes) ---
        public List<string> LabelsMes { get; set; } = new();
        public List<decimal> AsistenciasPorMes { get; set; } = new();

        // --- Rotación ---
        public int AltasPeriodo { get; set; }
        public int BajasPeriodo { get; set; }
    }

    // =========================================================================
    // 8) Reporte: CLIENTES
    // -------------------------------------------------------------------------
    // - Doughnut: Segmentación (% oro/plata/bronce u otro criterio).
    // - Barras: Recurrencia por mes (clientes con 2+ compras).
    // - Top N clientes por monto.
    // =========================================================================
    public class ClientesReportVM
    {
        // --- Segmentación ---
        public List<EtiquetaValor> Segmentacion { get; set; } = new(); // p.ej. Oro/Plata/Bronce

        // --- Recurrencia ---
        public List<string> LabelsMes { get; set; } = new();
        public List<decimal> ClientesRecurrentesPorMes { get; set; } = new();

        // --- Top clientes por monto ---
        public List<CategoriaValor> TopClientesPorMonto { get; set; } = new();
    }
    //public class ReporteViewModel<T> where T : class
    //{
    //    // -----------------------
    //    // Datos
    //    // -----------------------
    //    /// <summary>Items a renderizar en la vista</summary>
    //    public List<T> Items { get; set; } = new();

    //    // -----------------------
    //    // Filtros comunes
    //    // -----------------------
    //    /// <summary>Búsqueda global</summary>
    //    public string? Search { get; set; }

    //    /// <summary>Rango de fecha estándar</summary>
    //    public DateTime? FechaInicio { get; set; }
    //    public DateTime? FechaFin { get; set; }

    //    /// <summary>Estado típico (activo/inactivo)</summary>
    //    public bool? Estado { get; set; }

    //    /// <summary>Filtros adicionales (clave/valor) para chips o metadatos</summary>
    //    public Dictionary<string, string> ExtraFilters { get; set; } = new();

    //    // -----------------------
    //    // Ordenación
    //    // -----------------------
    //    public string? Sort { get; set; } = "id";
    //    public string? Dir { get; set; } = "asc"; // "asc" | "desc"

    //    // -----------------------
    //    // Paginación (si aplica)
    //    // -----------------------
    //    public int Page { get; set; } = 1;
    //    public int PageSize { get; set; } = 10;
    //    public int TotalItems { get; set; }
    //    public int TotalPages { get; set; }

    //    // -----------------------
    //    // Metadatos del reporte
    //    // -----------------------
    //    public string ReportTitle { get; set; } = string.Empty;
    //    public string CompanyInfo { get; set; } = string.Empty;
    //    public string GeneratedBy { get; set; } = string.Empty;
    //    public string? LogoUrl { get; set; } // ej: Url.Content("~/Imagenes/logo.png")

    //    /// <summary>Diccionario de totales por etiqueta (ej. "Activos" -> 10)</summary>
    //    public Dictionary<string, int> Totals { get; set; } = new();

    //    // -----------------------
    //    // Helpers
    //    // -----------------------
    //    public void AddTotal(string label, int value)
    //    {
    //        if (string.IsNullOrWhiteSpace(label)) return;
    //        Totals[label] = value;
    //    }

    //    public int GetTotal(string label)
    //    {
    //        return Totals != null && Totals.TryGetValue(label, out var v) ? v : 0;
    //    }

    //    internal void AddTotal(string v, decimal saldoTotal)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    public class ReporteViewModel<T> where T : class
    {
        // -----------------------
        // Datos
        // -----------------------
        /// <summary>Items a renderizar en la vista</summary>
        public List<T> Items { get; set; } = new();

        // -----------------------
        // Filtros comunes
        // -----------------------
        /// <summary>Búsqueda global</summary>
        public string? Search { get; set; }

        /// <summary>Rango de fecha estándar</summary>
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }

        /// <summary>Estado típico (activo/inactivo)</summary>
        public bool? Estado { get; set; }

        /// <summary>Filtros adicionales (clave/valor) para chips o metadatos</summary>
        public Dictionary<string, string> ExtraFilters { get; set; } = new();

        // -----------------------
        // Ordenación
        // -----------------------
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc"; // "asc" | "desc"

        // -----------------------
        // Paginación (si aplica)
        // -----------------------
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // -----------------------
        // Metadatos del reporte
        // -----------------------
        public string ReportTitle { get; set; } = string.Empty;
        public string CompanyInfo { get; set; } = string.Empty;
        public string GeneratedBy { get; set; } = string.Empty;
        public string? LogoUrl { get; set; } // ej: Url.Content("~/Imagenes/logo.png")

        // -----------------------
        // Totales
        // -----------------------
        /// <summary>Totales enteros (ej. "Activos" -> 10)</summary>
        public Dictionary<string, int> Totals { get; set; } = new();

        /// <summary>Totales decimales (ej. "SaldoTotal" -> 1234.56)</summary>
        public Dictionary<string, decimal> DecimalTotals { get; set; } = new();

        // -----------------------
        // Helpers
        // -----------------------

        // ---- INT ----
        public void AddTotal(string label, int value)
        {
            if (string.IsNullOrWhiteSpace(label)) return;
            Totals[label] = value;
        }

        public int GetTotal(string label)
        {
            return Totals != null && Totals.TryGetValue(label, out var v) ? v : 0;
        }

        // ---- DECIMAL ----
        public void AddTotal(string label, decimal value)
        {
            if (string.IsNullOrWhiteSpace(label)) return;
            DecimalTotals[label] = value;
        }

        public decimal GetTotalDecimal(string label)
        {
            return DecimalTotals != null && DecimalTotals.TryGetValue(label, out var v) ? v : 0m;
        }
    }

}
