// ===============================================
// RUTA: ModelsPartial/PuestoViewModels.cs
// DESCRIPCIÓN: ViewModels para listado, creación/edición y detalles de PUESTO
// ===============================================
using CreArte.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CreArte.ModelsPartial
{
    // VM para el listado con filtros, orden y paginación
    public class PuestoViewModels
    {
        // ---------- Filtros ----------
        public string? Search { get; set; }     // búsqueda global: PUESTO_ID, PUESTO_NOMBRE, AREA.AREA_NOMBRE
        public string? Puesto { get; set; }     // filtro de columna PUESTO ("__BLANKS__", "__NONBLANKS__", o texto)
        public string? Area { get; set; }       // filtro de columna ÁREA ("__BLANKS__", "__NONBLANKS__", o texto)
        public string? Nivel { get; set; }      // filtro de columna NIVEL ("__BLANKS__", "__NONBLANKS__", o texto)
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool? Estado { get; set; }       // true/false/null

        // ---------- Orden ----------
        // Permitidos: "id","puesto","area","fecha","estado"
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc";   // "asc" | "desc"

        // ---------- Paginación ----------
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // ---------- Datos ----------
        public List<PUESTO> Items { get; set; } = new();
    }

    // VM para Create/Edit
    public class PuestoCreateVM
    {
        [Display(Name = "Código de Puesto")]
        public string PUESTO_ID { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del puesto es obligatorio.")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        [Display(Name = "Nombre del puesto")]
        public string PUESTO_NOMBRE { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Máximo 255 caracteres.")]
        [Display(Name = "Descripción (opcional)")]
        public string? PUESTO_DESCRIPCION { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un área.")]
        [Display(Name = "Área")]
        public string AREA_ID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar un nivel.")]
        [Display(Name = "Nivel")]
        public string NIVEL_ID { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool ESTADO { get; set; } = true;

        // Para selects
        public IEnumerable<SelectListItem> Areas { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Niveles { get; set; } = new List<SelectListItem>();
    }

    // VM para Details (tarjeta/partial)
    public class PuestoDetailsVM
    {
        public string PUESTO_ID { get; set; } = string.Empty;
        public string PUESTO_NOMBRE { get; set; } = string.Empty;
        public string? PUESTO_DESCRIPCION { get; set; }

        public string AREA_ID { get; set; } = string.Empty;
        public string AREA_NOMBRE { get; set; } = string.Empty;

        public string NIVEL_ID { get; set; } = string.Empty;
        public string NIVEL_NOMBRE { get; set; } = string.Empty;

        public bool ESTADO { get; set; }

        public DateTime FECHA_CREACION { get; set; }
        public string USUARIO_CREACION { get; set; } = string.Empty;
        public DateTime? FECHA_MODIFICACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
    }
}
