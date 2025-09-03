using CreArte.Models;
using System;
using System.Collections.Generic;

namespace CreArte.ModelsPartial
{
    public class UsuarioListaViewModels
    {
        // Filtros
        public string? Search { get; set; }      // búsqueda global (ID/Nombre)
        public string? Usuario { get; set; }     // filtro del popover de USUARIO (o "__BLANKS__" / "__NONBLANKS__")
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string? Rol { get; set; }         // nombre o ID
        public bool? Estado { get; set; }

        // Ordenamiento
        public string? Sort { get; set; } = "id"; // "id","usuario","fecha","rol","estado"
        public string? Dir { get; set; } = "asc";  // "asc" | "desc"

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Datos
        public List<USUARIO> Items { get; set; } = new();
    }
}
