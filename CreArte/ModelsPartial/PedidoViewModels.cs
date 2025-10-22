using Microsoft.AspNetCore.Mvc;

namespace CreArte.ModelsPartial
{
    public class PedidoViewModels : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
