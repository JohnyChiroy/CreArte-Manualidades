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
using System.Security.Cryptography; // <-- Para SALT + SHA-256
using System.Text;

namespace CreArte.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly CreArteDbContext _context;

        public UsuariosController(CreArteDbContext context)
        {
            _context = context;
        }
        //public IActionResult Index()
        //{
        //    // Traemos de sesión el nombre que guardaste al hacer login
        //    ViewBag.Nombre = HttpContext.Session.GetString("Nombre") ?? "Usuario";
        //    return View(); // Views/Home/Index.cshtml
        //}

        // ============================================================
        // LISTADO (queda como lo tenías, sólo se mantiene tal cual)
        // Ruta: GET /Usuarios?Search=...&Rol=...&Estado=... etc.
        // ============================================================
        public async Task<IActionResult> Index([FromQuery] UsuarioListaViewModels vm)
        {
            string usuarioFiltro = vm?.Usuario;
            if (string.IsNullOrWhiteSpace(usuarioFiltro))
                usuarioFiltro = HttpContext?.Request?.Query["Usuario"].ToString();

            var q = _context.USUARIO
                .AsNoTracking()
                .Include(u => u.ROL)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var term = vm.Search.Trim();
                q = q.Where(u => u.USUARIO_ID.Contains(term) ||
                                 u.USUARIO_NOMBRE.Contains(term));
            }

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

            if (!string.IsNullOrWhiteSpace(vm.Rol))
            {
                var rolTerm = vm.Rol.Trim();
                q = q.Where(u => u.ROL_ID == rolTerm ||
                                (u.ROL != null && u.ROL.ROL_NOMBRE == rolTerm));
            }

            if (vm.Estado.HasValue)
                q = q.Where(u => u.ESTADO == vm.Estado.Value);

            if (vm.FechaInicio.HasValue)
            {
                var desde = vm.FechaInicio.Value.Date;
                q = q.Where(u => u.FECHA_CREACION >= desde);
            }
            if (vm.FechaFin.HasValue)
            {
                var hasta = vm.FechaFin.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(u => u.FECHA_CREACION <= hasta);
            }

            vm.TotalItems = await q.CountAsync();

            var sort = (vm.Sort ?? "id").ToLower();
            var dir = (vm.Dir ?? "desc").ToLower();
            bool asc = dir == "asc";

            IQueryable<USUARIO> ApplyOrder(IQueryable<USUARIO> src)
            {
                switch (sort)
                {
                    case "id": return asc ? src.OrderBy(u => u.USUARIO_ID) : src.OrderByDescending(u => u.USUARIO_ID);
                    case "usuario": return asc ? src.OrderBy(u => u.USUARIO_NOMBRE) : src.OrderByDescending(u => u.USUARIO_NOMBRE);
                    case "rol":
                        return asc ? src.OrderBy(u => u.ROL != null ? u.ROL.ROL_NOMBRE : "")
                                               : src.OrderByDescending(u => u.ROL != null ? u.ROL.ROL_NOMBRE : "");
                    case "estado": return asc ? src.OrderBy(u => u.ESTADO) : src.OrderByDescending(u => u.ESTADO);
                    case "fecha":
                    default: return asc ? src.OrderBy(u => u.FECHA_CREACION) : src.OrderByDescending(u => u.FECHA_CREACION);
                }
            }
            q = ApplyOrder(q);

            vm.Page = vm.Page <= 0 ? 1 : vm.Page;
            vm.PageSize = vm.PageSize <= 0 ? 10 : vm.PageSize;
            var skip = (vm.Page - 1) * vm.PageSize;

            vm.Items = await q.Skip(skip).Take(vm.PageSize).ToListAsync();
            vm.TotalPages = (int)Math.Ceiling((double)vm.TotalItems / vm.PageSize);

            ViewBag.Roles = await _context.ROL
                .AsNoTracking()
                .OrderBy(r => r.ROL_NOMBRE)
                .Select(r => r.ROL_NOMBRE)
                .ToListAsync();

            vm.Usuario = usuarioFiltro;
            return View(vm);
        }

        // ============================================================
        // DETALLES (sin cambios relevantes)
        // Ruta: GET /Usuarios/Details/{id}
        // ============================================================
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var uSUARIO = await _context.USUARIO
                .Include(u => u.EMPLEADO)
                .Include(u => u.ROL)
                .FirstOrDefaultAsync(m => m.USUARIO_ID == id);

            if (uSUARIO == null) return NotFound();
            return View(uSUARIO);
        }

        // =========================================================
        // GET: /Usuarios/Create
        // - Genera el USUARIO_ID (ej. U00001)
        // - Pone ESTADO = true
        // - Llena combos Rol/Empleado
        // =========================================================
        public async Task<IActionResult> Create()
        {
            var model = new USUARIO
            {
                USUARIO_ID = await ObtenerSiguienteUsuarioIdAsync(),
                ESTADO = true,
                ELIMINADO = false
            };

            await CargarCombosAsync(null, null);
            return View(model);
        }

        // =========================================================
        // POST: /Usuarios/Create
        // - Recibe: USUARIO (campos visibles) + PwdPlain + PwdPlainConfirm
        // - Valida reglas de contraseña (mismas que en tu JS)
        // - Calcula SALT + SHA-256(SALT||UTF8(password))
        // - Completa auditoría y guarda
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            // Bindea solo los campos visibles/seguros de la vista
            [Bind("USUARIO_ID,USUARIO_NOMBRE,ROL_ID,EMPLEADO_ID,ESTADO")] USUARIO usuario,
            string PwdPlain,
            string PwdPlainConfirm)
        {
            // 1) Validaciones básicas del modelo
            if (string.IsNullOrWhiteSpace(usuario.USUARIO_ID))
                ModelState.AddModelError(nameof(usuario.USUARIO_ID), "El código es requerido.");
            if (string.IsNullOrWhiteSpace(usuario.USUARIO_NOMBRE))
                ModelState.AddModelError(nameof(usuario.USUARIO_NOMBRE), "El nombre de usuario es requerido.");

            // 2) Validación de contraseña (servidor)
            if (string.IsNullOrWhiteSpace(PwdPlain) || string.IsNullOrWhiteSpace(PwdPlainConfirm))
                ModelState.AddModelError("", "Debe ingresar y confirmar la contraseña.");
            if (PwdPlain != PwdPlainConfirm)
                ModelState.AddModelError("", "Las contraseñas no coinciden.");
            if (!ValidaReglasPassword(PwdPlain))
                ModelState.AddModelError("", "La contraseña no cumple: minúscula, mayúscula, número y longitud 8–15.");

            // 3) Unicidad de nombre de usuario (opcional pero recomendado)
            bool nombreDuplicado = await _context.USUARIO
                .AnyAsync(u => u.USUARIO_NOMBRE == usuario.USUARIO_NOMBRE && !u.ELIMINADO);
            if (nombreDuplicado)
                ModelState.AddModelError(nameof(usuario.USUARIO_NOMBRE), "Ya existe un usuario con ese nombre.");

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(usuario.ROL_ID, usuario.EMPLEADO_ID);
                return View(usuario);
            }

            // 4) Generar SALT + HASH
            (byte[] salt, byte[] hash) = GenerarSaltYHash(PwdPlain);

            // 5) Completar campos no visibles (auditoría)
            usuario.USUARIO_SALT = salt;
            usuario.USUARIO_CONTRASENA = hash;
            usuario.ELIMINADO = false;
            usuario.USUARIO_CREACION = "SYSTEM";      // TODO: reemplaza por usuario logueado
            usuario.FECHA_CREACION = DateTime.Now;
            usuario.USUARIO_MODIFICACION = null;
            usuario.FECHA_MODIFICACION = null;
            usuario.USUARIO_ELIMINACION = null;
            usuario.FECHA_ELIMINACION = null;
            // usuario.USUARIO_CORREO -> si no lo manejas aquí, déjalo null o string.Empty

            // 6) Guardar
            try
            {
                _context.Add(usuario);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", $"No se pudo crear el usuario. Detalle: {ex.Message}");
                await CargarCombosAsync(usuario.ROL_ID, usuario.EMPLEADO_ID);
                return View(usuario);
            }
        }

        // =========================================================
        // ===================== HELPERS ===========================
        // =========================================================

        /// Genera el siguiente ID con formato U00001, U00002, ...
        private async Task<string> ObtenerSiguienteUsuarioIdAsyncHelper()
        {
            var last = await _context.USUARIO
                                     .OrderByDescending(u => u.USUARIO_ID)
                                     .Select(u => u.USUARIO_ID)
                                     .FirstOrDefaultAsync();
            int n = 0;
            if (!string.IsNullOrEmpty(last) && last.Length >= 6 && last.StartsWith("U"))
                int.TryParse(last.Substring(1), out n);
            return $"U{(n + 1).ToString("D5")}";
        }

        /// Llena combos de Rol y Empleado (seguro).
        private async Task CargarCombosAsyncHelper(string? rolSeleccionado, string? empleadoSeleccionado)
        {
            var roles = await _context.ROL
                .AsNoTracking()
                .OrderBy(r => r.ROL_NOMBRE) // si no existe, usa .OrderBy(r => r.ROL_ID)
                .Select(r => new { Value = r.ROL_ID, Text = r.ROL_NOMBRE ?? r.ROL_ID })
                .ToListAsync();
            ViewData["ROL_ID"] = new SelectList(roles, "Value", "Text", rolSeleccionado);

            var empleados = await _context.EMPLEADO
                .AsNoTracking()
                .OrderBy(e => e.EMPLEADO_ID)
                .Select(e => new { Value = e.EMPLEADO_ID, Text = e.EMPLEADO_ID }) // cambia Text si luego tienes nombre
                .ToListAsync();
            ViewData["EMPLEADO_ID"] = new SelectList(empleados, "Value", "Text", empleadoSeleccionado);
        }

        /// Reglas de contraseña: 1 minúscula, 1 mayúscula, 1 número, longitud [8..15].
        private bool ValidaReglasPasswordHelper(string pwd)
        {
            if (string.IsNullOrEmpty(pwd)) return false;
            bool hasLower = pwd.Any(char.IsLower);
            bool hasUpper = pwd.Any(char.IsUpper);
            bool hasDigit = pwd.Any(char.IsDigit);
            bool okLen = pwd.Length >= 8 && pwd.Length <= 15;
            return hasLower && hasUpper && hasDigit && okLen;
        }

        /// SALT(64) + SHA256(SALT || UTF8(password))
        private (byte[] salt, byte[] hash) GenerarSaltYHashHelper(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] salt = new byte[64];
            rng.GetBytes(salt);

            using var sha = SHA256.Create();
            byte[] pwdBytes = Encoding.UTF8.GetBytes(password);
            byte[] toHash = new byte[salt.Length + pwdBytes.Length];
            Buffer.BlockCopy(salt, 0, toHash, 0, salt.Length);
            Buffer.BlockCopy(pwdBytes, 0, toHash, salt.Length, pwdBytes.Length);
            byte[] hash = sha.ComputeHash(toHash);
            return (salt, hash);
        }

        // ============================================================
        // EDIT (POST) – sin tocar contraseñas aquí (opcional agregar cambio de pwd)
        // Ruta: POST /Usuarios/Edit/{id}
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("USUARIO_ID,USUARIO_NOMBRE,ROL_ID,EMPLEADO_ID,ESTADO")] USUARIO uSUARIO)
        {
            if (id != uSUARIO.USUARIO_ID) return NotFound();

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(uSUARIO.ROL_ID, uSUARIO.EMPLEADO_ID);
                return View(uSUARIO);
            }

            try
            {
                var dbUser = await _context.USUARIO.FirstOrDefaultAsync(u => u.USUARIO_ID == id);
                if (dbUser == null) return NotFound();

                dbUser.USUARIO_NOMBRE = uSUARIO.USUARIO_NOMBRE;
                dbUser.ROL_ID = uSUARIO.ROL_ID;
                dbUser.EMPLEADO_ID = uSUARIO.EMPLEADO_ID;
                dbUser.ESTADO = uSUARIO.ESTADO;
                dbUser.USUARIO_MODIFICACION = "SYSTEM";
                dbUser.FECHA_MODIFICACION = DateTime.Now;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!USUARIOExists(uSUARIO.USUARIO_ID)) return NotFound();
                else throw;
            }
        }

        // ============================================================
        // DELETE (GET/POST) – igual que lo tenías (borrado duro)
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var uSUARIO = await _context.USUARIO
                .Include(u => u.EMPLEADO)
                .Include(u => u.ROL)
                .FirstOrDefaultAsync(m => m.USUARIO_ID == id);

            if (uSUARIO == null) return NotFound();
            return View(uSUARIO);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var uSUARIO = await _context.USUARIO.FindAsync(id);
            if (uSUARIO != null) _context.USUARIO.Remove(uSUARIO);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool USUARIOExists(string id)
        {
            return _context.USUARIO.Any(e => e.USUARIO_ID == id);
        }

        // ============================================================
        // =====================  HELPERS  ============================
        // ============================================================

        /// <summary>
        /// Genera el siguiente código con formato U00001, U00002, ...
        /// Busca el mayor USUARIO_ID existente y suma 1.
        /// </summary>
        private async Task<string> ObtenerSiguienteUsuarioIdAsync()
        {
            var last = await _context.USUARIO
                                     .OrderByDescending(u => u.USUARIO_ID)
                                     .Select(u => u.USUARIO_ID)
                                     .FirstOrDefaultAsync();

            int n = 0;
            if (!string.IsNullOrEmpty(last) && last.Length >= 6 && last.StartsWith("U"))
                int.TryParse(last.Substring(1), out n);

            return $"U{(n + 1).ToString("D5")}";
        }

        /// <summary>
        /// Llena los combos de Rol y Empleado. 
        /// - Value = IDs reales
        /// - Text  = nombres legibles (ROL_NOMBRE y, si no tienes nombre de persona, usa EMPLEADO_ID)
        /// </summary>
        // Helper seguro: usa EMPLEADO_ID como texto (no asume PERSONA_NOMBRE)
        private async Task CargarCombosAsync(string? rolSeleccionado, string? empleadoSeleccionado)
        {
            // Roles (ID -> Nombre)
            var roles = await _context.ROL
                .AsNoTracking()
                .OrderBy(r => r.ROL_NOMBRE)
                .Select(r => new { Value = r.ROL_ID, Text = r.ROL_NOMBRE })
                .ToListAsync();
            ViewData["ROL_ID"] = new SelectList(roles, "Value", "Text", rolSeleccionado);

            // Empleados: mostramos el ID mientras confirmamos campo de nombre
            var empleados = await _context.EMPLEADO
                .AsNoTracking()
                .OrderBy(e => e.EMPLEADO_ID)
                .Select(e => new
                {
                    Value = e.EMPLEADO_ID,
                    Text = e.EMPLEADO_ID   // <- evita EF.Property("PERSONA_NOMBRE")
                })
                .ToListAsync();
            ViewData["EMPLEADO_ID"] = new SelectList(empleados, "Value", "Text", empleadoSeleccionado);
        }


        /// <summary>
        /// Valida reglas de contraseña: minúscula, mayúscula, número y longitud [8..15].
        /// </summary>
        private bool ValidaReglasPassword(string pwd)
        {
            if (string.IsNullOrEmpty(pwd)) return false;
            bool hasLower = pwd.Any(char.IsLower);
            bool hasUpper = pwd.Any(char.IsUpper);
            bool hasDigit = pwd.Any(char.IsDigit);
            bool okLen = pwd.Length >= 8 && pwd.Length <= 15;
            return hasLower && hasUpper && hasDigit && okLen;
        }

        /// <summary>
        /// Genera SALT (64 bytes) y HASH = SHA256(SALT || UTF8(password)).
        /// Devuelve (salt, hash).
        /// </summary>
        private (byte[] salt, byte[] hash) GenerarSaltYHash(string password)
        {
            // 1) SALT aleatorio de 64 bytes
            using var rng = RandomNumberGenerator.Create();
            byte[] salt = new byte[64];
            rng.GetBytes(salt);

            // 2) HASH SHA-256(SALT || UTF8(password))
            using var sha = SHA256.Create();
            byte[] pwdBytes = Encoding.UTF8.GetBytes(password);
            byte[] toHash = new byte[salt.Length + pwdBytes.Length];
            Buffer.BlockCopy(salt, 0, toHash, 0, salt.Length);
            Buffer.BlockCopy(pwdBytes, 0, toHash, salt.Length, pwdBytes.Length);
            byte[] hash = sha.ComputeHash(toHash);

            return (salt, hash);
        }
    }
}


