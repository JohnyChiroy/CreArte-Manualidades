// ModelsPartial/PermisosModuloVM.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CreArte.ModelsPartial
{
    // Para Index (resumen por Rol)
    public class PermisosIndexVM
    {
        public List<PermisoListVM> Items { get; set; } = new();
        public string? Q { get; set; }
        // Estado como string "true"/"false"/null (UI); en Controller se convierte a bool con TryParse
        public string? Estado { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public string? Sort { get; set; }
        public string? Dir { get; set; }
    }

    public class PermisoListVM
    {
        public string RolId { get; set; } = "";
        public string RolNombre { get; set; } = "";
        public string PermisosId { get; set; } = "";
        public DateTime? FechaCreacion { get; set; }
    }

    // Ítem por módulo
    public class ModuloPermItemVM
    {
        public string ModuloId { get; set; } = "";
        public string ModuloNombre { get; set; } = "";

        public bool Ver { get; set; }
        public bool Crear { get; set; }
        public bool Editar { get; set; }
        public bool Eliminar { get; set; }

        public bool YaAsignado { get; set; } // existe en BD
        public bool Touched { get; set; }    // para Edit: si cambió algo
    }

    // VM para Create/Edit masivo
    public class PermisosModuloBulkVM
    {
        [Required] public string RolId { get; set; } = "";
        public List<SelectListItem> Roles { get; set; } = new();
        public List<ModuloPermItemVM> Modulos { get; set; } = new();
    }
}
