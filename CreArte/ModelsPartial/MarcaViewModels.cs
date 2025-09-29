// ===============================================
// RUTA: ModelsPartial/MarcaViewModels.cs
// DESCRIPCIÓN: VMs para listar, crear/editar y detallar
//              MARCA (Nombre, Descripción, Estado).
// ===============================================
using CreArte.Models;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ------------------------------------------------------
    // ViewModel de LISTADO con filtros/orden/paginación
    // (Se usa en GET /Marcas/Index)
    // ------------------------------------------------------
    public class MarcaViewModels
    {
        public List<MARCA> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }     // texto libre (id, nombre, descripción)
        public string? Nombre { get; set; }
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

    // ------------------------------------------------------
    // ViewModel de CREATE/EDIT
    // (Rutas: GET/POST /Categorias/Create y GET/POST /Categorias/Edit/{id})
    // ------------------------------------------------------
    public class MarcaCreateVM
    {
        [Display(Name = "ID Marca")]
        [Required, StringLength(10)]
        public string MARCA_ID { get; set; } = default!;

        [Required, Display(Name = "Nombre"), StringLength(100)]
        public string? MARCA_NOMBRE { get; set; }

        [Display(Name = "Descripción"), StringLength(255)]
        public string? MARCA_DESCRIPCION { get; set; }

        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;
    }

    // ------------------------------------------------------
    // ViewModel para DETAILS (tarjeta/modal)
    // (Ruta: GET /Categorias/DetailsCard?id=...)
    // ------------------------------------------------------
    public class MarcaDetailsVM
    {
        public string MARCA_ID { get; set; } = default!;
        public string MARCA_NOMBRE { get; set; } = default!;
        public string? MARCA_DESCRIPCION { get; set; }
        public bool ESTADO { get; set; }

        // Auditoría
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
