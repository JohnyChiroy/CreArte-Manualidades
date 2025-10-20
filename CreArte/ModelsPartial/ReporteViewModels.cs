using System;
using System.Collections.Generic;

namespace CreArte.ModelsPartial
{
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

        /// <summary>Diccionario de totales por etiqueta (ej. "Activos" -> 10)</summary>
        public Dictionary<string, int> Totals { get; set; } = new();

        // -----------------------
        // Helpers
        // -----------------------
        public void AddTotal(string label, int value)
        {
            if (string.IsNullOrWhiteSpace(label)) return;
            Totals[label] = value;
        }

        public int GetTotal(string label)
        {
            return Totals != null && Totals.TryGetValue(label, out var v) ? v : 0;
        }
    }
}