//using CreArte.Data;
//using CreArte.Models;
//using CreArte.ModelsPartial;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;

//namespace CreArte.Controllers
//{
//    public class UsuariosController : Controller
//    {
//        private readonly CreArteDbContext _context;

//        public UsuariosController(CreArteDbContext context)
//        {
//            _context = context;
//        }

//        public async Task<IActionResult> Index([FromQuery] UsuarioListaViewModels vm)
//        {
//            // -------- 0) Lee también "Usuario" desde la QueryString por compatibilidad
//            string usuarioFiltro = vm?.Usuario;
//            if (string.IsNullOrWhiteSpace(usuarioFiltro))
//                usuarioFiltro = HttpContext?.Request?.Query["Usuario"].ToString();

//            // -------- 1) Query base con navegación de ROL (solo lectura)
//            var q = _context.USUARIO
//                .AsNoTracking()
//                .Include(u => u.ROL) // para poder filtrar/ordenar por nombre de rol
//                .AsQueryable();

//            // -------- 2) Búsqueda global (input de la derecha)
//            if (!string.IsNullOrWhiteSpace(vm.Search))
//            {
//                var term = vm.Search.Trim();
//                q = q.Where(u =>
//                    u.USUARIO_ID.Contains(term) ||
//                    u.USUARIO_NOMBRE.Contains(term));
//            }

