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
    public class CajaController : Controller
    {
        private readonly CreArteDbContext _context;

        public CajaController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Caja
        public async Task<IActionResult> Index()
        {
            return View(await _context.CAJA.ToListAsync());
        }

        // GET: Caja/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cAJA = await _context.CAJA
                .FirstOrDefaultAsync(m => m.CAJA_ID == id);
            if (cAJA == null)
            {
                return NotFound();
            }

            return View(cAJA);
        }

        // GET: Caja/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Caja/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CAJA_ID,CAJA_NOMBRE,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] CAJA cAJA)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cAJA);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cAJA);
        }

        // GET: Caja/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cAJA = await _context.CAJA.FindAsync(id);
            if (cAJA == null)
            {
                return NotFound();
            }
            return View(cAJA);
        }

        // POST: Caja/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("CAJA_ID,CAJA_NOMBRE,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] CAJA cAJA)
        {
            if (id != cAJA.CAJA_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cAJA);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CAJAExists(cAJA.CAJA_ID))
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
            return View(cAJA);
        }

        // GET: Caja/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cAJA = await _context.CAJA
                .FirstOrDefaultAsync(m => m.CAJA_ID == id);
            if (cAJA == null)
            {
                return NotFound();
            }

            return View(cAJA);
        }

        // POST: Caja/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cAJA = await _context.CAJA.FindAsync(id);
            if (cAJA != null)
            {
                _context.CAJA.Remove(cAJA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CAJAExists(string id)
        {
            return _context.CAJA.Any(e => e.CAJA_ID == id);
        }
    }
}
