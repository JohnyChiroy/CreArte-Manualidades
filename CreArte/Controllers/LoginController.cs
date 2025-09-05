using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

// 👇 Necesario para auth por cookies y claims
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace CreArte.Controllers
{
    public class LoginController : Controller
    {
        private readonly CreArteDbContext _context;

        public LoginController(CreArteDbContext context)
        {
            _context = context;
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
    }
}
