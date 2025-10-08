using CreArte.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    public class CompraLineaVM
    {
        [Required] public string ProductoId { get; set; } = default!;
        [Required][Range(0, int.MaxValue)] public int Cantidad { get; set; }
        // Precio puede ser null en estados tempranos; será obligatorio al confirmar
        [Range(0, double.MaxValue)] public decimal? PrecioCompra { get; set; }
        //public DateTime? FechaVencimiento { get; set; }
        public DateOnly? FechaVencimiento { get; set; }

        // Campo para Recibir (se usa en la pantalla de RECIBIDA)
        [Range(0, int.MaxValue)] public int? CantidadRecibida { get; set; }
    }
    //public class CompraLineaVM
    //{
    //    public string ProductoId { get; set; } = default!;
    //    public int Cantidad { get; set; }                   // Pedida
    //    public decimal? PrecioCompra { get; set; }          // Confirmada (obligatoria antes de REC)
    //    public int? CantidadRecibida { get; set; }          // Se captura en RECIBIR (>=0 y <= Cantidad)
    //    public DateOnly? FechaVencimiento { get; set; }     // Referencial
    //}
    public class CompraCreateVM
    {
        [Required] public string CompraId { get; set; } = default!;
        [Required] public string ProveedorId { get; set; } = default!;
        public string? Observaciones { get; set; }

        // Líneas iniciales
        [MinLength(1, ErrorMessage = "Agrega al menos un producto")]
        public List<CompraLineaVM> Lineas { get; set; } = new();
    }

    public class CompraConfirmarVM
    {
        [Required] public string CompraId { get; set; } = default!;
        // Obligatorio al confirmar
        //[Required] public DateTime FechaEntregaCompra { get; set; }
        [Required] public DateOnly FechaEntregaCompra { get; set; }

        // Todas las líneas deben traer PrecioCompra definido y >= 0
        public List<CompraLineaVM> Lineas { get; set; } = new();
    }

    //public class CompraRecibirVM
    //{
    //    [Required] public string CompraId { get; set; } = default!;
    //    // En esta pantalla capturas CantidadRecibida por línea
    //    public List<CompraLineaVM> Lineas { get; set; } = new();
    //}

    public class CompraDetailsVM
    {
        public string CompraId { get; set; } = default!;
        public string EstadoId { get; set; } = default!;
        public string EstadoNombre { get; set; } = default!;
        public string ProveedorId { get; set; } = default!;
        public string ProveedorNombre { get; set; } = default!;
        public DateTime FechaCompra { get; set; }
        //public DateTime? FechaEntregaCompra { get; set; }
        public DateOnly? FechaEntregaCompra { get; set; }
        public bool CargadaInventario { get; set; }
        public DateTime? FechaCarga { get; set; }
        public string? Observaciones { get; set; }

        public List<CompraLineaVM> Lineas { get; set; } = new();

        // Flags para mostrar/ocultar botones en la UI
        public bool PuedeAgregarEliminar { get; set; }
        public bool PuedeEditarPrecio { get; set; }
        public bool PuedeMarcarRecibida { get; set; }
        public bool PuedeCargarInventario { get; set; }
        public bool PuedeAnular { get; set; }
    }

    // VM para cada fila del listado de Compras (Index)
    public class CompraIndexRowVM
    {
        public string COMPRA_ID { get; set; } = default!;
        public string PROVEEDOR_ID { get; set; } = default!;
        public DateTime FECHA_COMPRA { get; set; }        // DATETIME en SQL
        public DateOnly? FECHA_ENTREGA_COMPRA { get; set; }

        public string ESTADO_COMPRA_ID { get; set; } = default!;
        public bool CARGADA_INVENTARIO { get; set; }
        public string? PROVEEDOR_NOMBRE { get; set; }
    }

    public class ComprasIndexVM
    {
        // ====== LISTA ======
        public List<CompraIndexRowVM> Items { get; set; } = new();

        // ====== FILTROS ======
        // Búsqueda global: COMPRA_ID, PROVEEDOR_ID o nombre del proveedor
        public string? Search { get; set; }

        // Estado: "BOR", "REV", "APR", "ENV", "CON", "REC", "CER", "ANU"
        public string? Estado { get; set; }
        //public List<SelectListItem> EstadosList { get; set; } = new()
        //{
        //    new SelectListItem("Borrador", "BOR"),
        //    new SelectListItem("Revisada", "REV"),
        //    new SelectListItem("Aprobada", "APR"),
        //    new SelectListItem("Enviada", "ENV"),
        //    new SelectListItem("Confirmada", "CON"),
        //    new SelectListItem("Recibida", "REC"),
        //    new SelectListItem("Cerrada", "CER"),
        //    new SelectListItem("Anulada", "ANU"),
        //};

        // ====== ORDEN ======
        // Columnas soportadas: "id", "proveedor", "fecha", "estado", "inv"
        public string? Sort { get; set; } = "fecha";
        // Dirección: "asc" o "desc"
        public string? Dir { get; set; } = "desc";

        // ====== PAGINACIÓN ======
        public int Page { get; set; } = 1;       // Página actual (1..N)
        public int PageSize { get; set; } = 10;  // Tamaño de página
        public int TotalPages { get; set; } = 1; // Cantidad total de páginas
        public int TotalCount { get; set; } = 0; // Total de registros sin paginar
    }
    // ===== Recibir: VM de pantalla =====
    // Se usa para capturar la CANTIDAD_RECIBIDA por renglón
    public class CompraRecibirVM
    {
        // Id de la compra que vamos a recibir
        public string CompraId { get; set; } = default!;

        // Renglones a recibir (ProductoId, Cantidad pedida, PrecioCompra, CantidadRecibida)
        public List<CompraLineaVM> Lineas { get; set; } = new();
    }

    // ===== Línea de compra (reutilizada en varias pantallas) =====
    // OJO: aquí no se edita Cantidad ni ProductoId en Recibir; solo CantidadRecibida


    public class CompraEditVM
    {
        public string CompraId { get; set; } = default!;

        // Ahora editable
        public string? ProveedorId { get; set; }       // <- NUEVO: editable
        public string? ProveedorNombre { get; set; }   // solo para mostrar si quieres

        public string? Observaciones { get; set; }

        public List<CompraLineaEditVM> Lineas { get; set; } = new();
    }


    // Línea editable en EDIT (mapea con el detalle si ya existía)
    public class CompraLineaEditVM
    {
        public string? DetalleCompraId { get; set; } // null => es una línea nueva
        public string ProductoId { get; set; } = default!;
        public int Cantidad { get; set; }
    }


}