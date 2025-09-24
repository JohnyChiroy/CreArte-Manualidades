using CreArte.Data; // CreArteDbContext
using CreArte.Models; // CreArteDbContext y entidades
using CreArte.ModelsPartial; // ViewModels: LoginViewModels, CambiarContrasenaViewModel, RecuperacionContrasenaVM, RestablecerContrasenaVM
using CreArte.Services.Auditoria; // ICurrentUserService y AuditoriaService
using CreArte.Services.Mail;  // EnvioCorreoSMTP y PlantillaEnvioCorreo
// 👇 Necesario para auth por cookies y claims
using Microsoft.AspNetCore.Authentication; 
using Microsoft.AspNetCore.Authentication.Cookies; 
using Microsoft.AspNetCore.Mvc; // Para Controller, IActionResult, etc.
using Microsoft.EntityFrameworkCore; // Para FirstOrDefaultAsync, AsNoTracking
using System.Security.Claims; // Para Claim, ClaimsIdentity, ClaimsPrincipal
using System.Security.Cryptography; // Para RandomNumberGenerator y SHA256
using System.Text; // Para Encoding.UTF8
using Microsoft.Extensions.Options; // Para IOptions<AppSettings>
using System.Threading.Tasks; // Para Task<IActionResult>
using System; // Para DateTime


namespace CreArte.Controllers
{
    public class LoginController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly EnvioCorreoSMTP _mailer;
        private readonly PlantillaEnvioCorreo _tpl;
        private readonly AppSettings _app; // Para BaseUrl
        public LoginController(CreArteDbContext context, EnvioCorreoSMTP mailer, PlantillaEnvioCorreo tpl, IOptions<AppSettings> appOptions)
        {
            _context = context;
            _mailer = mailer;
            _tpl = tpl;
            _app = appOptions.Value;
        }

        // ============================================
        // GET: /Login/Login
        // Ruta: GET /Login/Login
        // Muestra el formulario de inicio de sesión.
        // ============================================
        public IActionResult Login()
        {
            return View(); // Views/Login/Login.cshtml
        }

        // ============================================
        // POST: /Login/Login
        // Ruta: POST /Login/Login
        // Valida credenciales y emite el cookie de autenticación.
        // ============================================
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

