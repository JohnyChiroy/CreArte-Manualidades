using CreArte.Data; // CreArteDbContext
using CreArte.Models; // Entidades EF
using CreArte.ModelsPartial; // ViewModels: LoginViewModels, CambiarContrasenaViewModel, RecuperacionContrasenaVM, RestablecerContrasenaVM
using CreArte.Services.Auditoria; // ICurrentUserService y AuditoriaService (si fuera necesario)
using CreArte.Services.Mail;  // IEmailSender, EmailTemplates
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc; // Controller, IActionResult
using Microsoft.EntityFrameworkCore; // FirstOrDefaultAsync, AsNoTracking
using System.Security.Claims; // Claim, ClaimsIdentity, ClaimsPrincipal
using System.Security.Cryptography; // RandomNumberGenerator, SHA256, Rfc2898DeriveBytes
using System.Text; // Encoding.UTF8
using Microsoft.Extensions.Options; // IOptions<AppSettings>
using System.Threading.Tasks; // Task<IActionResult>
using System; // DateTime
using System.ComponentModel.DataAnnotations; // EmailAddressAttribute
using Microsoft.AspNetCore.WebUtilities; // WebEncoders
using System.Linq;
using Microsoft.AspNetCore.Authorization; // Linq

namespace CreArte.Controllers
{
    public class LoginController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly AppSettings _app; // Para BaseUrl (links en correos)

        public LoginController(CreArteDbContext context, IOptions<AppSettings> appOptions)
        {
            _context = context;
            _app = appOptions.Value;
        }

        [AllowAnonymous] // Permite acceso sin autenticar
        public IActionResult Login()
        {
            return View(); // Views/Login/Login.cshtml
        }

