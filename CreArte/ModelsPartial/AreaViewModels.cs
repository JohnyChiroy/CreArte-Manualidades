using CreArte.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CreArte.ModelsPartial
{
    public class AreaViewModels
    {
        // -----------------------
        // Filtros
        // -----------------------
        public string? Search { get; set; }      // búsqueda global (AREA_ID / AREA_NOMBRE)
        public string? Area { get; set; }        // filtro del popover de ÁREA ("__BLANKS__" / "__NONBLANKS__" o texto)
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool? Estado { get; set; }

        // -----------------------
        // Ordenamiento
        // -----------------------
        // valores permitidos: "id","area","fecha","estado"
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc";  // "asc" | "desc"

        // -----------------------
        // Paginación
        // -----------------------
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // -----------------------
        // Datos
        // -----------------------
        public List<AREA> Items { get; set; } = new();
    }
    public class AreaCreateVM
    {
        // Se mostrará en solo lectura (lo autogeneramos en servidor)
        [Display(Name = "Código de Área")]
        public string AREA_ID { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del área es obligatorio.")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        [Display(Name = "Nombre del área")]
        public string AREA_NOMBRE { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Máximo 255 caracteres.")]
        [Display(Name = "Descripción (opcional)")]
        public string? AREA_DESCRIPCION { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un nivel.")]
        [Display(Name = "Nivel")]
        public string NIVEL_ID { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool ESTADO { get; set; } = true;

        // Para poblar el <select>
        public IEnumerable<SelectListItem> Niveles { get; set; } = new List<SelectListItem>();
    }
    public class AreaDetailsVM
    {
        public string AREA_ID { get; set; } = string.Empty;
        public string AREA_NOMBRE { get; set; } = string.Empty;
        public string? AREA_DESCRIPCION { get; set; }
        public string NIVEL_ID { get; set; } = string.Empty;
        public string NIVEL_NOMBRE { get; set; } = string.Empty;
        public bool ESTADO { get; set; }
        public DateTime FECHA_CREACION { get; set; }
        public string USUARIO_CREACION { get; set; } = string.Empty;
        public DateTime? FECHA_MODIFICACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
    }
}