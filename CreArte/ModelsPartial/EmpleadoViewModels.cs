// ===============================================
// RUTA: ModelsPartial/EmpleadoViewModels.cs
// DESCRIPCIÓN: VMs para listar, crear/editar y detallar
//              EMPLEADO con campos de PERSONA incluidos.
// ===============================================
using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ------------------------------------------------------
    // ViewModel de LISTADO con filtros/orden/paginación
    // ------------------------------------------------------
    public class EmpleadoViewModels
    {
        // Lista de entidades (con PERSONA y PUESTO incluidos en el Controller)
        public List<EMPLEADO> Items { get; set; } = new();

        // -------- Filtros --------
        public string? Search { get; set; }            // ID, nombre, apellido, puesto
        public string? Puesto { get; set; }            // texto o marcadores "__BLANKS__"/"__NONBLANKS__"
        public string? Genero { get; set; }            // Femenino/Masculino/Otro
        public DateTime? FechaIngresoIni { get; set; } // rango fecha de ingreso
        public DateTime? FechaIngresoFin { get; set; }
        public bool? Estado { get; set; }              // true/false/null

        // -------- Orden --------
        public string? Sort { get; set; } = "id";      // "id","nombre","puesto","ingreso","estado","fecha"
        public string? Dir { get; set; } = "asc";      // "asc" | "desc"

        // -------- Paginación --------
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // -------- Combos --------
        public List<SelectListItem> Puestos { get; set; } = new();
        public List<SelectListItem> Generos { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel de CREATE/EDIT (incluye campos de PERSONA)
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

        // ===== PERSONA =====
        // TODO: Ajusta nombres de propiedades a tu entidad PERSONA
        [Required, Display(Name = "Nombres")]
        [StringLength(100)]
        public string? Nombres { get; set; }

        [Required, Display(Name = "Apellidos")]
        [StringLength(100)]
        public string? Apellidos { get; set; }

        [Display(Name = "DPI")]
        [StringLength(25)]
        public string? DPI { get; set; }

        [Display(Name = "Teléfono")]
        [StringLength(30)]
        public string? Telefono { get; set; }

        [Display(Name = "Correo")]
        [EmailAddress]
        [StringLength(120)]
        public string? Correo { get; set; }

        [Display(Name = "Dirección")]
        [StringLength(255)]
        public string? Direccion { get; set; }

        // ===== EMPLEADO =====
        // EmpleadoCreateVM
        [Display(Name = "Fecha de Nacimiento")]
        public DateOnly? EMPLEADO_FECHANACIMIENTO { get; set; }

        [Required, Display(Name = "Fecha de Ingreso")]
        public DateOnly? EMPLEADO_FECHAINGRESO { get; set; }


        [Display(Name = "Género")]
        public string? EMPLEADO_GENERO { get; set; } // si usas catálogo, cambia a ID

        [Required, Display(Name = "Puesto")]
        public string? PUESTO_ID { get; set; }

        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;

        // ===== Combos =====
        public List<SelectListItem> Puestos { get; set; } = new();
        public List<SelectListItem> Generos { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel para DETAILS (tarjeta/modal)
    // ------------------------------------------------------
    public class EmpleadoDetailsVM
    {
        public string EMPLEADO_ID { get; set; } = default!;
        public string PERSONA_ID { get; set; } = default!;

        // PERSONA (ajusta a tu esquema)
        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }
        public string? DPI { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }

        // EMPLEADO
        // EmpleadoDetailsVM
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
