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
    public class UsuariosController : Controller
    {
        private readonly CreArteDbContext _context;

        public UsuariosController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: Usuarios
        public async Task<IActionResult> Index()
        {
            var creArteDbContext = _context.USUARIO.Include(u => u.EMPLEADO).Include(u => u.ROL);
            return View(await creArteDbContext.ToListAsync());
        }

        // GET: Usuarios/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var uSUARIO = await _context.USUARIO
                .Include(u => u.EMPLEADO)
                .Include(u => u.ROL)
                .FirstOrDefaultAsync(m => m.USUARIO_ID == id);
            if (uSUARIO == null)
            {
                return NotFound();
            }

            return View(uSUARIO);
        }

        // GET: Usuarios/Create
        public IActionResult Create()
        {
            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID");
            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID");
            return View();
        }

        // POST: Usuarios/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("USUARIO_ID,USUARIO_NOMBRE,USUARIO_CONTRASENA,USUARIO_SALT,USUARIO_FECHAREGISTRO,USUARIO_CAMBIOINICIAL,ROL_ID,EMPLEADO_ID,USUARIO_CORREO,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] USUARIO uSUARIO)
        {
            if (ModelState.IsValid)
            {
                _context.Add(uSUARIO);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID", uSUARIO.EMPLEADO_ID);
            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID", uSUARIO.ROL_ID);
            return View(uSUARIO);
        }

        // GET: Usuarios/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var uSUARIO = await _context.USUARIO.FindAsync(id);
            if (uSUARIO == null)
            {
                return NotFound();
            }
            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID", uSUARIO.EMPLEADO_ID);
            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID", uSUARIO.ROL_ID);
            return View(uSUARIO);
        }

        // POST: Usuarios/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("USUARIO_ID,USUARIO_NOMBRE,USUARIO_CONTRASENA,USUARIO_SALT,USUARIO_FECHAREGISTRO,USUARIO_CAMBIOINICIAL,ROL_ID,EMPLEADO_ID,USUARIO_CORREO,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] USUARIO uSUARIO)
        {
            if (id != uSUARIO.USUARIO_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(uSUARIO);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!USUARIOExists(uSUARIO.USUARIO_ID))
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
            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID", uSUARIO.EMPLEADO_ID);
            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID", uSUARIO.ROL_ID);
            return View(uSUARIO);
        }

        // GET: Usuarios/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var uSUARIO = await _context.USUARIO
                .Include(u => u.EMPLEADO)
                .Include(u => u.ROL)
                .FirstOrDefaultAsync(m => m.USUARIO_ID == id);
            if (uSUARIO == null)
            {
                return NotFound();
            }

            return View(uSUARIO);
        }

        // POST: Usuarios/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var uSUARIO = await _context.USUARIO.FindAsync(id);
            if (uSUARIO != null)
            {
                _context.USUARIO.Remove(uSUARIO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool USUARIOExists(string id)
        {
            return _context.USUARIO.Any(e => e.USUARIO_ID == id);
        }
    }
}
