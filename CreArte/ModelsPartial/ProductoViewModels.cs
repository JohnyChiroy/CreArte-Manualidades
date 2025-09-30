using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ------------------------------------------------------
    // ViewModel de LISTADO con filtros/orden/paginación
    // (Se usa en GET /Productos/Index)
    // ------------------------------------------------------
    public class ProductoViewModels
    {
        public List<PRODUCTO> Items { get; set; } = new();

        // Filtros
        public string? Search { get; set; }          // id, nombre, subcategoría, marca
        public string? SubCategoria { get; set; }    // texto por nombre
        public string? Tipo { get; set; }            // texto por nombre
        public string? Marca { get; set; }           // texto por nombre
        public string? Unidad { get; set; }          // texto por nombre
        public string? Empaque { get; set; }         // texto por nombre
        public bool? Estado { get; set; }            // true/false/null

        // Orden
        public string? Sort { get; set; } = "id";
        public string? Dir { get; set; } = "asc";

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        // Combos para filtros (opcional en la vista)
        public List<SelectListItem> SubCategorias { get; set; } = new();
        public List<SelectListItem> TiposProducto { get; set; } = new();
        public List<SelectListItem> Unidades { get; set; } = new();
        public List<SelectListItem> Empaques { get; set; } = new();
        public List<SelectListItem> Marcas { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel de CREATE/EDIT
    // (Rutas: GET/POST /Productos/Create y GET/POST /Productos/Edit/{id})
    // Incluye el archivo de imagen por fuera (IFormFile en Controller),
    // aquí solo guardamos la URL relativa en DB si se sube.
    // ------------------------------------------------------
    public class ProductoCreateVM
    {
        // ===== IDs =====
        [Display(Name = "ID Producto")]
        [Required, StringLength(10)]
        public string PRODUCTO_ID { get; set; } = default!;

        // ===== Campos principales =====
        [Required, Display(Name = "Nombre"), StringLength(100)]
        public string? PRODUCTO_NOMBRE { get; set; }

        [Display(Name = "Descripción"), StringLength(250)]
        public string? PRODUCTO_DESCRIPCION { get; set; }

        [Required, Display(Name = "Subcategoría"), StringLength(10)]
        public string? SUBCATEGORIA_ID { get; set; }

        [Required, Display(Name = "Tipo de Producto"), StringLength(10)]
        public string? TIPO_PRODUCTO_ID { get; set; }

        [Required, Display(Name = "Unidad de Medida"), StringLength(10)]
        public string? UNIDAD_MEDIDA_ID { get; set; }

        [Display(Name = "Tipo de Empaque"), StringLength(10)]
        public string? TIPO_EMPAQUE_ID { get; set; }

        [Display(Name = "Marca"), StringLength(10)]
        public string? MARCA_ID { get; set; }

        [Display(Name = "Imagen (URL Relativa)"), StringLength(500)]
        public string? IMAGEN_PRODUCTO { get; set; } // Ej: /uploads/productos/archivo.png

        [Display(Name = "IVA (%)")]
        public decimal? PORCENTAJE_IVA { get; set; } // 0..100

        [Display(Name = "Estado")]
        public bool ESTADO { get; set; } = true;

        // ===== Combos =====
        public List<SelectListItem> SubCategorias { get; set; } = new();
        public List<SelectListItem> TiposProducto { get; set; } = new();
        public List<SelectListItem> Unidades { get; set; } = new();
        public List<SelectListItem> Empaques { get; set; } = new();
        public List<SelectListItem> Marcas { get; set; } = new();
    }

    // ------------------------------------------------------
    // ViewModel para DETAILS (tarjeta/modal)
    // (Ruta: GET /Productos/DetailsCard?id=...)
    // ------------------------------------------------------
    public class ProductoDetailsVM
    {
        public string PRODUCTO_ID { get; set; } = default!;
        public string PRODUCTO_NOMBRE { get; set; } = default!;
        public string? PRODUCTO_DESCRIPCION { get; set; }

        public string SUBCATEGORIA_ID { get; set; } = default!;
        public string SUBCATEGORIA_NOMBRE { get; set; } = default!;

        public string TIPO_PRODUCTO_ID { get; set; } = default!;
        public string TIPO_PRODUCTO_NOMBRE { get; set; } = default!;

        public string UNIDAD_MEDIDA_ID { get; set; } = default!;
        public string UNIDAD_MEDIDA_NOMBRE { get; set; } = default!;

        public string? TIPO_EMPAQUE_ID { get; set; }
        public string? TIPO_EMPAQUE_NOMBRE { get; set; }

        public string? MARCA_ID { get; set; }
        public string? MARCA_NOMBRE { get; set; }

        public string? IMAGEN_PRODUCTO { get; set; }
        public decimal? PORCENTAJE_IVA { get; set; }
        public bool ESTADO { get; set; }

        // Auditoría
        public string USUARIO_CREACION { get; set; } = default!;
        public DateTime FECHA_CREACION { get; set; }
        public string? USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }
    }
}
