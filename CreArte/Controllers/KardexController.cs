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
    public class KardexController : Controller
    {
        private readonly CreArteDbContext _context;

        public KardexController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Kardex
        public async Task<IActionResult> Index()
        {
            var creArteDbContext = _context.KARDEX.Include(k => k.PRODUCTO);
            return View(await creArteDbContext.ToListAsync());
        }

        // GET: Kardex/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kARDEX = await _context.KARDEX
                .Include(k => k.PRODUCTO)
                .FirstOrDefaultAsync(m => m.KARDEX_ID == id);
            if (kARDEX == null)
            {
                return NotFound();
            }

            return View(kARDEX);
        }

        // GET: Kardex/Create
        public IActionResult Create()
        {
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID");
            return View();
        }

        // POST: Kardex/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("KARDEX_ID,PRODUCTO_ID,FECHA,TIPO_MOVIMIENTO,CANTIDAD,COSTO_UNITARIO,REFERENCIA,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] KARDEX kARDEX)
        {
            if (ModelState.IsValid)
            {
                _context.Add(kARDEX);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", kARDEX.PRODUCTO_ID);
            return View(kARDEX);
        }

        // GET: Kardex/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kARDEX = await _context.KARDEX.FindAsync(id);
            if (kARDEX == null)
            {
                return NotFound();
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", kARDEX.PRODUCTO_ID);
            return View(kARDEX);
        }

        // POST: Kardex/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("KARDEX_ID,PRODUCTO_ID,FECHA,TIPO_MOVIMIENTO,CANTIDAD,COSTO_UNITARIO,REFERENCIA,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] KARDEX kARDEX)
        {
            if (id != kARDEX.KARDEX_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(kARDEX);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!KARDEXExists(kARDEX.KARDEX_ID))
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
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", kARDEX.PRODUCTO_ID);
            return View(kARDEX);
        }

        // GET: Kardex/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kARDEX = await _context.KARDEX
                .Include(k => k.PRODUCTO)
                .FirstOrDefaultAsync(m => m.KARDEX_ID == id);
            if (kARDEX == null)
            {
                return NotFound();
            }

            return View(kARDEX);
        }

        // POST: Kardex/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var kARDEX = await _context.KARDEX.FindAsync(id);
            if (kARDEX != null)
            {
                _context.KARDEX.Remove(kARDEX);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool KARDEXExists(string id)
        {
            return _context.KARDEX.Any(e => e.KARDEX_ID == id);
        }
    }
}
