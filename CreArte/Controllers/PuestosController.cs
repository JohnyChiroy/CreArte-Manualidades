using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CreArte.Data;
using CreArte.Models;

namespace CreArte.Controllers
{
    public class PuestosController : Controller
    {
        private readonly CreArteDbContext _context;

        public PuestosController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Puestos
        public async Task<IActionResult> Index()
        {
            var creArteDbContext = _context.PUESTO.Include(p => p.AREA).Include(p => p.NIVEL);
            return View(await creArteDbContext.ToListAsync());
        }

        // GET: Puestos/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pUESTO = await _context.PUESTO
                .Include(p => p.AREA)
                .Include(p => p.NIVEL)
                .FirstOrDefaultAsync(m => m.PUESTO_ID == id);
            if (pUESTO == null)
            {
                return NotFound();
            }

            return View(pUESTO);
        }

        // GET: Puestos/Create
        public IActionResult Create()
        {
            ViewData["AREA_ID"] = new SelectList(_context.AREA, "AREA_ID", "AREA_ID");
            ViewData["NIVEL_ID"] = new SelectList(_context.NIVEL, "NIVEL_ID", "NIVEL_ID");
            return View();
        }

        // POST: Puestos/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PUESTO_ID,PUESTO_NOMBRE,PUESTO_DESCRIPCION,AREA_ID,NIVEL_ID,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] PUESTO pUESTO)
        {
            if (ModelState.IsValid)
            {
                _context.Add(pUESTO);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AREA_ID"] = new SelectList(_context.AREA, "AREA_ID", "AREA_ID", pUESTO.AREA_ID);
            ViewData["NIVEL_ID"] = new SelectList(_context.NIVEL, "NIVEL_ID", "NIVEL_ID", pUESTO.NIVEL_ID);
            return View(pUESTO);
        }

        // GET: Puestos/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pUESTO = await _context.PUESTO.FindAsync(id);
            if (pUESTO == null)
            {
                return NotFound();
            }
            ViewData["AREA_ID"] = new SelectList(_context.AREA, "AREA_ID", "AREA_ID", pUESTO.AREA_ID);
            ViewData["NIVEL_ID"] = new SelectList(_context.NIVEL, "NIVEL_ID", "NIVEL_ID", pUESTO.NIVEL_ID);
            return View(pUESTO);
        }

        // POST: Puestos/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PUESTO_ID,PUESTO_NOMBRE,PUESTO_DESCRIPCION,AREA_ID,NIVEL_ID,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] PUESTO pUESTO)
        {
            if (id != pUESTO.PUESTO_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pUESTO);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PUESTOExists(pUESTO.PUESTO_ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["AREA_ID"] = new SelectList(_context.AREA, "AREA_ID", "AREA_ID", pUESTO.AREA_ID);
            ViewData["NIVEL_ID"] = new SelectList(_context.NIVEL, "NIVEL_ID", "NIVEL_ID", pUESTO.NIVEL_ID);
            return View(pUESTO);
        }

        // GET: Puestos/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pUESTO = await _context.PUESTO
                .Include(p => p.AREA)
                .Include(p => p.NIVEL)
                .FirstOrDefaultAsync(m => m.PUESTO_ID == id);
            if (pUESTO == null)
            {
                return NotFound();
            }

            return View(pUESTO);
        }

        // POST: Puestos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pUESTO = await _context.PUESTO.FindAsync(id);
            if (pUESTO != null)
            {
                _context.PUESTO.Remove(pUESTO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PUESTOExists(string id)
        {
            return _context.PUESTO.Any(e => e.PUESTO_ID == id);
        }
    }
}
