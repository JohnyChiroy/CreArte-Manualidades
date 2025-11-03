using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CreArte.ModelsPartial
{
    public class CajaViewModels : Controller
    {
        // GET: CajaViewModels
        public ActionResult Index()
        {
            return View();
        }

        // GET: CajaViewModels/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: CajaViewModels/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: CajaViewModels/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: CajaViewModels/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: CajaViewModels/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: CajaViewModels/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: CajaViewModels/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
