// ===============================================
// RUTA: ModelsPartial/NivelViewModels.cs
// DESCRIPCIÓN: ViewModels para el módulo de Niveles
// - NivelViewModels: listado + filtros/orden/paginación
// - NivelCreateVM: formulario Create/Edit
// - NivelDetailsVM: datos para modal Details
// ===============================================
using System.ComponentModel.DataAnnotations;
using CreArte.Models;
using System;
using System.Collections.Generic;

namespace CreArte.ModelsPartial
{
    /// <summary>
    /// ViewModel para el Index (listado de niveles) con filtros/orden/paginación.
    /// </summary>
    public class NivelViewModels
    {
        // Lista de ítems (se llena en el controlador con IQueryable materializado)
        public List<NIVEL> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }        // Busca en ID/Nombre
        public string? Nivel { get; set; }         // "__BLANKS__", "__NONBLANKS__" o texto
        public DateTime? FechaInicio { get; set; } // Rango por FECHA_CREACION
        public DateTime? FechaFin { get; set; }
        public bool? Estado { get; set; }          // true/false/null

        // Orden
        public string? Sort { get; set; } = "id";  // "id","nivel","fecha","estado"
        public string? Dir { get; set; } = "asc"; // "asc" | "desc"

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// VM para formularios Create/Edit de Nivel.
    /// </summary>
    public class NivelCreateVM
    {
        [Required, Display(Name = "Código")]
        [StringLength(10, ErrorMessage = "Máximo 10 caracteres.")]
        public string NIVEL_ID { get; set; } = default!;

        [Required, Display(Name = "Nombre del nivel")]
        [StringLength(30, ErrorMessage = "Máximo 30 caracteres.")]
        public string NIVEL_NOMBRE { get; set; } = default!;

        [Display(Name = "Descripción")]
        [StringLength(30, ErrorMessage = "Máximo 30 caracteres.")]
        public string? NIVEL_DESCRIPCION { get; set; }

        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;
    }

    /// <summary>
    /// VM para mostrar detalles (modal) con datos de auditoría.
    /// </summary>
    public class NivelDetailsVM
    {
        public string NIVEL_ID { get; set; } = default!;
        public string NIVEL_NOMBRE { get; set; } = default!;
        public string? NIVEL_DESCRIPCION { get; set; }
        public bool ESTADO { get; set; }

        // Auditoría
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
