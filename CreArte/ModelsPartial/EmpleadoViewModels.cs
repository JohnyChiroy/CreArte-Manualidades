using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ------------------------------------------------------
    // ViewModel de LISTADO con filtros/orden/paginación
    // (Se usa en GET /Empleados/Index) — NO TOCAR
    // ------------------------------------------------------
    public class EmpleadoViewModels
    {
        public List<EMPLEADO> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }            // texto libre (id, nombre, apellido, puesto)
        public string? Puesto { get; set; }            // texto o marcadores "__BLANKS__"/"__NONBLANKS__"
        public string? Genero { get; set; }            // Femenino/Masculino/...
        public DateTime? FechaIngresoIni { get; set; } // DateTime? para inputs <input type="date">
        public DateTime? FechaIngresoFin { get; set; }
        public bool? Estado { get; set; }              // true/false/null

        // Orden
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc";

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Combos
        public List<SelectListItem> Puestos { get; set; } = new();
        public List<SelectListItem> Generos { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel de CREATE/EDIT (incluye campos de PERSONA)
    // (Rutas: GET/POST /Empleados/Create y GET/POST /Empleados/Edit/{id})
    // ------------------------------------------------------
    public class EmpleadoCreateVM
    {
        // ===== IDs =====
        [Display(Name = "ID Empleado ( = Persona )")]
        [Required, StringLength(10)]
        public string EMPLEADO_ID { get; set; } = default!;

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

        // Estado general (se refleja en PERSONA y EMPLEADO)
        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;

        // ===== EMPLEADO (OBLIGATORIOS marcados con [Required]) =====
        [Required, Display(Name = "Fecha de Nacimiento")]
        public DateTime? EMPLEADO_FECHANACIMIENTO { get; set; }

        [Required, Display(Name = "Fecha de Ingreso")]
        public DateTime? EMPLEADO_FECHAINGRESO { get; set; }

        [Required, Display(Name = "Género"), StringLength(10)]
        public string? EMPLEADO_GENERO { get; set; }

        [Required, Display(Name = "Puesto"), StringLength(10)]
        public string? PUESTO_ID { get; set; }

        // ===== Combos =====
        public List<SelectListItem> Puestos { get; set; } = new();
        public List<SelectListItem> Generos { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel para DETAILS (tarjeta/modal)
    // (Ruta: GET /Empleados/DetailsCard?id=...)
    // ------------------------------------------------------
    public class EmpleadoDetailsVM
    {
        public string EMPLEADO_ID { get; set; } = default!;
        public string PERSONA_ID { get; set; } = default!;

        // Compatibles con Controller actual
        public string? Nombres { get; set; }   // PERSONA_PRIMERNOMBRE
        public string? Apellidos { get; set; } // PERSONA_PRIMERAPELLIDO
        public string? DPI { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public string? NIT { get; set; }
        public string? TelefonoCasa { get; set; }

        // (Opcionales) completos de PERSONA por si luego se muestran
        public string? PERSONA_PRIMERNOMBRE { get; set; }
        public string? PERSONA_SEGUNDONOMBRE { get; set; }
        public string? PERSONA_TERCERNOMBRE { get; set; }
        public string? PERSONA_PRIMERAPELLIDO { get; set; }
        public string? PERSONA_SEGUNDOAPELLIDO { get; set; }
        public string? PERSONA_APELLIDOCASADA { get; set; }
        public string? PERSONA_NIT { get; set; }
        public string? PERSONA_CUI { get; set; }
        public string? PERSONA_TELEFONOCASA { get; set; }
        public string? PERSONA_TELEFONOMOVIL { get; set; }
        public string? PERSONA_CORREO { get; set; }
        public string? PERSONA_DIRECCION { get; set; }

        // EMPLEADO (en entidad son DateOnly/DateOnly?)
        public DateOnly? EMPLEADO_FECHANACIMIENTO { get; set; }
        public DateOnly EMPLEADO_FECHAINGRESO { get; set; }
        public string? EMPLEADO_GENERO { get; set; }
        public string PUESTO_ID { get; set; } = default!;
        public string PUESTO_NOMBRE { get; set; } = default!;
        public bool ESTADO { get; set; }

        // Auditoría
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
