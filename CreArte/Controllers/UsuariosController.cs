// ===============================================
// RUTA: Controllers/UsuariosController.cs
// DESCRIPCIÓN: CRUD de USUARIO con filtros, orden,
// paginación y auditoría. Control de contraseñas
// con PBKDF2 + SALT (USUARIO_CONTRASENA, USUARIO_SALT).
// ===============================================
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Auditoria; // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public UsuariosController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – GET /Usuarios?Search=&Rol=&FechaInicio=&FechaFin=&Estado=&Sort=&Dir=&Page=&PageSize=
        // ▸ Filtros: búsqueda global (ID/Usuario/Empleado/Correo), Rol, rango de fecha de registro, Estado
        // ▸ Orden: id | usuario | fecha | rol | estado
        // ▸ Paginación
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? Search,
            string? Rol,
            DateTime? FechaInicio,       // rango de USUARIO_FECHAREGISTRO (>=)
            DateTime? FechaFin,          // rango de USUARIO_FECHAREGISTRO (<=)
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Base de consulta: usuarios no eliminados
            IQueryable<USUARIO> q = _context.USUARIO
                .AsNoTracking()
                .Where(u => !u.ELIMINADO);

            // 2) Búsqueda global
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(u =>
                    EF.Functions.Like(u.USUARIO_ID, $"%{s}%") ||
                    EF.Functions.Like(u.USUARIO_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(u.USUARIO_CORREO ?? "", $"%{s}%") ||
                    // Nombre de rol
                    EF.Functions.Like(u.ROL.ROL_NOMBRE, $"%{s}%") ||
                    // Nombre completo de empleado (concatenado)
                    EF.Functions.Like(
                        (u.EMPLEADO.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (u.EMPLEADO.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (u.EMPLEADO.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (u.EMPLEADO.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (u.EMPLEADO.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (u.EMPLEADO.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? ""),
                        $"%{s}%"
                    )
                );
            }

            // 3) Filtro por Rol (acepta ID o nombre parcial)
            if (!string.IsNullOrWhiteSpace(Rol))
            {
                string r = Rol.Trim();
                q = q.Where(u =>
                    u.ROL_ID == r ||
                    EF.Functions.Like(u.ROL.ROL_NOMBRE, $"%{r}%"));
            }

            // 4) Rango de fecha de registro (USUARIO_FECHAREGISTRO)
            if (FechaInicio.HasValue)
            {
                var fi = FechaInicio.Value.Date;
                q = q.Where(u => u.USUARIO_FECHAREGISTRO >= fi);
            }
            if (FechaFin.HasValue)
            {
                var ff = FechaFin.Value.Date.AddDays(1).AddTicks(-1); // hasta el final del día
                q = q.Where(u => u.USUARIO_FECHAREGISTRO <= ff);
            }

            // 5) Estado
            if (Estado.HasValue)
                q = q.Where(u => u.ESTADO == Estado.Value);

            // 6) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(u => u.USUARIO_ID) : q.OrderByDescending(u => u.USUARIO_ID),
                "usuario" => asc ? q.OrderBy(u => u.USUARIO_NOMBRE) : q.OrderByDescending(u => u.USUARIO_NOMBRE),
                "fecha" => asc ? q.OrderBy(u => u.USUARIO_FECHAREGISTRO) : q.OrderByDescending(u => u.USUARIO_FECHAREGISTRO),
                "rol" => asc ? q.OrderBy(u => u.ROL.ROL_NOMBRE) : q.OrderByDescending(u => u.ROL.ROL_NOMBRE),
                "estado" => asc ? q.OrderBy(u => u.ESTADO) : q.OrderByDescending(u => u.ESTADO),
                _ => asc ? q.OrderBy(u => u.FECHA_CREACION) : q.OrderByDescending(u => u.FECHA_CREACION),
            };

            // 7) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // 8) Include para mostrar datos de rol y empleado(persona)
            var items = await q
                .Include(u => u.ROL)
                .Include(u => u.EMPLEADO).ThenInclude(e => e.EMPLEADONavigation)
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var vm = new UsuarioViewModels
            {
                Items = items,
                Search = Search,
                Rol = Rol,
                FechaInicio = FechaInicio,
                FechaFin = FechaFin,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = Page,
                PageSize = PageSize,
                TotalItems = total,
                TotalPages = totalPages
            };

            return View(vm);
        }

        // ============================================================
        // DETAILS (Partial para modal) – GET /Usuarios/DetailsCard?id=...
        // ▸ Devuelve datos del usuario + rol + empleado (nombre completo)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.USUARIO
                .AsNoTracking()
                .Include(u => u.ROL)
                .Include(u => u.EMPLEADO).ThenInclude(e => e.EMPLEADONavigation)
                .Where(u => u.USUARIO_ID == id && !u.ELIMINADO)
                .Select(u => new UsuarioDetailsVM
                {
                    USUARIO_ID = u.USUARIO_ID,
                    USUARIO_NOMBRE = u.USUARIO_NOMBRE,
                    USUARIO_CORREO = u.USUARIO_CORREO,
                    USUARIO_FECHAREGISTRO = u.USUARIO_FECHAREGISTRO,
                    USUARIO_CAMBIOINICIAL = u.USUARIO_CAMBIOINICIAL,
                    ESTADO = u.ESTADO,

                    ROL_ID = u.ROL_ID,
                    ROL_NOMBRE = u.ROL.ROL_NOMBRE,

                    EMPLEADO_ID = u.EMPLEADO_ID,
                    EMPLEADO_NOMBRE_COMPLETO =
                        (
                            (u.EMPLEADO.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                            (u.EMPLEADO.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                            (u.EMPLEADO.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                            (u.EMPLEADO.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                            (u.EMPLEADO.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                            (u.EMPLEADO.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? "")
                        ).Trim(),

                    // Auditoría
                    FECHA_CREACION = u.FECHA_CREACION,
                    USUARIO_CREACION = u.USUARIO_CREACION,
                    FECHA_MODIFICACION = u.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = u.USUARIO_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm); // Views/Usuarios/Details.cshtml (parcial)
        }

        // ============================================================
        // CREATE (GET) – GET /Usuarios/Create
        // ▸ Genera ID (US + 8 dígitos) y carga combos Rol/Empleado
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var nextId = await SiguienteUsuarioIdAsync();

            var vm = new UsuarioCreateVM
            {
                USUARIO_ID = nextId,
                ESTADO = true,
                USUARIO_CAMBIOINICIAL = true, // por política: forzar cambio inicial

                Roles = await CargarRolesAsync(),
                Empleados = await CargarEmpleadosAsync()
            };

            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – POST /Usuarios/Create
        // ▸ Valida: unicidad de USUARIO_NOMBRE, FKs activos/no eliminados,
        //   contraseña con política y confirmación, correo válido (si viene).
        // ▸ Genera SALT y HASH PBKDF2 (HMACSHA256).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsuarioCreateVM vm)
        {
            // Recalcular/asegurar ID del servidor
            vm.USUARIO_ID = await SiguienteUsuarioIdAsync();

            // ---------- VALIDACIONES ----------
            if (string.IsNullOrWhiteSpace(vm.USUARIO_NOMBRE))
                ModelState.AddModelError(nameof(vm.USUARIO_NOMBRE), "El nombre de usuario es obligatorio.");
            else if (!Regex.IsMatch(vm.USUARIO_NOMBRE, @"^[a-zA-Z0-9_.-]{3,50}$"))
                ModelState.AddModelError(nameof(vm.USUARIO_NOMBRE), "El usuario debe tener de 3 a 50 caracteres y usar solo letras, números, punto, guion o guion bajo.");

            // Unicidad de usuario
            if (!string.IsNullOrWhiteSpace(vm.USUARIO_NOMBRE))
            {
                bool dup = await _context.USUARIO.AnyAsync(u => !u.ELIMINADO && u.USUARIO_NOMBRE == vm.USUARIO_NOMBRE.Trim());
                if (dup) ModelState.AddModelError(nameof(vm.USUARIO_NOMBRE), "Ya existe un usuario con ese nombre.");
            }

            // Contraseña + confirmación
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "La contraseña es obligatoria.");
            else
            {
                var pwdErr = ValidatePasswordPolicy(vm.Password);
                if (!string.IsNullOrEmpty(pwdErr))
                    ModelState.AddModelError(nameof(vm.Password), pwdErr);
            }

            if (vm.Password != vm.ConfirmPassword)
                ModelState.AddModelError(nameof(vm.ConfirmPassword), "La confirmación no coincide.");

            // Validar FK Rol (activo y no eliminado)
            bool rolOk = !string.IsNullOrWhiteSpace(vm.ROL_ID)
                         && await _context.ROL.AnyAsync(r => r.ROL_ID == vm.ROL_ID && !r.ELIMINADO && r.ESTADO);
            if (!rolOk)
                ModelState.AddModelError(nameof(vm.ROL_ID), "El rol seleccionado no existe o no está activo.");

            // Validar FK Empleado (activo y no eliminado)
            bool empOk = !string.IsNullOrWhiteSpace(vm.EMPLEADO_ID)
                         && await _context.EMPLEADO.AnyAsync(e => e.EMPLEADO_ID == vm.EMPLEADO_ID && !e.ELIMINADO && e.ESTADO);
            if (!empOk)
                ModelState.AddModelError(nameof(vm.EMPLEADO_ID), "El empleado seleccionado no existe o no está activo.");

            // Correo (opcional, si viene validar formato básico)
            if (!string.IsNullOrWhiteSpace(vm.USUARIO_CORREO))
            {
                if (!Regex.IsMatch(vm.USUARIO_CORREO.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    ModelState.AddModelError(nameof(vm.USUARIO_CORREO), "Formato de correo no válido.");
            }

            if (!ModelState.IsValid)
            {
                vm.Roles = await CargarRolesAsync();
                vm.Empleados = await CargarEmpleadosAsync();
                return View(vm);
            }

            // Normalización
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Generar salt + hash PBKDF2
            var (hash, salt) = HashPasswordPBKDF2(vm.Password!);

            try
            {
                var u = new USUARIO
                {
                    USUARIO_ID = vm.USUARIO_ID!,
                    USUARIO_NOMBRE = vm.USUARIO_NOMBRE!.Trim(),
                    USUARIO_CONTRASENA = hash,
                    USUARIO_SALT = salt,
                    USUARIO_FECHAREGISTRO = DateTime.Now,
                    USUARIO_CAMBIOINICIAL = vm.USUARIO_CAMBIOINICIAL,
                    ROL_ID = vm.ROL_ID!,
                    EMPLEADO_ID = vm.EMPLEADO_ID!,
                    USUARIO_CORREO = s2(vm.USUARIO_CORREO),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(u);

                _context.USUARIO.Add(u);
                await _context.SaveChangesAsync();

                TempData["SwalTitle"] = "¡Usuario creado!";
                TempData["SwalText"] = $"El usuario \"{u.USUARIO_NOMBRE}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "Usuarios");
                TempData["SwalCreateUrl"] = Url.Action("Create", "Usuarios");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                ModelState.AddModelError("", "Ocurrió un error al crear el usuario. Intenta nuevamente.");
                vm.Roles = await CargarRolesAsync();
                vm.Empleados = await CargarEmpleadosAsync();
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – GET /Usuarios/Edit/{id}
        // ▸ Carga el usuario y combos. (La contraseña no se muestra)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var u = await _context.USUARIO
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.USUARIO_ID == id && !x.ELIMINADO);

            if (u == null) return NotFound();

            var vm = new UsuarioCreateVM
            {
                USUARIO_ID = u.USUARIO_ID,
                USUARIO_NOMBRE = u.USUARIO_NOMBRE,
                USUARIO_CORREO = u.USUARIO_CORREO,
                USUARIO_CAMBIOINICIAL = u.USUARIO_CAMBIOINICIAL,
                ROL_ID = u.ROL_ID,
                EMPLEADO_ID = u.EMPLEADO_ID,
                ESTADO = u.ESTADO,

                Roles = await CargarRolesAsync(),
                Empleados = await CargarEmpleadosAsync()
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – POST /Usuarios/Edit/{id}
        // ▸ Actualiza datos generales, rol, empleado, correo, estado.
        // ▸ Si viene nueva contraseña, la valida y re-hashea.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UsuarioCreateVM vm)
        {
            if (id != vm.USUARIO_ID) return NotFound();

            var u = await _context.USUARIO.FirstOrDefaultAsync(x => x.USUARIO_ID == id && !x.ELIMINADO);
            if (u == null) return NotFound();

            // Validar usuario (puede cambiar USUARIO_NOMBRE si lo permites; aquí lo permitimos)
            if (string.IsNullOrWhiteSpace(vm.USUARIO_NOMBRE))
                ModelState.AddModelError(nameof(vm.USUARIO_NOMBRE), "El nombre de usuario es obligatorio.");
            else if (!Regex.IsMatch(vm.USUARIO_NOMBRE, @"^[a-zA-Z0-9_.-]{3,50}$"))
                ModelState.AddModelError(nameof(vm.USUARIO_NOMBRE), "El usuario debe tener de 3 a 50 caracteres y usar solo letras, números, punto, guion o guion bajo.");

            // Unicidad excluyendo el propio
            if (!string.IsNullOrWhiteSpace(vm.USUARIO_NOMBRE))
            {
                bool dup = await _context.USUARIO.AnyAsync(x =>
                    !x.ELIMINADO &&
                    x.USUARIO_NOMBRE == vm.USUARIO_NOMBRE.Trim() &&
                    x.USUARIO_ID != id);
                if (dup) ModelState.AddModelError(nameof(vm.USUARIO_NOMBRE), "Ya existe un usuario con ese nombre.");
            }

            // Correo opcional
            if (!string.IsNullOrWhiteSpace(vm.USUARIO_CORREO))
            {
                if (!Regex.IsMatch(vm.USUARIO_CORREO.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    ModelState.AddModelError(nameof(vm.USUARIO_CORREO), "Formato de correo no válido.");
            }

            // Rol
            bool rolOk = !string.IsNullOrWhiteSpace(vm.ROL_ID)
                         && await _context.ROL.AnyAsync(r => r.ROL_ID == vm.ROL_ID && !r.ELIMINADO && r.ESTADO);
            if (!rolOk)
                ModelState.AddModelError(nameof(vm.ROL_ID), "El rol seleccionado no existe o no está activo.");

            // Empleado
            bool empOk = !string.IsNullOrWhiteSpace(vm.EMPLEADO_ID)
                         && await _context.EMPLEADO.AnyAsync(e => e.EMPLEADO_ID == vm.EMPLEADO_ID && !e.ELIMINADO && e.ESTADO);
            if (!empOk)
                ModelState.AddModelError(nameof(vm.EMPLEADO_ID), "El empleado seleccionado no existe o no está activo.");

            // Si trae nueva contraseña, validar política y confirmación
            bool willChangePwd = !string.IsNullOrWhiteSpace(vm.Password) || !string.IsNullOrWhiteSpace(vm.ConfirmPassword);
            if (willChangePwd)
            {
                if (string.IsNullOrWhiteSpace(vm.Password))
                    ModelState.AddModelError(nameof(vm.Password), "La contraseña es obligatoria si desea cambiarla.");
                else
                {
                    var pwdErr = ValidatePasswordPolicy(vm.Password!);
                    if (!string.IsNullOrEmpty(pwdErr))
                        ModelState.AddModelError(nameof(vm.Password), pwdErr);
                }

                if (vm.Password != vm.ConfirmPassword)
                    ModelState.AddModelError(nameof(vm.ConfirmPassword), "La confirmación no coincide.");
            }

            if (!ModelState.IsValid)
            {
                vm.Roles = await CargarRolesAsync();
                vm.Empleados = await CargarEmpleadosAsync();
                return View(vm);
            }

            // Normalización
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Aplicar cambios
            u.USUARIO_NOMBRE = vm.USUARIO_NOMBRE!.Trim();
            u.USUARIO_CORREO = s2(vm.USUARIO_CORREO);
            u.ROL_ID = vm.ROL_ID!;
            u.EMPLEADO_ID = vm.EMPLEADO_ID!;
            u.ESTADO = vm.ESTADO;
            u.USUARIO_CAMBIOINICIAL = vm.USUARIO_CAMBIOINICIAL;

            // Cambio de contraseña (opcional)
            if (willChangePwd && !string.IsNullOrWhiteSpace(vm.Password))
            {
                var (hash, salt) = HashPasswordPBKDF2(vm.Password!);
                u.USUARIO_CONTRASENA = hash;
                u.USUARIO_SALT = salt;
                // Al cambiar contraseña desde admin podrías forzar CAMBIOINICIAL=true si así lo deseas
                // u.USUARIO_CAMBIOINICIAL = true;
            }

            _audit.StampUpdate(u);
            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Usuario actualizado!";
            TempData["SwalText"] = $"\"{u.USUARIO_NOMBRE}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = u.USUARIO_ID });
        }

        // ============================================================
        // DELETE – GET /Usuarios/Delete/{id}  y  POST /Usuarios/Delete/{id}
        // ▸ Borrado lógico (no se elimina físicamente)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var u = await _context.USUARIO
                .AsNoTracking()
                .Include(x => x.ROL)
                .Include(x => x.EMPLEADO).ThenInclude(e => e.EMPLEADONavigation)
                .FirstOrDefaultAsync(x => x.USUARIO_ID == id);

            if (u == null) return NotFound();

            return View(u);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var u = await _context.USUARIO.FindAsync(id);
            if (u == null) return NotFound();

            // 1) Borrado lógico + auditoría
            _audit.StampSoftDelete(u);
            await _context.SaveChangesAsync();

            // 2) PRG: marcar TempData para que el Index muestre SweetAlert
            TempData["SwalOneBtnFlag"] = "deleted";                      // <- bandera para el Index
            TempData["SwalTitle"] = "¡Usuario eliminado!";               // título del swal
            TempData["SwalText"] = $"\"{u.USUARIO_NOMBRE}\" se eliminó correctamente."; // texto

            // 3) Volver al listado (PRG)
            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo US00000001 para USUARIO
        private async Task<string> SiguienteUsuarioIdAsync()
        {
            const string prefijo = "US";
            const int ancho = 8;

            var ids = await _context.USUARIO
                .Select(x => x.USUARIO_ID)
                .Where(id => id.StartsWith(prefijo))
                .ToListAsync();

            int maxNum = 0;
            var rx = new Regex(@"^" + prefijo + @"(?<n>\d+)$");
            foreach (var id in ids)
            {
                var m = rx.Match(id);
                if (m.Success && int.TryParse(m.Groups["n"].Value, out int n))
                    if (n > maxNum) maxNum = n;
            }

            var siguiente = maxNum + 1;
            return prefijo + siguiente.ToString(new string('0', ancho));
        }

        // Carga de Roles activos y no eliminados (para combo)
        private async Task<List<SelectListItem>> CargarRolesAsync()
        {
            return await _context.ROL
                .Where(r => !r.ELIMINADO && r.ESTADO)
                .OrderBy(r => r.ROL_NOMBRE)
                .Select(r => new SelectListItem
                {
                    Text = r.ROL_NOMBRE,
                    Value = r.ROL_ID
                })
                .ToListAsync();
        }

        // Carga de Empleados activos y no eliminados (para combo)
        // ▸ La opción Text será el nombre completo (6 partes)
        private async Task<List<SelectListItem>> CargarEmpleadosAsync()
        {
            return await _context.EMPLEADO
                .Where(e => !e.ELIMINADO && e.ESTADO)
                .OrderBy(e => e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE)
                .Select(e => new SelectListItem
                {
                    Text =
                        (
                            (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? "")
                        ).Trim(),
                    Value = e.EMPLEADO_ID
                })
                .ToListAsync();
        }

        // Política de contraseñas (mín. 8: mayúscula, minúscula, dígito, símbolo)
        private string? ValidatePasswordPolicy(string pwd)
        {
            if (pwd.Length < 8) return "La contraseña debe tener al menos 8 caracteres.";
            if (!pwd.Any(char.IsUpper)) return "La contraseña debe incluir al menos una letra mayúscula.";
            if (!pwd.Any(char.IsLower)) return "La contraseña debe incluir al menos una letra minúscula.";
            if (!pwd.Any(char.IsDigit)) return "La contraseña debe incluir al menos un dígito.";
            if (!Regex.IsMatch(pwd, @"[^a-zA-Z0-9]")) return "La contraseña debe incluir al menos un símbolo.";
            return null;
        }

        // Hash PBKDF2 con HMACSHA256 + SALT (64 bytes) y 100,000 iteraciones
        private (byte[] hash, byte[] salt) HashPasswordPBKDF2(string password, byte[]? existingSalt = null)
        {
            byte[] salt = existingSalt ?? RandomNumberGenerator.GetBytes(64); // 64 bytes
            // Derivación de clave (hash) con 100k iteraciones
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32); // 256 bits
            return (hash, salt);
        }
    }
}
