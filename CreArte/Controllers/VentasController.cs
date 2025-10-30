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
    public class VentasController : Controller
    {
        private readonly CreArteDbContext _context;

        public VentasController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Ventas
        public async Task<IActionResult> Index()
        {
            var creArteDbContext = _context.VENTA.Include(v => v.CLIENTE).Include(v => v.USUARIO);
            return View(await creArteDbContext.ToListAsync());
        }

        // GET: Ventas/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vENTA = await _context.VENTA
                .Include(v => v.CLIENTE)
                .Include(v => v.USUARIO)
                .FirstOrDefaultAsync(m => m.VENTA_ID == id);
            if (vENTA == null)
            {
                return NotFound();
            }

            return View(vENTA);
        }

        // GET: Ventas/Create
        public IActionResult Create()
        {
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID");
            ViewData["USUARIO_ID"] = new SelectList(_context.USUARIO, "USUARIO_ID", "USUARIO_ID");
            return View();
        }

        // POST: Ventas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VENTA_ID,FECHA,CLIENTE_ID,USUARIO_ID,TOTAL,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] VENTA vENTA)
        {
            if (ModelState.IsValid)
            {
                _context.Add(vENTA);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID", vENTA.CLIENTE_ID);
            ViewData["USUARIO_ID"] = new SelectList(_context.USUARIO, "USUARIO_ID", "USUARIO_ID", vENTA.USUARIO_ID);
            return View(vENTA);
        }

        // GET: Ventas/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vENTA = await _context.VENTA.FindAsync(id);
            if (vENTA == null)
            {
                return NotFound();
            }
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID", vENTA.CLIENTE_ID);
            ViewData["USUARIO_ID"] = new SelectList(_context.USUARIO, "USUARIO_ID", "USUARIO_ID", vENTA.USUARIO_ID);
            return View(vENTA);
        }

        // POST: Ventas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("VENTA_ID,FECHA,CLIENTE_ID,USUARIO_ID,TOTAL,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] VENTA vENTA)
        {
            if (id != vENTA.VENTA_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(vENTA);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VENTAExists(vENTA.VENTA_ID))
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
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID", vENTA.CLIENTE_ID);
            ViewData["USUARIO_ID"] = new SelectList(_context.USUARIO, "USUARIO_ID", "USUARIO_ID", vENTA.USUARIO_ID);
            return View(vENTA);
        }

        // GET: Ventas/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vENTA = await _context.VENTA
                .Include(v => v.CLIENTE)
                .Include(v => v.USUARIO)
                .FirstOrDefaultAsync(m => m.VENTA_ID == id);
            if (vENTA == null)
            {
                return NotFound();
            }

            return View(vENTA);
        }

        // POST: Ventas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var vENTA = await _context.VENTA.FindAsync(id);
            if (vENTA != null)
            {
                _context.VENTA.Remove(vENTA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VENTAExists(string id)
        {
            return _context.VENTA.Any(e => e.VENTA_ID == id);
        }
    }
}
