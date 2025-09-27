// ===============================================
// RUTA: ModelsPartial/UsuarioViewModels.cs
// DESCRIPCIÓN: ViewModels para el módulo de Usuarios
//  - UsuarioViewModels: listado con filtros/orden/paginación
//  - UsuarioCreateVM: Create/Edit con validaciones de UI
//  - UsuarioDetailsVM: para la tarjeta (modal) de detalles
// ===============================================
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using CreArte.Models;

namespace CreArte.ModelsPartial
{
    // ===================== LISTADO =====================
    public class UsuarioViewModels
    {
        // Filtros
        public string? Search { get; set; }       // global (ID/Usuario/Empleado/Correo/Rol)
        public DateTime? FechaInicio { get; set; } // USUARIO_FECHAREGISTRO >=
        public DateTime? FechaFin { get; set; }    // USUARIO_FECHAREGISTRO <=
        public string? Rol { get; set; }          // nombre o ID
        public bool? Estado { get; set; }

        // Ordenamiento
        public string? Sort { get; set; } = "id";  // "id","usuario","fecha","rol","estado"
        public string? Dir { get; set; } = "asc";  // "asc" | "desc"

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Datos
        public List<USUARIO> Items { get; set; } = new();
    }

    // ===================== CREATE / EDIT =====================
    public class UsuarioCreateVM
    {
        // Identificador
        public string? USUARIO_ID { get; set; }

        // Datos de usuario
        public string? USUARIO_NOMBRE { get; set; }

        // Passwords para UI (NO se guardan así en la DB)
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }

        public string? USUARIO_CORREO { get; set; }

        // Relaciones
        public string? ROL_ID { get; set; }
        public string? EMPLEADO_ID { get; set; }

        // Estado y política de cambio
        public bool USUARIO_CAMBIOINICIAL { get; set; } = true;
        public bool ESTADO { get; set; } = true;

        // Combos
        public List<SelectListItem> Roles { get; set; } = new();
        public List<SelectListItem> Empleados { get; set; } = new();
    }

    // ===================== DETAILS (modal) =====================
    public class UsuarioDetailsVM
    {
        public string USUARIO_ID { get; set; } = "";
        public string USUARIO_NOMBRE { get; set; } = "";
        public string? USUARIO_CORREO { get; set; }
        public DateTime USUARIO_FECHAREGISTRO { get; set; }
        public bool USUARIO_CAMBIOINICIAL { get; set; }
        public bool ESTADO { get; set; }

        // ROL
        public string ROL_ID { get; set; } = "";
        public string ROL_NOMBRE { get; set; } = "";

        // EMPLEADO
        public string EMPLEADO_ID { get; set; } = "";
        public string EMPLEADO_NOMBRE_COMPLETO { get; set; } = "";

        // Auditoría
        public DateTime FECHA_CREACION { get; set; }
        public string USUARIO_CREACION { get; set; } = "";
        public DateTime? FECHA_MODIFICACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
    }
}
