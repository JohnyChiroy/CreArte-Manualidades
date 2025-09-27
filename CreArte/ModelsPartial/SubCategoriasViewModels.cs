// ===============================================
// RUTA: ModelsPartial/SubCategoriaViewModels.cs
// DESCRIPCIÓN: VMs para listar, crear/editar y detallar
//              SUBCATEGORÍA (con nombre de CATEGORÍA).
// ===============================================
using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ----------------- LISTADO -------------------
    public class SubCategoriaViewModels
    {
        public List<SUBCATEGORIA> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }     // ID/Nombre/Desc/Categoría
        public string? Nombre { get; set; }    
        public string? Categoria { get; set; }
        public bool? Estado { get; set; }       // true/false/null

        // Orden
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc";

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    // ------------- CREATE / EDIT -----------------
    public class SubCategoriaCreateVM
    {
        [Display(Name = "ID Subcategoría")]
        [Required, StringLength(10)]
        public string SUBCATEGORIA_ID { get; set; } = default!;

        [Required, Display(Name = "Nombre"), StringLength(100)]
        public string? SUBCATEGORIA_NOMBRE { get; set; }

        [Display(Name = "Descripción"), StringLength(255)]
        public string? SUBCATEGORIA_DESCRIPCION { get; set; }

        [Required, Display(Name = "Categoría"), StringLength(10)]
        public string? CATEGORIA_ID { get; set; }

        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;

        // Combos
        public List<SelectListItem> Categorias { get; set; } = new();
    }

    // ----------------- DETAILS -------------------
    public class SubCategoriaDetailsVM
    {
        public string SUBCATEGORIA_ID { get; set; } = default!;
        public string SUBCATEGORIA_NOMBRE { get; set; } = default!;
        public string? SUBCATEGORIA_DESCRIPCION { get; set; }
        public string CATEGORIA_ID { get; set; } = default!;
        public string CATEGORIA_NOMBRE { get; set; } = default!;
        public bool ESTADO { get; set; }

        // Auditoría
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