//            // -------- 3) Filtro “USUARIO” del popover (texto, (Blanks), (Non blanks))
//            if (!string.IsNullOrEmpty(usuarioFiltro))
//            {
//                if (usuarioFiltro == "__BLANKS__")
//                    q = q.Where(u => string.IsNullOrEmpty(u.USUARIO_NOMBRE));
//                else if (usuarioFiltro == "__NONBLANKS__")
//                    q = q.Where(u => !string.IsNullOrEmpty(u.USUARIO_NOMBRE));
//                else
//                {
//                    var name = usuarioFiltro.Trim();
//                    q = q.Where(u => u.USUARIO_NOMBRE.Contains(name));
//                }
//            }

//            // -------- 4) Filtro por ROL (acepta ID o nombre)
//            if (!string.IsNullOrWhiteSpace(vm.Rol))
//            {
//                var rolTerm = vm.Rol.Trim();
//                q = q.Where(u =>
//                    u.ROL_ID == rolTerm ||
//                    (u.ROL != null && u.ROL.ROL_NOMBRE == rolTerm));
//            }

//            // -------- 5) Filtro por ESTADO
//            if (vm.Estado.HasValue)
//            {
//                q = q.Where(u => u.ESTADO == vm.Estado.Value);
//            }

//            // -------- 6) Filtro por RANGO DE FECHAS (FECHA_CREACION)
//            if (vm.FechaInicio.HasValue)
//            {
//                var desde = vm.FechaInicio.Value.Date; // 00:00
//                q = q.Where(u => u.FECHA_CREACION >= desde);
//            }
//            if (vm.FechaFin.HasValue)
//            {
//                var hasta = vm.FechaFin.Value.Date.AddDays(1).AddTicks(-1); // 23:59:59.9999999
//                q = q.Where(u => u.FECHA_CREACION <= hasta);
//            }

