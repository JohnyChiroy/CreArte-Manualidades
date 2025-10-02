using Microsoft.AspNetCore.Mvc;

namespace CreArte.ModelsPartial
{
    public class CompraViewModels : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
