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
    public class InventarioController : Controller
    {
        private readonly CreArteDbContext _context;

        public InventarioController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Inventario
        public async Task<IActionResult> Index()
        {
            var creArteDbContext = _context.INVENTARIO.Include(i => i.PRODUCTO);
            return View(await creArteDbContext.ToListAsync());
        }

        // GET: Inventario/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iNVENTARIO = await _context.INVENTARIO
                .Include(i => i.PRODUCTO)
                .FirstOrDefaultAsync(m => m.INVENTARIO_ID == id);
            if (iNVENTARIO == null)
            {
                return NotFound();
            }

            return View(iNVENTARIO);
        }

        // GET: Inventario/Create
        public IActionResult Create()
        {
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID");
            return View();
        }

        // POST: Inventario/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("INVENTARIO_ID,PRODUCTO_ID,STOCK_ACTUAL,STOCK_MINIMO,COSTO_UNITARIO,FECHA_VENCIMIENTO,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] INVENTARIO iNVENTARIO)
        {
            if (ModelState.IsValid)
            {
                _context.Add(iNVENTARIO);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", iNVENTARIO.PRODUCTO_ID);
            return View(iNVENTARIO);
        }

        // GET: Inventario/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iNVENTARIO = await _context.INVENTARIO.FindAsync(id);
            if (iNVENTARIO == null)
            {
                return NotFound();
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", iNVENTARIO.PRODUCTO_ID);
            return View(iNVENTARIO);
        }

        // POST: Inventario/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("INVENTARIO_ID,PRODUCTO_ID,STOCK_ACTUAL,STOCK_MINIMO,COSTO_UNITARIO,FECHA_VENCIMIENTO,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] INVENTARIO iNVENTARIO)
        {
            if (id != iNVENTARIO.INVENTARIO_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(iNVENTARIO);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!INVENTARIOExists(iNVENTARIO.INVENTARIO_ID))
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
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", iNVENTARIO.PRODUCTO_ID);
            return View(iNVENTARIO);
        }

        // GET: Inventario/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iNVENTARIO = await _context.INVENTARIO
                .Include(i => i.PRODUCTO)
                .FirstOrDefaultAsync(m => m.INVENTARIO_ID == id);
            if (iNVENTARIO == null)
            {
                return NotFound();
            }

            return View(iNVENTARIO);
        }

        // POST: Inventario/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var iNVENTARIO = await _context.INVENTARIO.FindAsync(id);
            if (iNVENTARIO != null)
            {
                _context.INVENTARIO.Remove(iNVENTARIO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool INVENTARIOExists(string id)
        {
            return _context.INVENTARIO.Any(e => e.INVENTARIO_ID == id);
        }
    }
}
