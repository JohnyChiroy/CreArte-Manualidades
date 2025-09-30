using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ------------------------------------------------------
    // ViewModel de LISTADO con filtros/orden/paginación
    // (Se usa en GET /Proveedores/Index)
    // ------------------------------------------------------
    public class ProveedorViewModels
    {
        public List<PROVEEDOR> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }   // texto libre (id, nombre completo, NIT, DPI, empresa)
        public string? Empresa { get; set; }  // texto
        public bool? Estado { get; set; }     // true/false/null

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
    // ViewModel de CREATE/EDIT (incluye campos de PERSONA)
    // (Rutas: GET/POST /Proveedores/Create y GET/POST /Proveedores/Edit/{id})
    // ------------------------------------------------------
    public class ProveedorCreateVM
    {
        // ===== IDs =====
        [Display(Name = "ID Proveedor ( = Persona )")]
        [Required, StringLength(10)]
        public string PROVEEDOR_ID { get; set; } = default!;

        [Display(Name = "ID Persona")]
        [Required, StringLength(10)]
        public string PERSONA_ID { get; set; } = default!;

        // ===== PERSONA (OBLIGATORIOS marcados con [Required]) =====
        [Required, Display(Name = "Primer Nombre"), StringLength(50)]
        public string? PERSONA_PRIMERNOMBRE { get; set; }

        [Display(Name = "Segundo Nombre"), StringLength(50)]
        public string? PERSONA_SEGUNDONOMBRE { get; set; }

        [Display(Name = "Tercer Nombre"), StringLength(50)]
        public string? PERSONA_TERCERNOMBRE { get; set; }

        [Required, Display(Name = "Primer Apellido"), StringLength(50)]
        public string? PERSONA_PRIMERAPELLIDO { get; set; }

        [Display(Name = "Segundo Apellido"), StringLength(50)]
        public string? PERSONA_SEGUNDOAPELLIDO { get; set; }

        [Display(Name = "Apellido de Casada"), StringLength(50)]
        public string? PERSONA_APELLIDOCASADA { get; set; }

        [Display(Name = "NIT"), StringLength(15)]
        public string? PERSONA_NIT { get; set; }

        [Required, Display(Name = "CUI / DPI"), StringLength(13)]
        public string? PERSONA_CUI { get; set; }

        [Required, Display(Name = "Dirección"), StringLength(255)]
        public string? PERSONA_DIRECCION { get; set; }

        [Display(Name = "Teléfono (Casa)"), StringLength(15)]
        public string? PERSONA_TELEFONOCASA { get; set; }

        [Required, Display(Name = "Teléfono (Móvil)"), StringLength(15)]
        public string? PERSONA_TELEFONOMOVIL { get; set; }

        [Required, Display(Name = "Correo"), EmailAddress, StringLength(150)]
        public string? PERSONA_CORREO { get; set; }

        // ===== PROVEEDOR =====
        [Display(Name = "Empresa"), StringLength(100)]
        public string? EMPRESA { get; set; }

        [Display(Name = "Observación"), StringLength(250)]
        public string? PROVEEDOR_OBSERVACION { get; set; }

        // Estado general (se refleja en PERSONA y PROVEEDOR)
        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;
    }

    // ------------------------------------------------------
    // ViewModel para DETAILS (tarjeta/modal)
    // (Ruta: GET /Proveedores/DetailsCard?id=...)
    // ------------------------------------------------------
    public class ProveedorDetailsVM
    {
        public string PROVEEDOR_ID { get; set; } = default!;
        public string PERSONA_ID { get; set; } = default!;

        // Mapeos prácticos para la tarjeta
        public string? Nombres { get; set; }     // Nombre completo (6 partes)
        public string? DPI { get; set; }         // PERSONA_CUI
        public string? Telefono { get; set; }    // PERSONA_TELEFONOMOVIL
        public string? TelefonoCasa { get; set; }// PERSONA_TELEFONOCASA
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public string? NIT { get; set; }

        // PROVEEDOR
        public string? EMPRESA { get; set; }
        public string? PROVEEDOR_OBSERVACION { get; set; }
        public bool ESTADO { get; set; }

        // Auditoría
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