//            // -------- 7) Total + ORDENAMIENTO (por defecto: fecha desc)
//            vm.TotalItems = await q.CountAsync();

//            var sort = (vm.Sort ?? "id").ToLower();
//            var dir = (vm.Dir ?? "desc").ToLower();
//            bool asc = dir == "asc";

//            IQueryable<USUARIO> ApplyOrder(IQueryable<USUARIO> src)
//            {
//                switch (sort)
//                {
//                    case "id":
//                        return asc ? src.OrderBy(u => u.USUARIO_ID)
//                                   : src.OrderByDescending(u => u.USUARIO_ID);
//                    case "usuario":
//                        return asc ? src.OrderBy(u => u.USUARIO_NOMBRE)
//                                   : src.OrderByDescending(u => u.USUARIO_NOMBRE);
//                    case "rol":
//                        return asc ? src.OrderBy(u => u.ROL != null ? u.ROL.ROL_NOMBRE : "")
//                                   : src.OrderByDescending(u => u.ROL != null ? u.ROL.ROL_NOMBRE : "");
//                    case "estado":
//                        // Asc: Inactivo(false) -> Activo(true)
//                        return asc ? src.OrderBy(u => u.ESTADO)
//                                   : src.OrderByDescending(u => u.ESTADO);
//                    case "fecha":
//                    default:
//                        return asc ? src.OrderBy(u => u.FECHA_CREACION)
//                                   : src.OrderByDescending(u => u.FECHA_CREACION);
//                }
//            }
//            q = ApplyOrder(q);

