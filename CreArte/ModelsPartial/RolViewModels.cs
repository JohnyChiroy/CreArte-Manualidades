// ===============================================
// RUTA: ModelsPartial/RolViewModels.cs
// DESCRIPCIÓN: VMs para listar, crear/editar y detallar ROL.
//              ▸ En este módulo no hay DateOnly en entidad ni
//                conversiones de fecha como en EMPLEADO.
// ===============================================
using CreArte.Models;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ------------------------------------------------------
    // ViewModel de LISTADO con filtros/orden/paginación
    // (Se usa en GET /Roles/Index) — MISMO PATRÓN
    // ------------------------------------------------------
    public class RolViewModels
    {
        public List<ROL> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; } // id/nombre
        public bool? Estado { get; set; }   // true/false/null

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
    // (Rutas: GET/POST /Roles/Create y GET/POST /Roles/Edit/{id})
    // ------------------------------------------------------
    public class RolCreateVM
    {
        [Display(Name = "ID Rol")]
        [Required, StringLength(10)]
        public string ROL_ID { get; set; } = default!;

        [Display(Name = "Nombre del Rol")]
        [Required, StringLength(100)]
        public string? ROL_NOMBRE { get; set; }

        [Display(Name = "Descripción")]
        [StringLength(250)]
        public string? ROL_DESCRIPCION { get; set; }

        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;
    }

    // ------------------------------------------------------
    // ViewModel para DETAILS (tarjeta/modal)
    // (Ruta: GET /Roles/DetailsCard?id=...)
    // ------------------------------------------------------
    public class RolDetailsVM
    {
        public string ROL_ID { get; set; } = default!;
        public string ROL_NOMBRE { get; set; } = default!;
        public string? ROL_DESCRIPCION { get; set; }
        public bool ESTADO { get; set; }

        // Auditoría (por si quieres mostrarla en la tarjeta)
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
