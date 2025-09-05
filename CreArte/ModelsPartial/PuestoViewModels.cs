using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CreArte.ModelsPartial
{
    public class PuestoViewModels
    {
        [Required, Display(Name = "ID Puesto")]
        [StringLength(10, ErrorMessage = "Máximo 10 caracteres.")]
        public string Id_Puesto { get; set; } = default!;

        [Required, Display(Name = "Nombre del Puesto")]
        [StringLength(80)]
        public string Nombre_Puesto { get; set; } = default!;

        [Display(Name = "Descripción")]
        [StringLength(250)]
        public string? Descripcion_Puesto { get; set; }

        [Required, Display(Name = "Área")]
        public string Id_Area { get; set; } = default!;

        [Display(Name = "Área")]
        public string? Area_Nombre { get; set; }

        [Required, Display(Name = "Nivel")]
        public string Id_Nivel { get; set; } = default!;

        [Display(Name = "Nivel")]
        public string? Nivel_Nombre { get; set; }

        [Display(Name = "Estado")]
        public bool Estado { get; set; } = true;

        // Campos de auditoría: los llenará el controlador
        public string Usuario_Create { get; set; } = default!;
        public DateTime Fecha_Create { get; set; }
        public string? Usuario_Modify { get; set; }
        public DateTime? Fecha_Modify { get; set; }

        // Filtros
        public string? Search { get; set; }      // búsqueda global (ID/Nombre)
        public string? Puesto { get; set; }     // filtro del popover de USUARIO (o "__BLANKS__" / "__NONBLANKS__")
        public string? Area { get; set; }     // filtro del popover de USUARIO (o "__BLANKS__" / "__NONBLANKS__")
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Nivel { get; set; }         // nombre o ID
        public bool? Estado_Filtro { get; set; }

        // Ordenamiento
        public string? Sort { get; set; } = "id"; // "id","Puesto","fecha","Area","Nivel","estado"
        public string? Dir { get; set; } = "asc";  // "asc" | "desc"

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Combos:
        public List<SelectListItem> Areas { get; set; } = new();
        public List<SelectListItem> Niveles { get; set; } = new();
    
    }
}