//            // -------- 8) Paginación segura
//            vm.Page = vm.Page <= 0 ? 1 : vm.Page;
//            vm.PageSize = vm.PageSize <= 0 ? 10 : vm.PageSize;
//            var skip = (vm.Page - 1) * vm.PageSize;

//            vm.Items = await q.Skip(skip).Take(vm.PageSize).ToListAsync();

//            // -------- 9) TotalPages (para la vista)
//            vm.TotalPages = (int)Math.Ceiling((double)vm.TotalItems / vm.PageSize);

//            // -------- 10) Combo de roles (nombres legibles)
//            ViewBag.Roles = await _context.ROL
//                .AsNoTracking()
//                .OrderBy(r => r.ROL_NOMBRE)
//                .Select(r => r.ROL_NOMBRE)
//                .ToListAsync();

//            // -------- 11) Devolvemos la vista
//            vm.Usuario = usuarioFiltro; // por si tu VM lo necesita en la vista
//            return View(vm);
//        }



//        // GET: Usuarios/Details/5
//        public async Task<IActionResult> Details(string id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }

//            var uSUARIO = await _context.USUARIO
//                .Include(u => u.EMPLEADO)
//                .Include(u => u.ROL)
//                .FirstOrDefaultAsync(m => m.USUARIO_ID == id);
//            if (uSUARIO == null)
//            {
//                return NotFound();
//            }

