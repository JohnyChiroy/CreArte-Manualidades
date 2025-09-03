using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        //public async Task<IActionResult> Index()
        //{
        //    var creArteDbContext = _context.USUARIO.Include(u => u.EMPLEADO).Include(u => u.ROL);
        //    return View(await creArteDbContext.ToListAsync());
        //}        

        //public IActionResult Index([FromQuery] UsuarioListaViewModels vm)
        //{
        //    // (Opcional) Enviar roles desde aquí si quieres que el combo sea dinámico.
        //    ViewBag.Roles = new[] { "Administrador", "Vendedor" };

        //    // Aún no cargamos datos; la UI se mostrará vacía y funcional para enviar filtros.
        //    return View(vm);
        //}

        // GET: Usuarios
        // Ruta: GET /Usuarios/Index
        public async Task<IActionResult> Index([FromQuery] UsuarioListaViewModels vm)
        {
            // -------- 0) Lee también "Usuario" desde la QueryString por compatibilidad
            string usuarioFiltro = vm?.Usuario;
            if (string.IsNullOrWhiteSpace(usuarioFiltro))
                usuarioFiltro = HttpContext?.Request?.Query["Usuario"].ToString();

            // -------- 1) Query base con navegación de ROL (solo lectura)
            var q = _context.USUARIO
                .AsNoTracking()
                .Include(u => u.ROL) // para poder filtrar/ordenar por nombre de rol
                .AsQueryable();

            // -------- 2) Búsqueda global (input de la derecha)
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var term = vm.Search.Trim();
                q = q.Where(u =>
                    u.USUARIO_ID.Contains(term) ||
                    u.USUARIO_NOMBRE.Contains(term));
            }

            // -------- 3) Filtro “USUARIO” del popover (texto, (Blanks), (Non blanks))
            if (!string.IsNullOrEmpty(usuarioFiltro))
            {
                if (usuarioFiltro == "__BLANKS__")
                    q = q.Where(u => string.IsNullOrEmpty(u.USUARIO_NOMBRE));
                else if (usuarioFiltro == "__NONBLANKS__")
                    q = q.Where(u => !string.IsNullOrEmpty(u.USUARIO_NOMBRE));
                else
                {
                    var name = usuarioFiltro.Trim();
                    q = q.Where(u => u.USUARIO_NOMBRE.Contains(name));
                }
            }

            // -------- 4) Filtro por ROL (acepta ID o nombre)
            if (!string.IsNullOrWhiteSpace(vm.Rol))
            {
                var rolTerm = vm.Rol.Trim();
                q = q.Where(u =>
                    u.ROL_ID == rolTerm ||
                    (u.ROL != null && u.ROL.ROL_NOMBRE == rolTerm));
            }

            // -------- 5) Filtro por ESTADO
            if (vm.Estado.HasValue)
            {
                q = q.Where(u => u.ESTADO == vm.Estado.Value);
            }

            // -------- 6) Filtro por RANGO DE FECHAS (FECHA_CREACION)
            if (vm.FechaInicio.HasValue)
            {
                var desde = vm.FechaInicio.Value.Date; // 00:00
                q = q.Where(u => u.FECHA_CREACION >= desde);
            }
            if (vm.FechaFin.HasValue)
            {
                var hasta = vm.FechaFin.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
                q = q.Where(u => u.FECHA_CREACION <= hasta);
            }

            // -------- 7) Total + ORDENAMIENTO (por defecto: fecha desc)
            vm.TotalItems = await q.CountAsync();

            var sort = (vm.Sort ?? "fecha").ToLower();
            var dir = (vm.Dir ?? "desc").ToLower();
            bool asc = dir == "asc";

            IQueryable<USUARIO> ApplyOrder(IQueryable<USUARIO> src)
            {
                switch (sort)
                {
                    case "id":
                        return asc ? src.OrderBy(u => u.USUARIO_ID)
                                   : src.OrderByDescending(u => u.USUARIO_ID);
                    case "usuario":
                        return asc ? src.OrderBy(u => u.USUARIO_NOMBRE)
                                   : src.OrderByDescending(u => u.USUARIO_NOMBRE);
                    case "rol":
                        return asc ? src.OrderBy(u => u.ROL != null ? u.ROL.ROL_NOMBRE : "")
                                   : src.OrderByDescending(u => u.ROL != null ? u.ROL.ROL_NOMBRE : "");
                    case "estado":
                        // Asc: Inactivo(false) -> Activo(true)
                        return asc ? src.OrderBy(u => u.ESTADO)
                                   : src.OrderByDescending(u => u.ESTADO);
                    case "fecha":
                    default:
                        return asc ? src.OrderBy(u => u.FECHA_CREACION)
                                   : src.OrderByDescending(u => u.FECHA_CREACION);
                }
            }
            q = ApplyOrder(q);

            // -------- 8) Paginación segura
            vm.Page = vm.Page <= 0 ? 1 : vm.Page;
            vm.PageSize = vm.PageSize <= 0 ? 10 : vm.PageSize;
            var skip = (vm.Page - 1) * vm.PageSize;

            vm.Items = await q.Skip(skip).Take(vm.PageSize).ToListAsync();

            // -------- 9) TotalPages (para la vista)
            vm.TotalPages = (int)Math.Ceiling((double)vm.TotalItems / vm.PageSize);

            // -------- 10) Combo de roles (nombres legibles)
            ViewBag.Roles = await _context.ROL
                .AsNoTracking()
                .OrderBy(r => r.ROL_NOMBRE)
                .Select(r => r.ROL_NOMBRE)
                .ToListAsync();

            // -------- 11) Devolvemos la vista
            vm.Usuario = usuarioFiltro; // por si tu VM lo necesita en la vista
            return View(vm);
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
