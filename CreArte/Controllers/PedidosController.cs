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
    public class PedidosController : Controller
    {
        private readonly CreArteDbContext _context;

        public PedidosController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Pedidos
        public async Task<IActionResult> Index()
        {
            var creArteDbContext = _context.PEDIDO.Include(p => p.CLIENTE).Include(p => p.ESTADO_PEDIDO);
            return View(await creArteDbContext.ToListAsync());
        }

        // GET: Pedidos/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pEDIDO = await _context.PEDIDO
                .Include(p => p.CLIENTE)
                .Include(p => p.ESTADO_PEDIDO)
                .FirstOrDefaultAsync(m => m.PEDIDO_ID == id);
            if (pEDIDO == null)
            {
                return NotFound();
            }

            return View(pEDIDO);
        }

        // GET: Pedidos/Create
        public IActionResult Create()
        {
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID");
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID");
            return View();
        }

        // POST: Pedidos/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PEDIDO_ID,FECHA_PEDIDO,ESTADO_PEDIDO_ID,FECHA_ENTREGA_PEDIDO,OBSERVACIONES_PEDIDO,CLIENTE_ID,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] PEDIDO pEDIDO)
        {
            if (ModelState.IsValid)
            {
                _context.Add(pEDIDO);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID", pEDIDO.CLIENTE_ID);
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID", pEDIDO.ESTADO_PEDIDO_ID);
            return View(pEDIDO);
        }

        // GET: Pedidos/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pEDIDO = await _context.PEDIDO.FindAsync(id);
            if (pEDIDO == null)
            {
                return NotFound();
            }
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID", pEDIDO.CLIENTE_ID);
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID", pEDIDO.ESTADO_PEDIDO_ID);
            return View(pEDIDO);
        }

        // POST: Pedidos/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PEDIDO_ID,FECHA_PEDIDO,ESTADO_PEDIDO_ID,FECHA_ENTREGA_PEDIDO,OBSERVACIONES_PEDIDO,CLIENTE_ID,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] PEDIDO pEDIDO)
        {
            if (id != pEDIDO.PEDIDO_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pEDIDO);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PEDIDOExists(pEDIDO.PEDIDO_ID))
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
            ViewData["CLIENTE_ID"] = new SelectList(_context.CLIENTE, "CLIENTE_ID", "CLIENTE_ID", pEDIDO.CLIENTE_ID);
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID", pEDIDO.ESTADO_PEDIDO_ID);
            return View(pEDIDO);
        }

        // GET: Pedidos/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pEDIDO = await _context.PEDIDO
                .Include(p => p.CLIENTE)
                .Include(p => p.ESTADO_PEDIDO)
                .FirstOrDefaultAsync(m => m.PEDIDO_ID == id);
            if (pEDIDO == null)
            {
                return NotFound();
            }

            return View(pEDIDO);
        }

        // POST: Pedidos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pEDIDO = await _context.PEDIDO.FindAsync(id);
            if (pEDIDO != null)
            {
                _context.PEDIDO.Remove(pEDIDO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PEDIDOExists(string id)
        {
            return _context.PEDIDO.Any(e => e.PEDIDO_ID == id);
        }
    }
}