//            return View(uSUARIO);
//        }

//        // GET: Usuarios/Create
//        public IActionResult Create()
//        {
//            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID");
//            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID");
//            return View();
//        }

//        // POST: Usuarios/Create
//        // To protect from overposting attacks, enable the specific properties you want to bind to.
//        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Create([Bind("USUARIO_ID,USUARIO_NOMBRE,USUARIO_CONTRASENA,USUARIO_SALT,USUARIO_FECHAREGISTRO,USUARIO_CAMBIOINICIAL,ROL_ID,EMPLEADO_ID,USUARIO_CORREO,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] USUARIO uSUARIO)
//        {
//            if (ModelState.IsValid)
//            {
//                _context.Add(uSUARIO);
//                await _context.SaveChangesAsync();
//                return RedirectToAction(nameof(Index));
//            }
//            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID", uSUARIO.EMPLEADO_ID);
//            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID", uSUARIO.ROL_ID);
//            return View(uSUARIO);
//        }

//        // GET: Usuarios/Edit/5
//        public async Task<IActionResult> Edit(string id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }

//            var uSUARIO = await _context.USUARIO.FindAsync(id);
//            if (uSUARIO == null)
//            {
//                return NotFound();
//            }
//            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID", uSUARIO.EMPLEADO_ID);
//            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID", uSUARIO.ROL_ID);
//            return View(uSUARIO);
//        }

