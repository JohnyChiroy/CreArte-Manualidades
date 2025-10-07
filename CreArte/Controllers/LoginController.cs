using CreArte.Data; // CreArteDbContext
using CreArte.Models; // Entidades EF
using CreArte.ModelsPartial; // ViewModels: LoginViewModels, CambiarContrasenaViewModel, RecuperacionContrasenaVM, RestablecerContrasenaVM
using CreArte.Services.Auditoria; // ICurrentUserService y AuditoriaService (si fuera necesario)
using CreArte.Services.Mail;  // EnvioCorreoSMTP y PlantillaEnvioCorreo
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

namespace CreArte.Controllers
{
    public class LoginController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly EnvioCorreoSMTP _mailer;
        private readonly PlantillaEnvioCorreo _tpl;
        private readonly AppSettings _app; // Para BaseUrl (links en correos)

        public LoginController(CreArteDbContext context, EnvioCorreoSMTP mailer, PlantillaEnvioCorreo tpl, IOptions<AppSettings> appOptions)
        {
            _context = context;
            _mailer = mailer;
            _tpl = tpl;
            _app = appOptions.Value;
        }
        public IActionResult Login()
        {
            return View(); // Views/Login/Login.cshtml
        }

        [HttpPost]
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

            // 6) ¿Debe cambiar contraseña?
            if (usuario.USUARIO_CAMBIOINICIAL == true)
            {
                // Guardamos ID en sesión para que la vista de cambio lo use
                HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID);
                HttpContext.Session.SetString("UsuarioNombre", usuario.USUARIO_NOMBRE);
                return RedirectToAction("CambiarContrasena", "Login");
            }

            // 7) SESIÓN (opcional para mostrar nombre en UI)
            HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID);
            HttpContext.Session.SetString("Nombre", usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID);

            // 8) AUTENTICACIÓN POR COOKIES (claims)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.USUARIO_ID),
                new Claim(ClaimTypes.Name, usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID),
                new Claim("preferred_username", usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID)
            };
            if (!string.IsNullOrWhiteSpace(usuario.USUARIO_CORREO))
                claims.Add(new Claim(ClaimTypes.Email, usuario.USUARIO_CORREO));

            // (Opcional) Agregar rol como claim
            // if (!string.IsNullOrWhiteSpace(usuario.ROL_ID))
            //     claims.Add(new Claim(ClaimTypes.Role, usuario.ROL_ID));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                // IsPersistent = model.Recordarme, // si agregas "Recordarme"
                // ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                // AllowRefresh = true
            };

            // Emite el cookie de autenticación
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProps
            );

            // 9) OK → redirigimos a Home (o donde quieras)
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

        // ================================
        // GET: /Login/RecuperarContrasena
        // ================================
        [HttpGet]
        public IActionResult RecuperarContrasena()
        {
            return View(new RecuperacionContrasenaVM());
        }

        // =================================
        // POST: /Login/RecuperarContrasena
        // - Genera token y envía correo
        // =================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecuperarContrasena(RecuperacionContrasenaVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1) Buscar usuario por nombre o correo
            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u =>
                        u.ELIMINADO == false &&
                        (u.USUARIO_NOMBRE == model.UsuarioONCorreo
                         || (u.USUARIO_CORREO != null && u.USUARIO_CORREO == model.UsuarioONCorreo)));

            if (usuario == null)
            {
                // No reveles si existe o no por seguridad
                TempData["ResetInfo"] = "Si el usuario existe, enviaremos instrucciones al correo registrado.";
                return RedirectToAction(nameof(RecuperarContrasena));
            }

            // 2) Obtener correo destino (validación simple)
            string correoDestino = usuario.USUARIO_CORREO;
            try { _ = new System.Net.Mail.MailAddress(correoDestino); }
            catch
            {
                TempData["ResetError"] = "El correo registrado es inválido. Contacta al administrador.";
                return RedirectToAction(nameof(RecuperarContrasena));
            }

            // 3) Generar token seguro + caducidad
            var token = GenerarTokenSeguro();
            var expira = DateTime.UtcNow.AddHours(1); // 1 hora

            // 3.1) Guardar fila en TOKEN_RECUPERACION
            var tokenId = GenerarTokenIdCorto(); // PK corto legible
            var fila = new TOKEN_RECUPERACION
            {
                TOKEN_ID = tokenId,
                USUARIO_ID = usuario.USUARIO_ID,
                TOKEN_VALOR = token,
                TOKEN_EXPIRA = expira,
                TOKEN_USADO = false,
                USUARIO_CREACION = "sistema",
                FECHA_CREACION = DateTime.Now,
                ESTADO = true,
                ELIMINADO = false
            };
            _context.TOKEN_RECUPERACION.Add(fila);
            await _context.SaveChangesAsync();

            // 4) URL de restablecimiento (usa App.BaseUrl)
            var url = $"{_app.BaseUrl}/Login/RestablecerContrasena?token={Uri.EscapeDataString(token)}&user={Uri.EscapeDataString(usuario.USUARIO_NOMBRE)}";

            // 5) Enviar correo
            var asunto = "Recuperación de contraseña - CreArte Manualidades";
            var html = _tpl.GenerarHtmlRecuperacion(usuario.USUARIO_NOMBRE, url, expira);

            var (ok, error) = await _mailer.EnviarAsync(correoDestino, asunto, html);
            if (ok)
                TempData["ResetInfo"] = "Si el usuario existe, hemos enviado un enlace de recuperación a su correo.";
            else
            {
#if DEBUG
                TempData["ResetError"] = $"No se pudo enviar el correo: {error}";
#else
                TempData["ResetError"] = "Ocurrió un problema al enviar el correo. Inténtalo nuevamente.";
#endif
            }

            return RedirectToAction(nameof(RecuperarContrasena));
        }

        // =========================================
        // GET: /Login/RestablecerContrasena
        // - Muestra formulario para nueva contraseña
        // =========================================
        [HttpGet]
        public async Task<IActionResult> RestablecerContrasena(string token, string user)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(user))
                return BadRequest("Token o usuario inválido.");

            // 1) Traer usuario por nombre
            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == user && u.ELIMINADO == false);

            if (usuario == null)
                return Content("El enlace de restablecimiento es inválido o ya expiró.");

            // 2) Validar token activo para ese usuario
            var tokenRow = await _context.TOKEN_RECUPERACION
                .FirstOrDefaultAsync(t => t.USUARIO_ID == usuario.USUARIO_ID
                                       && t.TOKEN_VALOR == token
                                       && !t.ELIMINADO
                                       && t.ESTADO
                                       && !t.TOKEN_USADO);

            if (tokenRow == null || tokenRow.TOKEN_EXPIRA < DateTime.UtcNow)
                return Content("El enlace de restablecimiento es inválido o ya expiró.");

            // 3) Renderizar VM
            var vm = new RestablecerContrasenaVM
            {
                UsuarioNombre = user,
                Token = token
            };
            return View(vm);
        }

        // =========================================
        // POST: /Login/RestablecerContrasena
        // - Aplica nueva contraseña (PBKDF2), invalida token
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerContrasena(RestablecerContrasenaVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1) Traer usuario por nombre
            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == model.UsuarioNombre && !u.ELIMINADO);

            if (usuario == null)
            {
                ModelState.AddModelError("", "El enlace es inválido o ya expiró.");
                return View(model);
            }

            // 2) Revalidar token para ese usuario
            var tokenRow = await _context.TOKEN_RECUPERACION
                .FirstOrDefaultAsync(t => t.USUARIO_ID == usuario.USUARIO_ID
                                       && t.TOKEN_VALOR == model.Token
                                       && !t.ELIMINADO
                                       && t.ESTADO
                                       && !t.TOKEN_USADO);

            if (tokenRow == null || tokenRow.TOKEN_EXPIRA < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "El enlace es inválido o ya expiró.");
                return View(model);
            }

            // 3) Regenerar SALT + HASH (PBKDF2)
            var newSalt = GenerateSalt(64);
            var newHash = ComputePBKDF2(model.NuevaContrasena, newSalt);

            usuario.USUARIO_SALT = newSalt;
            usuario.USUARIO_CONTRASENA = newHash;

            // 4) Marcar token como usado
            tokenRow.TOKEN_USADO = true;
            tokenRow.USADO_EN = DateTime.UtcNow;
            tokenRow.USUARIO_MODIFICACION = "sistema";
            tokenRow.FECHA_MODIFICACION = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["ResetOk"] = "Tu contraseña fue cambiada correctamente. Ahora puedes iniciar sesión.";
            return RedirectToAction("Login", "Login");
        }

        // =========================
        // Helpers criptográficos
        // =========================

        // ► PBKDF2 (HMACSHA256) 100,000 iteraciones, hash 32 bytes
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

        // ► Token URL-safe (GUID + 16 bytes aleatorios Base64Url)
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
