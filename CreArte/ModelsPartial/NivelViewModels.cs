// Ruta sugerida: CreArte/ModelsPartial/NivelViewModels.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    /// <summary>
    /// ViewModel unificado para Niveles:
    /// - Datos del catálogo Nivel
    /// - Filtros/orden/paginación para listarlo
    /// - Pensado para usarse también como combo dependiente en Puestos
    /// </summary>
    public class NivelViewModels
    {
        // -----------------------------
        // Datos de la entidad (Tabla)
        // -----------------------------
        [Required, Display(Name = "ID Nivel")]
        [StringLength(10, ErrorMessage = "Máximo 10 caracteres.")]
        public string Id_Nivel { get; set; } = default!;

        [Required, Display(Name = "Nombre del Nivel")]
        [StringLength(80)]
        public string Nivel_Nombre { get; set; } = default!;

        [Display(Name = "Descripción")]
        [StringLength(250)]
        public string? Descripcion_Nivel { get; set; }

        // Si manejas una prioridad/jerarquía numérica, descomenta:
        // [Display(Name = "Prioridad")]
        // public int? Prioridad { get; set; }

        [Display(Name = "Estado")]
        public bool Estado { get; set; } = true;

        // -----------------------------
        // Auditoría (llenado en Controller)
        // -----------------------------
        public string Usuario_Create { get; set; } = default!;
        public DateTime Fecha_Create { get; set; }
        public string? Usuario_Modify { get; set; }
        public DateTime? Fecha_Modify { get; set; }

        // -----------------------------
        // Filtros del Index
        // -----------------------------
        public string? Search { get; set; }        // busca en ID/Nombre
        public string? Nivel { get; set; }         // filtro específico por nombre (o "__BLANKS__"/"__NONBLANKS__")
        public DateTime? FechaInicio { get; set; } // fecha creación desde
        public DateTime? FechaFin { get; set; }    // fecha creación hasta
        public bool? Estado_Filtro { get; set; }   // true/false/null

        // -----------------------------
        // Ordenamiento
        // -----------------------------
        public string? Sort { get; set; } = "id";  // "id","Nivel","fecha","estado"
        public string? Dir { get; set; } = "asc";  // "asc" | "desc"

        // -----------------------------
        // Paginación
        // -----------------------------
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // -----------------------------
        // Combos (si necesitas alguno)
        // -----------------------------
        public List<SelectListItem> Estados { get; set; } = new(); // opcional
    }
}
