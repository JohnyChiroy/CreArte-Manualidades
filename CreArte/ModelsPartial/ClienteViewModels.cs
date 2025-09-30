using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ------------------------------------------------------
    // ViewModel de LISTADO con filtros/orden/paginación
    // (Se usa en GET /Clientes/Index) — NO TOCAR
    // ------------------------------------------------------
    public class ClienteViewModels
    {
        public List<CLIENTE> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }           // texto libre (id, nombre, nit, correo, teléfono, tipo)
        public string? TipoCliente { get; set; }      // texto o marcadores "__BLANKS__"/"__NONBLANKS__"
        public bool? Estado { get; set; }             // true/false/null

        // Orden
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc";

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Combos
        public List<SelectListItem> TiposCliente { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel de CREATE/EDIT (incluye campos de PERSONA)
    // (Rutas: GET/POST /Clientes/Create y GET/POST /Clientes/Edit/{id})
    // ------------------------------------------------------
    public class ClienteCreateVM
    {
        // ===== IDs =====
        [Display(Name = "ID Cliente ( = Persona )")]
        [Required, StringLength(10)]
        public string CLIENTE_ID { get; set; } = default!;

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

        // Estado general (se refleja en PERSONA y CLIENTE)
        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;

        // ===== CLIENTE =====
        [Display(Name = "Nota"), StringLength(250)]
        public string? CLIENTE_NOTA { get; set; }   // opcional

        [Required, Display(Name = "Tipo de Cliente"), StringLength(10)]
        public string? TIPO_CLIENTE_ID { get; set; }

        // ===== Combos =====
        public List<SelectListItem> TiposCliente { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel para DETAILS (tarjeta/modal)
    // (Ruta: GET /Clientes/DetailsCard?id=...)
    // ------------------------------------------------------
    public class ClienteDetailsVM
    {
        public string CLIENTE_ID { get; set; } = default!;
        public string PERSONA_ID { get; set; } = default!;

        // Resumen amigable
        public string? Nombres { get; set; }   // nombre completo (6 partes)
        public string? DPI { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public string? NIT { get; set; }
        public string? TelefonoCasa { get; set; }

        // CLIENTE
        public string? CLIENTE_NOTA { get; set; }
        public string TIPO_CLIENTE_ID { get; set; } = default!;
        public string TIPO_CLIENTE_NOMBRE { get; set; } = default!;
        public bool ESTADO { get; set; }

        // Auditoría
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
