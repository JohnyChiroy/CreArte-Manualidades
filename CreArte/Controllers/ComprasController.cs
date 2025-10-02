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
    public class ComprasController : Controller
    {
        private readonly CreArteDbContext _context;

        public ComprasController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Compras
        public async Task<IActionResult> Index()
        {
            var creArteDbContext = _context.COMPRA.Include(c => c.ESTADO_COMPRA).Include(c => c.PROVEEDOR);
            return View(await creArteDbContext.ToListAsync());
        }

        // GET: Compras/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cOMPRA = await _context.COMPRA
                .Include(c => c.ESTADO_COMPRA)
                .Include(c => c.PROVEEDOR)
                .FirstOrDefaultAsync(m => m.COMPRA_ID == id);
            if (cOMPRA == null)
            {
                return NotFound();
            }

            return View(cOMPRA);
        }

        // GET: Compras/Create
        public IActionResult Create()
        {
            ViewData["ESTADO_COMPRA_ID"] = new SelectList(_context.ESTADO_COMPRA, "ESTADO_COMPRA_ID", "ESTADO_COMPRA_ID");
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID");
            return View();
        }

        // POST: Compras/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("COMPRA_ID,FECHA_COMPRA,ESTADO_COMPRA_ID,FECHA_ENTREGA_COMPRA,OBSERVACIONES_COMPRA,PROVEEDOR_ID,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] COMPRA cOMPRA)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cOMPRA);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ESTADO_COMPRA_ID"] = new SelectList(_context.ESTADO_COMPRA, "ESTADO_COMPRA_ID", "ESTADO_COMPRA_ID", cOMPRA.ESTADO_COMPRA_ID);
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID", cOMPRA.PROVEEDOR_ID);
            return View(cOMPRA);
        }

        // GET: Compras/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cOMPRA = await _context.COMPRA.FindAsync(id);
            if (cOMPRA == null)
            {
                return NotFound();
            }
            ViewData["ESTADO_COMPRA_ID"] = new SelectList(_context.ESTADO_COMPRA, "ESTADO_COMPRA_ID", "ESTADO_COMPRA_ID", cOMPRA.ESTADO_COMPRA_ID);
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID", cOMPRA.PROVEEDOR_ID);
            return View(cOMPRA);
        }

        // POST: Compras/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("COMPRA_ID,FECHA_COMPRA,ESTADO_COMPRA_ID,FECHA_ENTREGA_COMPRA,OBSERVACIONES_COMPRA,PROVEEDOR_ID,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] COMPRA cOMPRA)
        {
            if (id != cOMPRA.COMPRA_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cOMPRA);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!COMPRAExists(cOMPRA.COMPRA_ID))
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
            ViewData["ESTADO_COMPRA_ID"] = new SelectList(_context.ESTADO_COMPRA, "ESTADO_COMPRA_ID", "ESTADO_COMPRA_ID", cOMPRA.ESTADO_COMPRA_ID);
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID", cOMPRA.PROVEEDOR_ID);
            return View(cOMPRA);
        }

        // GET: Compras/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cOMPRA = await _context.COMPRA
                .Include(c => c.ESTADO_COMPRA)
                .Include(c => c.PROVEEDOR)
                .FirstOrDefaultAsync(m => m.COMPRA_ID == id);
            if (cOMPRA == null)
            {
                return NotFound();
            }

            return View(cOMPRA);
        }

        // POST: Compras/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cOMPRA = await _context.COMPRA.FindAsync(id);
            if (cOMPRA != null)
            {
                _context.COMPRA.Remove(cOMPRA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool COMPRAExists(string id)
        {
            return _context.COMPRA.Any(e => e.COMPRA_ID == id);
        }
    }
}