            // 5) Verificar contraseña: SHA256(SALT || password)
            byte[] hashIngresado = HashPasswordWithSaltSHA256(model.USUARIO_CONTRASENA, usuario.USUARIO_SALT);
            if (!FixedTimeEquals(hashIngresado, usuario.USUARIO_CONTRASENA))
            {
                ModelState.AddModelError(string.Empty, "La contraseña es incorrecta.");
                return View(model);
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

            // 8) AUTENTICACIÓN POR COOKIES (lo que necesita tu auditoría)
            //    Creamos los claims que leerá tu servicio (Name, NameIdentifier, etc.)
            var claims = new List<Claim>
            {
                // Identificador único del usuario
                new Claim(ClaimTypes.NameIdentifier, usuario.USUARIO_ID),

                // Nombre visible (lo que luego obtienes con User.Identity.Name)
                new Claim(ClaimTypes.Name, usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID),

                // Suele ser útil para integraciones (tu servicio lee este claim si existe)
                new Claim("preferred_username", usuario.USUARIO_NOMBRE ?? usuario.USUARIO_ID)
            };

            // Si tu tabla tiene correo, agrégalo como claim
            if (!string.IsNullOrWhiteSpace(usuario.USUARIO_CORREO))
            {
                claims.Add(new Claim(ClaimTypes.Email, usuario.USUARIO_CORREO));
            }

            // (Opcional) Si tienes ROL_ID y quieres agregarlo como claim de rol:
            // if (!string.IsNullOrWhiteSpace(usuario.ROL_ID))
            //     claims.Add(new Claim(ClaimTypes.Role, usuario.ROL_ID));

            // Construimos la identidad con el esquema de cookies por defecto
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Construimos el principal
            var principal = new ClaimsPrincipal(identity);

            // Propiedades del cookie (puedes agregar IsPersistent si tienes “Recordarme”)
            var authProps = new AuthenticationProperties
            {
                // IsPersistent = model.Recordarme, // si tu modelo tiene esta propiedad
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
        // Ruta: GET /Login/Logout
        // Cierra sesión: limpia sesión y cookie de auth.
        // ============================================
        public async Task<IActionResult> Logout()
        {
            // Limpia el cookie de autenticación
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Limpia la sesión ASP.NET
            HttpContext.Session.Clear();
            HttpContext.Response.Cookies.Delete(".AspNetCore.Session");

            return RedirectToAction("Login", "Login");
        }

        // ============================================
        // GET: /Login/CambiarContrasena
        // Ruta: GET /Login/CambiarContrasena
        // Pide la nueva contraseña si “cambio inicial” está activo.
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
        // Ruta: POST /Login/CambiarContrasena
        // Actualiza hash y salt, y desactiva el flag de cambio inicial.
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

            // Nuevo SALT + nuevo hash
            byte[] nuevoSalt = GenerateSalt(64);
            byte[] nuevoHash = HashPasswordWithSaltSHA256(model.NuevaContrasena, nuevoSalt);

            usuario.USUARIO_SALT = nuevoSalt;
            usuario.USUARIO_CONTRASENA = nuevoHash;
            usuario.USUARIO_CAMBIOINICIAL = false;

            _context.SaveChanges();

            TempData["Mensaje"] = "Contraseña actualizada correctamente. Ingresa tus credenciales para tu primer inicio de sesión.";
            return RedirectToAction("Login", "Login");
        }

        // =========================
        // Helpers criptográficos
        // =========================

        private static byte[] GenerateSalt(int size)
        {
            var salt = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        private static byte[] HashPasswordWithSaltSHA256(string password, byte[] salt)
        {
            byte[] passBytes = Encoding.UTF8.GetBytes(password);
            byte[] salted = new byte[salt.Length + passBytes.Length];
            Buffer.BlockCopy(salt, 0, salted, 0, salt.Length);
            Buffer.BlockCopy(passBytes, 0, salted, salt.Length, passBytes.Length);
            using var sha = SHA256.Create();
            return sha.ComputeHash(salted);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        //aca empieza la recuperacion de contrasena

        // ================================
        // GET: /Login/RecuperarContrasena
        // ================================
        [HttpGet]
        public IActionResult RecuperarContrasena()
        {
            // Vista: Views/Login/RecuperarContrasena.cshtml
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
            //    - Opción A: por USUARIO_NOMBRE
            //    - Opción B: por USUARIO_EMAIL
            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u =>
                        u.ELIMINADO == false &&
                        (u.USUARIO_NOMBRE == model.UsuarioONCorreo
                         || (u.USUARIO_CORREO != null && u.USUARIO_CORREO == model.UsuarioONCorreo)));

            // (Alternativa si tomas el correo desde EMPLEADO/PERSONA)
            // var usuario = await _context.USUARIO
            //     .Include(u => u.EMPLEADO) // Ajusta navegación si existe
            //     .ThenInclude(e => e.PERSONA) // Ajusta si existe
            //     .FirstOrDefaultAsync(u =>
            //          u.ELIMINADO == false &&
            //          (u.USUARIO_NOMBRE == model.UsuarioONCorreo
            //            || (u.EMPLEADO != null && u.EMPLEADO.PERSONA != null 
            //                && u.EMPLEADO.PERSONA.CORREO == model.UsuarioONCorreo)));

            if (usuario == null)
            {
                // No reveles si existe o no por seguridad
                TempData["ResetInfo"] = "Si el usuario existe, enviaremos instrucciones al correo registrado.";
                return RedirectToAction(nameof(RecuperarContrasena));
            }

            // 2) Obtener correo destino
            string correoDestino = usuario.USUARIO_CORREO;
            // (Alternativa por JOIN)
            // if (string.IsNullOrWhiteSpace(correoDestino) && usuario.EMPLEADO?.PERSONA?.CORREO != null)
            //     correoDestino = usuario.EMPLEADO.PERSONA.CORREO;

            // Validación básica de formato (evita MailAddress con null/empty)
            try
            {
                _ = new System.Net.Mail.MailAddress(correoDestino);
            }
            catch
            {
                TempData["ResetError"] = "El correo registrado es inválido. Contacta al administrador.";
                return RedirectToAction(nameof(RecuperarContrasena));
            }

            //// 3) Generar token seguro y caducidad
            //var token = GenerarTokenSeguro();
            //var expira = DateTime.UtcNow.AddHours(1); // 1 hora de vigencia

            //usuario.TOKEN_RECUPERACION = token;
            //usuario.TOKEN_RECUPERACION = expira;
            //await _context.SaveChangesAsync();

            //// 4) Construir URL de restablecimiento (usa App.BaseUrl)
            ////    Ruta: GET /Login/RestablecerContrasena?token=...&user=...
            //var url = $"{_app.BaseUrl}/Login/RestablecerContrasena?token={Uri.EscapeDataString(token)}&user={Uri.EscapeDataString(usuario.USUARIO_NOMBRE)}";

            // 3) Generar token seguro y caducidad
            var token = GenerarTokenSeguro();
            var expira = DateTime.UtcNow.AddHours(1); // 1 hora de vigencia

            // 3.1) Crear fila en TOKEN_RECUPERACION
            var tokenId = GenerarTokenIdCorto(); // PK VARCHAR(10) (evitar colisiones)
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

            // 4) Construir URL de restablecimiento (mantengo ?token=&user=)
            var url = $"{_app.BaseUrl}/Login/RestablecerContrasena?token={Uri.EscapeDataString(token)}&user={Uri.EscapeDataString(usuario.USUARIO_NOMBRE)}";


            // 5) Preparar HTML del correo
            var asunto = "Recuperación de contraseña - CreArte Manualidades";
            var html = _tpl.GenerarHtmlRecuperacion(usuario.USUARIO_NOMBRE, url, expira);

            // 6) Enviar correo
            //var ok = await _mailer.EnviarAsync(correoDestino, asunto, html);

            //TempData[ok ? "ResetInfo" : "ResetError"] = ok
            //    ? "Si el usuario existe, hemos enviado un enlace de recuperación a su correo."
            //    : "Ocurrió un problema al enviar el correo. Inténtalo nuevamente.";
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

            //// Buscar el usuario por nombre y validar token
            //var usuario = await _context.USUARIO
            //    .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == user && u.TOKEN_RECUPERACION == token && u.ELIMINADO == false);

            //if (usuario == null || !usuario.RESET_EXPIRA.HasValue || usuario.RESET_EXPIRA.Value < DateTime.UtcNow)
            //{
            //    return Content("El enlace de restablecimiento es inválido o ya expiró.");
            //}

            //var vm = new RestablecerContrasenaVM
            //{
            //    UsuarioNombre = user,
            //    Token = token
            //};
            //// Vista: Views/Login/RestablecerContrasena.cshtml (créala con el código que te dejo abajo)
            //return View(vm);

            // 1) Traer usuario por nombre
            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == user && u.ELIMINADO == false);

            if (usuario == null)
                return Content("El enlace de restablecimiento es inválido o ya expiró.");

            // 2) Buscar token válido para ese usuario
            var tokenRow = await _context.TOKEN_RECUPERACION
                .FirstOrDefaultAsync(t => t.USUARIO_ID == usuario.USUARIO_ID
                                       && t.TOKEN_VALOR == token
                                       && !t.ELIMINADO
                                       && t.ESTADO
                                       && !t.TOKEN_USADO);

            if (tokenRow == null || tokenRow.TOKEN_EXPIRA < DateTime.UtcNow)
            {
                return Content("El enlace de restablecimiento es inválido o ya expiró.");
            }

            // 3) Preparar VM
            var vm = new RestablecerContrasenaVM
            {
                UsuarioNombre = user,
                Token = token
            };
            return View(vm);

        }

        // =========================================
        // POST: /Login/RestablecerContrasena
        // - Aplica nueva contraseña, regenera SALT+SHA256
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerContrasena(RestablecerContrasenaVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            //var usuario = await _context.USUARIO
            //    .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == model.UsuarioNombre
            //                            && u.TOKEN_RECUPERACION == model.Token
            //                            && u.ELIMINADO == false);

            //if (usuario == null || !usuario.RESET_EXPIRA.HasValue || usuario.RESET_EXPIRA.Value < DateTime.UtcNow)
            //{
            //    ModelState.AddModelError("", "El enlace es inválido o ya expiró.");
            //    return View(model);
            //}

            //// 1) Generar nueva SALT de 64 bytes
            //var newSalt = GenerarSalt(64);
            //// 2) Hash = SHA-256 (SALT || PWD_UTF8)
            //var newHash = HashSha256Salted(newSalt, model.NuevaContrasena);

            //usuario.USUARIO_SALT = newSalt;
            //usuario.USUARIO_CONTRASENA = newHash;

            //// 3) Invalidar token
            //usuario.RESET_TOKEN = null;
            //usuario.RESET_EXPIRA = null;

            //await _context.SaveChangesAsync();

            //TempData["ResetOk"] = "Tu contraseña fue cambiada correctamente. Ahora puedes iniciar sesión.";
            //// Ruta recomendada al login: GET /Login/Login
            //return RedirectToAction("Login", "Login");
            // 1) Traer usuario por nombre
            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == model.UsuarioNombre && !u.ELIMINADO);

            if (usuario == null)
            {
                ModelState.AddModelError("", "El enlace es inválido o ya expiró.");
                return View(model);
            }

            // 2) Revalidar token en la tabla
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

            // 3) Regenerar SALT + HASH
            var newSalt = GenerarSalt(64);
            var newHash = HashSha256Salted(newSalt, model.NuevaContrasena);

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

        // Genera un token URL-safe (GUID + 16 bytes aleatorios Base64Url)
        private static string GenerarTokenSeguro()
        {
            var guid = Guid.NewGuid().ToString("N");
            var random = new byte[16];
            RandomNumberGenerator.Fill(random);
            var b64 = Convert.ToBase64String(random)
                            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{guid}.{b64}";
        }

        private static byte[] GenerarSalt(int size)
        {
            var salt = new byte[size];
            RandomNumberGenerator.Fill(salt);
            return salt;
        }

        // SHA-256( SALT || UTF8(password) )
        private static byte[] HashSha256Salted(byte[] salt, string plainPwd)
        {
            using var sha = SHA256.Create();
            var pwdBytes = Encoding.UTF8.GetBytes(plainPwd ?? "");
            var data = new byte[salt.Length + pwdBytes.Length];
            Buffer.BlockCopy(salt, 0, data, 0, salt.Length);
            Buffer.BlockCopy(pwdBytes, 0, data, salt.Length, pwdBytes.Length);
            return sha.ComputeHash(data);
        }
        // Genera un id corto (10 chars) para TOKEN_ID -> evita colisiones visibles
        private string GenerarTokenIdCorto()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sin 0/O/I/1
            Span<char> chars = stackalloc char[10];
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[10];
            rng.GetBytes(bytes);
            for (int i = 0; i < 10; i++)
                chars[i] = alphabet[bytes[i] % alphabet.Length];

            var id = new string(chars);

            // (Opcional) Reintento si colisiona
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
