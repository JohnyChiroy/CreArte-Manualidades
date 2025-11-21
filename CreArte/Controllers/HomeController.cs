using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System;
using System.Linq;

namespace CreArte.Controllers
{
    public class HomeController : Controller
    {
        private readonly CreArteDbContext _db;

        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger, CreArteDbContext db)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        [Route("Home/CalendarioEventos")]
        public IActionResult CalendarioEventos(DateTime? start, DateTime? end, string? type)
        {
            // 1) Rango base (DateTime) que llega de FullCalendar
            var desde = start ?? DateTime.Today.AddDays(-15);
            var hasta = end ?? DateTime.Today.AddDays(45);

            // 2) Conversión a DateOnly para poder comparar con columnas DateOnly?
            var desdeOnly = DateOnly.FromDateTime(desde.Date);
            var hastaOnly = DateOnly.FromDateTime(hasta.Date);

            var filtro = (type ?? "").Trim().ToUpperInvariant();
            var eventos = new List<CalendarEventDto>();

            // =================== PEDIDOS (FECHA_ENTREGA_PEDIDO es DateOnly?) ===================
            if (string.IsNullOrEmpty(filtro) || filtro == "PEDIDO")
            {
                var pedidos = _db.PEDIDO
                    .Where(p => !p.ELIMINADO
                             && p.FECHA_ENTREGA_PEDIDO != null              // evita nulls
                             && p.FECHA_ENTREGA_PEDIDO >= desdeOnly
                             && p.FECHA_ENTREGA_PEDIDO < hastaOnly)
                    // Cambio a evaluación en memoria para poder usar ToDateTime sin errores de traducción
                    .AsEnumerable()
                    .Select(p => new CalendarEventDto
                    {
                        id = p.PEDIDO_ID,
                        title = "Pedido " + p.PEDIDO_ID,
                        // Convertimos el DateOnly? a DateTime a las 00:00 (FullCalendar feliz)
                        start = p.FECHA_ENTREGA_PEDIDO!.Value.ToDateTime(TimeOnly.MinValue),
                        color = "#BF265E",
                        url = Url.Action("Details", "Pedidos", new { id = p.PEDIDO_ID }),
                        type = "PEDIDO"
                    })
                    .ToList();

                eventos.AddRange(pedidos);
            }


            if (string.IsNullOrEmpty(filtro) || filtro == "COMPRA")
            {
                var compras = _db.COMPRA
                    .Where(c => !c.ELIMINADO
                             && c.FECHA_COMPRA >= desde
                             && c.FECHA_COMPRA < hasta)
                    .Select(c => new CalendarEventDto
                    {
                        id = c.COMPRA_ID,
                        title = "Compra " + c.COMPRA_ID,
                        start = c.FECHA_COMPRA, // aquí asumo DateTime o DateTime?
                        color = "#EFBD21",
                        url = Url.Action("Details", "Compras", new { id = c.COMPRA_ID }),
                        type = "COMPRA"
                    })
                    .ToList();

                eventos.AddRange(compras);
            }

            return Json(eventos);
        }

        // Dashboard
        public IActionResult Index()
        {
            return View();
        }
    }

    //public class HomeController : Controller
    //{
    //    private readonly ILogger<HomeController> _logger;

    //    public HomeController(ILogger<HomeController> logger)
    //    {
    //        _logger = logger;
    //    }

    //    public IActionResult Index()
    //    {
    //        // Traemos de sesión el nombre que guardaste al hacer login
    //        ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Usuario";
    //        return View(); // Views/Home/Index.cshtml
    //    }

    //    public IActionResult Privacy()
    //    {
    //        return View();
    //    }

    //    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    //    public IActionResult Error()
    //    {
    //        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    //    }
    //}
}