        [HttpPost]
        [AllowAnonymous] // Permite acceso sin autenticar
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModels model)
        {
            // 1) Validación de modelo
            if (!ModelState.IsValid)
                return View(model);

            // 2) Buscar usuario (no eliminado)
            var usuario = await _context.USUARIO
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == model.USUARIO_NOMBRE && u.ELIMINADO == false);

            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "El usuario ingresado no existe.");
                return View(model);
            }

            // 3) Validar estado activo
            if (!usuario.ESTADO)
            {
                ModelState.AddModelError(string.Empty, "El usuario está deshabilitado. Contacte al administrador.");
                return View(model);
            }

            // 4) Validar que tenga credenciales configuradas
            if (usuario.USUARIO_SALT == null || usuario.USUARIO_CONTRASENA == null)
            {
                ModelState.AddModelError(string.Empty, "El usuario no tiene credenciales configuradas. Contacte al administrador.");
                return View(model);
            }

            // 5) Verificar contraseña con PBKDF2 (actual) y fallback LEGADO (SHA-256(SALT||PWD))
            bool okPassword = false;

            // Intento A: PBKDF2 (HMACSHA256, 100k iteraciones, hash 32 bytes)
            byte[] tryPbkdf2 = ComputePBKDF2(model.USUARIO_CONTRASENA, usuario.USUARIO_SALT);
            okPassword = FixedTimeEquals(tryPbkdf2, usuario.USUARIO_CONTRASENA);

            // Intento B (si falla): esquema legado SHA-256(SALT || UTF8(password))
            bool matchedByLegacy = false;
            if (!okPassword)
            {
                var tryLegacy = HashSha256Salted(usuario.USUARIO_SALT, model.USUARIO_CONTRASENA);
                okPassword = FixedTimeEquals(tryLegacy, usuario.USUARIO_CONTRASENA);
                matchedByLegacy = okPassword;
            }

            if (!okPassword)
            {
                ModelState.AddModelError(string.Empty, "La contraseña es incorrecta.");
                return View(model);
            }

            // (OPCIONAL) Migración silenciosa: si coincidió por LEGACY, rehasheamos en PBKDF2 y guardamos.
            if (matchedByLegacy)
            {
                try
                {
                    var newSalt = GenerateSalt(64);
                    var newHash = ComputePBKDF2(model.USUARIO_CONTRASENA, newSalt);
                    var u = await _context.USUARIO.FirstAsync(x => x.USUARIO_ID == usuario.USUARIO_ID);
                    u.USUARIO_SALT = newSalt;
                    u.USUARIO_CONTRASENA = newHash;
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Si falla la migración, NO bloqueamos el login (simplemente seguimos).
                }
            }

            //// 6) ¿Debe cambiar contraseña?
            //if (usuario.USUARIO_CAMBIOINICIAL == true)
            //{
            //    // Guardamos ID en sesión para que la vista de cambio lo use
            //    HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID);
            //    HttpContext.Session.SetString("UsuarioNombre", usuario.USUARIO_NOMBRE);
            //    return RedirectToAction("CambiarContrasena", "Login");
            //}

            //// 7) SESIÓN (opcional para mostrar nombre en UI)
            //HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID);
            //HttpContext.Session.SetString("Nombre", usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID);




            //// 8) AUTENTICACIÓN POR COOKIES (claims)
            //var claims = new List<Claim>
            //{
            //    new Claim(ClaimTypes.NameIdentifier, usuario.USUARIO_ID),
            //    new Claim(ClaimTypes.Name, usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID),
            //    new Claim("preferred_username", usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID)
            //};
            //if (!string.IsNullOrWhiteSpace(usuario.USUARIO_CORREO))
            //    claims.Add(new Claim(ClaimTypes.Email, usuario.USUARIO_CORREO));

            //// (Opcional) Agregar rol como claim
            //// if (!string.IsNullOrWhiteSpace(usuario.ROL_ID))
            ////     claims.Add(new Claim(ClaimTypes.Role, usuario.ROL_ID));

            //var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            //var principal = new ClaimsPrincipal(identity);

            //var authProps = new AuthenticationProperties
            //{
            //    // IsPersistent = model.Recordarme, // si agregas "Recordarme"
            //    // ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            //    // AllowRefresh = true
            //};

            //// Emite el cookie de autenticación
            //await HttpContext.SignInAsync(
            //    CookieAuthenticationDefaults.AuthenticationScheme,
            //    principal,
            //    authProps
            //);

            //// 9) OK → redirigimos a Home (o donde quieras)
            //return RedirectToAction("Index", "Home");
            // 6) ¿Debe cambiar contraseña?
            if (usuario.USUARIO_CAMBIOINICIAL == true)
            {
                HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID);
                HttpContext.Session.SetString("UsuarioNombre", usuario.USUARIO_NOMBRE);
                return RedirectToAction("CambiarContrasena", "Login");
            }

            // 7) SESIÓN opcional
            HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID);
            HttpContext.Session.SetString("Nombre", usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID);

            // ====== OBTENER ROL (USUARIO.ROL_ID) ======
            string? rolId = await _context.USUARIO
                .Where(u => u.USUARIO_ID == usuario.USUARIO_ID)
                .Select(u => u.ROL_ID) // <-- ajuste si su columna se llama distinto
                .FirstOrDefaultAsync();

            // 8) AUTENTICACIÓN POR COOKIES (claims)
            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.USUARIO_ID),
                    new Claim(ClaimTypes.Name, usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID),
                    new Claim("preferred_username", usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID)
                };
            if (!string.IsNullOrWhiteSpace(usuario.USUARIO_CORREO))
                claims.Add(new Claim(ClaimTypes.Email, usuario.USUARIO_CORREO));

            if (!string.IsNullOrWhiteSpace(rolId))
            {
                claims.Add(new Claim(ClaimTypes.Role, rolId)); // estándar
                claims.Add(new Claim("rol_id", rolId));        // custom minúsculas
                claims.Add(new Claim("ROL_ID", rolId));        // ⬅️ ADICIONAL: lo pide su filtro
                HttpContext.Session.SetString("RolId", rolId);
            }


            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name,
                ClaimTypes.Role // mapea este tipo como "role"
            );
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                // IsPersistent = model.Recordarme,
                // ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProps
            );

            return RedirectToAction("Index", "Home");
        }

        // ============================================
        // GET: /Login/Logout
        // Cierra sesión: limpia sesión y cookie de auth.
        // ============================================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            HttpContext.Response.Cookies.Delete(".AspNetCore.Session");
            return RedirectToAction("Login", "Login");
        }

        // ============================================
        // GET: /Login/CambiarContrasena
        // Vista para cambiar contraseña tras "cambio inicial".
        // ============================================
        [AllowAnonymous] // Permite acceso sin autenticar
        public IActionResult CambiarContrasena()
        {
            var userId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login");

            return View(); // Views/Login/CambiarContrasena.cshtml
        }

        // ============================================
        // POST: /Login/CambiarContrasena
        // ► Ahora guarda con PBKDF2 (igual que UsuariosController).
        // ============================================
        [HttpPost]
        [AllowAnonymous] // Permite acceso sin autenticar
        [ValidateAntiForgeryToken]
        public IActionResult CambiarContrasena(CambiarContrasenaViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login");

            var usuario = _context.USUARIO.FirstOrDefault(u => u.USUARIO_ID == userId && u.ELIMINADO == false);
            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "No se encontró el usuario.");
                return View(model);
            }

            // ► Nuevo SALT + PBKDF2
            byte[] nuevoSalt = GenerateSalt(64);
            byte[] nuevoHash = ComputePBKDF2(model.NuevaContrasena, nuevoSalt);

            usuario.USUARIO_SALT = nuevoSalt;
            usuario.USUARIO_CONTRASENA = nuevoHash;
            usuario.USUARIO_CAMBIOINICIAL = false;

            _context.SaveChanges();

            TempData["Mensaje"] = "Contraseña actualizada correctamente. Ingresa tus credenciales para tu primer inicio de sesión.";
            return RedirectToAction("Login", "Login");
        }

        // =====================================================================
        // ====================== AQUÍ EMPIEZA LO DEL CORREO ====================
        // =====================================================================

        // GET: /Login/EnvioDeCorreo
        [AllowAnonymous] // Permite acceso sin autenticar
        [HttpGet]
        public IActionResult EnvioDeCorreo()
        {
            return View(new RecuperacionContrasenaVM()); // Views/Login/EnvioDeCorreo.cshtml
        }

        // POST: /Login/EnvioDeCorreo (envía correo con enlace)
        [HttpPost]
        [AllowAnonymous] // Permite acceso sin autenticar
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnvioDeCorreo(
            RecuperacionContrasenaVM vm,
            [FromServices] IEmailSender mail,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["ResetError"] = "Ingrese su usuario o correo.";
                return View(vm);
            }

            var input = (vm.UsuarioONCorreo ?? "").Trim().ToLowerInvariant();
            bool esEmail = new EmailAddressAttribute().IsValid(input);

            PERSONA? persona = null;
            EMPLEADO? empleado = null;
            USUARIO? usuario = null;

            if (esEmail)
            {
                persona = await _context.PERSONA
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => !p.ELIMINADO && p.PERSONA_CORREO.ToLower() == input, ct);

                if (persona == null)
                {
                    TempData["ResetError"] = "El correo no está registrado.";
                    return View(vm);
                }
                if (!persona.ESTADO) // bool
                {
                    TempData["ResetError"] = "La persona asociada está INACTIVA.";
                    return View(vm);
                }

                empleado = await _context.EMPLEADO
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => !e.ELIMINADO && e.EMPLEADO_ID == persona.PERSONA_ID, ct);

                if (empleado == null)
                {
                    TempData["ResetError"] = "No hay empleado asociado al correo.";
                    return View(vm);
                }
                if (!empleado.ESTADO) // bool
                {
                    TempData["ResetError"] = "El empleado está INACTIVO.";
                    return View(vm);
                }

                usuario = await _context.USUARIO
                    .FirstOrDefaultAsync(u => !u.ELIMINADO && u.EMPLEADO_ID == empleado.EMPLEADO_ID, ct);
            }
            else
            {
                usuario = await _context.USUARIO
                    .FirstOrDefaultAsync(u => !u.ELIMINADO && u.USUARIO_NOMBRE.ToLower() == input, ct);

                if (usuario == null)
                {
                    TempData["ResetError"] = "Usuario no encontrado.";
                    return View(vm);
                }
                if (!usuario.ESTADO) // bool
                {
                    TempData["ResetError"] = "El usuario está INACTIVO.";
                    return View(vm);
                }

                empleado = await _context.EMPLEADO
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => !e.ELIMINADO && e.EMPLEADO_ID == usuario.EMPLEADO_ID, ct);

                if (empleado == null)
                {
                    TempData["ResetError"] = "Usuario sin empleado asociado.";
                    return View(vm);
                }

                persona = await _context.PERSONA
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => !p.ELIMINADO && p.PERSONA_ID == empleado.EMPLEADO_ID, ct);

                if (persona == null || string.IsNullOrWhiteSpace(persona.PERSONA_CORREO))
                {
                    TempData["ResetError"] = "No se encontró correo para el usuario.";
                    return View(vm);
                }
            }

            if (usuario == null)
            {
                TempData["ResetError"] = "No se encontró el usuario ligado.";
                return View(vm);
            }
            if (!usuario.ESTADO) // bool
            {
                TempData["ResetError"] = "El usuario está INACTIVO.";
                return View(vm);
            }

            // 2) Invalidar tokens anteriores activos (marcar como eliminados)
            var ahora = DateTime.Now;
            var creadoPor = usuario.USUARIO_NOMBRE ?? "SYSTEM";

            var prevTokens = await _context.TOKEN_RECUPERACION
                .Where(t => t.USUARIO_ID == usuario.USUARIO_ID
                            && !t.ELIMINADO
                            && !t.TOKEN_USADO
                            && t.TOKEN_EXPIRA > ahora)
                .ToListAsync(ct);

            foreach (var t in prevTokens)
            {
                t.ELIMINADO = true;
                t.USUARIO_ELIMINACION = creadoPor;
                t.FECHA_ELIMINACION = ahora;
                t.USUARIO_MODIFICACION = creadoPor;
                t.FECHA_MODIFICACION = ahora;
            }
            if (prevTokens.Count > 0) await _context.SaveChangesAsync(ct);

            // 3) Generar token crudo URL-safe y guardar solo HASH en BD
            var rawBytes = RandomNumberGenerator.GetBytes(32);
            var rawToken = WebEncoders.Base64UrlEncode(rawBytes);
            var tokenHashHex = Sha256Hex(rawToken);

            var expira = ahora.AddHours(1);

            var tk = new TOKEN_RECUPERACION
            {
                TOKEN_ID = GenerarTokenIdCorto(),
                USUARIO_ID = usuario.USUARIO_ID,
                TOKEN_VALOR = tokenHashHex,   // HASH HEX
                TOKEN_EXPIRA = expira,
                TOKEN_USADO = false,
                USADO_EN = null,

                // Auditoría
                USUARIO_CREACION = creadoPor,
                FECHA_CREACION = ahora,
                USUARIO_MODIFICACION = null,
                FECHA_MODIFICACION = null,
                ELIMINADO = false,
                USUARIO_ELIMINACION = null,
                FECHA_ELIMINACION = null,
                ESTADO = true // si su campo es bool; si es string, cámbielo por "ACTIVO"
            };

            _context.TOKEN_RECUPERACION.Add(tk);
            await _context.SaveChangesAsync(ct);

            // 4) URL absoluta al endpoint de restablecimiento (LOCAL: usa el host+puerto actuales)
            //    Incluimos PathBase por si su app corre bajo subruta.
            var relative = Url.Action(nameof(RestablecerContrasena), "Login", new { token = rawToken })!;
            var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{relative}";

            // 5) Armar y enviar correo
            var nombre = $"{persona?.PERSONA_PRIMERNOMBRE} {persona?.PERSONA_PRIMERAPELLIDO}".Trim();
            var logoUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Imagenes/logoCreArte.png";

            var html = EmailTemplates.BuildRecoveryEmailHtml(nombre, url, expira, logoUrl);
            try
            {
                await mail.SendAsync(persona!.PERSONA_CORREO, "Recupera tu contraseña — CreArte", html);
            }
            catch (Exception) // Si desea, capture SmtpException en específico
            {
                TempData["ResetError"] = "No se pudo enviar el correo en este momento. Intente nuevamente.";
                return View(vm);
            }

            TempData["ResetInfo"] = "Te enviamos un enlace de recuperación a tu correo.";
            return RedirectToAction(nameof(EnvioDeCorreo));
        }


        // GET: /Login/RestablecerContrasena?token=...
        [AllowAnonymous] // Permite acceso sin autenticar
        [HttpGet]
        public async Task<IActionResult> RestablecerContrasena(string token, CancellationToken ct)
        {
            const string err = "Tu enlace de recuperación ha expirado o no es válido. Solicita uno nuevo.";

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["LoginError"] = err;
                return RedirectToAction("Login");
            }

            var tokenHashHex = Sha256Hex(token);
            var ahora = DateTime.Now;

            var tk = await _context.TOKEN_RECUPERACION
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.TOKEN_VALOR == tokenHashHex &&
                    !t.ELIMINADO &&
                    !t.TOKEN_USADO &&
                    t.TOKEN_EXPIRA >= ahora, ct);

            if (tk == null)
            {
                TempData["LoginError"] = err;
                return RedirectToAction("Login");
            }

            ViewBag.RecoveryToken = token; // token crudo para el POST
            return View(new RestablecerContrasenaVM()); // Views/Login/RestablecerContrasena.cshtml
        }

        // POST: /Login/RestablecerContrasenaPorToken
        [HttpPost]
        [AllowAnonymous] // Permite acceso sin autenticar
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerContrasenaPorToken(
            RestablecerContrasenaVM model, string token, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.RecoveryToken = token;
                return View("RestablecerContrasena", model);
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Mensaje"] = "El enlace de recuperación no es válido o ha expirado.";
                return RedirectToAction("Login");
            }

            var tokenHashHex = Sha256Hex(token);
            var ahora = DateTime.Now;

            var tk = await _context.TOKEN_RECUPERACION
                .FirstOrDefaultAsync(t =>
                    t.TOKEN_VALOR == tokenHashHex &&
                    !t.ELIMINADO &&
                    !t.TOKEN_USADO &&
                    t.TOKEN_EXPIRA >= ahora, ct);

            if (tk == null)
            {
                TempData["Mensaje"] = "El enlace de recuperación no es válido o ha expirado.";
                return RedirectToAction("Login");
            }

            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u => !u.ELIMINADO && u.USUARIO_ID == tk.USUARIO_ID, ct);

            if (usuario == null)
            {
                TempData["Mensaje"] = "No se pudo completar la recuperación.";
                return RedirectToAction("Login");
            }

            // PBKDF2 coherente con login
            var newSalt = GenerateSalt(64);
            var newHash = ComputePBKDF2(model.NuevaContrasena!, newSalt);

            usuario.USUARIO_SALT = newSalt;
            usuario.USUARIO_CONTRASENA = newHash;
            usuario.USUARIO_CAMBIOINICIAL = false;
            //usuario.MODIFICADO_POR = usuario.USUARIO_NOMBRE;
            usuario.FECHA_MODIFICACION = ahora;

            // (Opcional) historial si su tabla existe
            try
            {
                var historial = new HISTORIAL_CONTRASENA
                {
                    HISTORIAL_ID = GenerarTokenIdCorto(),
                    USUARIO_ID = usuario.USUARIO_ID,
                    HASH = newHash,
                    SALT = newSalt,
                    FECHA_CREACION = ahora,
                    USUARIO_CREACION = usuario.USUARIO_NOMBRE,
                    ESTADO = true, // si es bool; si es string, use "ACTIVO"
                    ELIMINADO = false
                };
                _context.HISTORIAL_CONTRASENA.Add(historial);
            }
            catch { /* ignorar si no existe la tabla */ }

            // Consumir token
            tk.TOKEN_USADO = true;
            tk.USADO_EN = ahora;
            //tk.MODIFICADO_POR = usuario.USUARIO_NOMBRE;
            tk.FECHA_MODIFICACION = ahora;

            await _context.SaveChangesAsync(ct);

            TempData["Mensaje"] = "Tu contraseña fue actualizada correctamente.";
            return RedirectToAction("Login");
        }

        // =========================
        // Helpers criptográficos
        // =========================

        // PBKDF2 (HMACSHA256) 100,000 iteraciones, hash 32 bytes
        private static byte[] ComputePBKDF2(string password, byte[] salt, int iterations = 100_000)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password ?? "", salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32); // 256 bits
        }

        // ► LEGADO: SHA-256( SALT || UTF8(password) )
        private static byte[] HashSha256Salted(byte[] salt, string plainPwd)
        {
            using var sha = SHA256.Create();
            var pwdBytes = Encoding.UTF8.GetBytes(plainPwd ?? "");
            var data = new byte[salt.Length + pwdBytes.Length];
            Buffer.BlockCopy(salt, 0, data, 0, salt.Length);
            Buffer.BlockCopy(pwdBytes, 0, data, salt.Length, pwdBytes.Length);
            return sha.ComputeHash(data);
        }

        // ► Comparación en tiempo constante
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // ► Salt seguro (N bytes)
        private static byte[] GenerateSalt(int size)
        {
            var salt = new byte[size];
            RandomNumberGenerator.Fill(salt);
            return salt;
        }

        // ► SHA256 → HEX (para token)
        private static string Sha256Hex(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? ""));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // ► Token URL-safe (GUID + 16 bytes aleatorios Base64Url) — (no usado aquí, pero disponible)
        private static string GenerarTokenSeguro()
        {
            var guid = Guid.NewGuid().ToString("N");
            var random = new byte[16];
            RandomNumberGenerator.Fill(random);
            var b64 = Convert.ToBase64String(random).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{guid}.{b64}";
        }

        // ► ID corto (10 chars) para TOKEN_ID (evita confundir 0/O/I/1)
        private string GenerarTokenIdCorto()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Span<char> chars = stackalloc char[10];
            var bytes = new byte[10];
            RandomNumberGenerator.Fill(bytes);
            for (int i = 0; i < 10; i++)
                chars[i] = alphabet[bytes[i] % alphabet.Length];

            var id = new string(chars);
            // Reintento si colisiona
            if (_context.TOKEN_RECUPERACION.Any(t => t.TOKEN_ID == id))
                return GenerarTokenIdCorto();

            return id;
        }
    }

    // ==========
    // App config
    // ==========
    public class AppSettings
    {
        public required string BaseUrl { get; set; } // p. ej. https://localhost:7123 ó https://crearte.tu-dominio.com
    }
}
