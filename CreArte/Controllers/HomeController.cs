using System.Diagnostics;
using CreArte.Models;
using Microsoft.AspNetCore.Mvc;

namespace CreArte.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Traemos de sesión el nombre que guardaste al hacer login
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Usuario";
            return View(); // Views/Home/Index.cshtml
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