//        // POST: Usuarios/Edit/5
//        // To protect from overposting attacks, enable the specific properties you want to bind to.
//        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Edit(string id, [Bind("USUARIO_ID,USUARIO_NOMBRE,USUARIO_CONTRASENA,USUARIO_SALT,USUARIO_FECHAREGISTRO,USUARIO_CAMBIOINICIAL,ROL_ID,EMPLEADO_ID,USUARIO_CORREO,USUARIO_CREACION,FECHA_CREACION,USUARIO_MODIFICACION,FECHA_MODIFICACION,ELIMINADO,USUARIO_ELIMINACION,FECHA_ELIMINACION,ESTADO")] USUARIO uSUARIO)
//        {
//            if (id != uSUARIO.USUARIO_ID)
//            {
//                return NotFound();
//            }

//            if (ModelState.IsValid)
//            {
//                try
//                {
//                    _context.Update(uSUARIO);
//                    await _context.SaveChangesAsync();
//                }
//                catch (DbUpdateConcurrencyException)
//                {
//                    if (!USUARIOExists(uSUARIO.USUARIO_ID))
//                    {
//                        return NotFound();
//                    }
//                    else
//                    {
//                        throw;
//                    }
//                }
//                return RedirectToAction(nameof(Index));
//            }
//            ViewData["EMPLEADO_ID"] = new SelectList(_context.EMPLEADO, "EMPLEADO_ID", "EMPLEADO_ID", uSUARIO.EMPLEADO_ID);
//            ViewData["ROL_ID"] = new SelectList(_context.ROL, "ROL_ID", "ROL_ID", uSUARIO.ROL_ID);
//            return View(uSUARIO);
//        }

//        // GET: Usuarios/Delete/5
//        public async Task<IActionResult> Delete(string id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }

//            var uSUARIO = await _context.USUARIO
//                .Include(u => u.EMPLEADO)
//                .Include(u => u.ROL)
//                .FirstOrDefaultAsync(m => m.USUARIO_ID == id);
//            if (uSUARIO == null)
//            {
//                return NotFound();
//            }

//            return View(uSUARIO);
//        }

//        // POST: Usuarios/Delete/5
//        [HttpPost, ActionName("Delete")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DeleteConfirmed(string id)
//        {
//            var uSUARIO = await _context.USUARIO.FindAsync(id);
//            if (uSUARIO != null)
//            {
//                _context.USUARIO.Remove(uSUARIO);
//            }

//            await _context.SaveChangesAsync();
//            return RedirectToAction(nameof(Index));
//        }

//        private bool USUARIOExists(string id)
//        {
//            return _context.USUARIO.Any(e => e.USUARIO_ID == id);
//        }
//    }
//}
