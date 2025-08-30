using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CreArte.Controllers
{
    public class LoginController : Controller
    {
        private readonly CreArteDbContext _context;

        public LoginController(CreArteDbContext context)
        {
            _context = context;
        }

        // GET: /Login/Login
        public IActionResult Login()
        {
            return View(); // Views/Login/Login.cshtml
        }

        // POST: /Login/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModels model)
        {
            // 1) Validamos modelo
            if (!ModelState.IsValid)
                return View(model);

            // 2) Buscamos el usuario por nombre (no eliminado)
            var usuario = _context.USUARIO
                .AsNoTracking()
                .FirstOrDefault(u => u.USUARIO_NOMBRE == model.USUARIO_NOMBRE && u.ELIMINADO == false);

            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "El usuario ingresado no existe.");
                return View(model);
            }

            // 3) Validamos que esté activo (si ESTADO es nullable, usamos GetValueOrDefault)
            // Después (funciona cuando ESTADO es bool no-nullable)
            if (!usuario.ESTADO)
            {
                ModelState.AddModelError(string.Empty, "El usuario está deshabilitado. Contacte al administrador.");
                return View(model);
            }


            // 4) Verificamos la contraseña con SALT + SHA-256
            //    - Suponemos que USUARIO_CONTRASENA es byte[] con el hash almacenado.
            //    - Suponemos que USUARIO_SALT es byte[] (64 bytes).
            if (usuario.USUARIO_SALT == null || usuario.USUARIO_CONTRASENA == null)
            {
                ModelState.AddModelError(string.Empty, "El usuario no tiene credenciales configuradas. Contacte al administrador.");
                return View(model);
            }

            // 4.1) Calculamos hash SHA256( SALT || password ) en bytes
            byte[] hashIngresado = HashPasswordWithSaltSHA256(model.USUARIO_CONTRASENA, usuario.USUARIO_SALT);

            // 4.2) Comparamos de forma constante (evita ataques de tiempo)
            if (!FixedTimeEquals(hashIngresado, usuario.USUARIO_CONTRASENA))
            {
                ModelState.AddModelError(string.Empty, "La contraseña es incorrecta.");
                return View(model);
            }

            // 5) Forzar cambio de contraseña si corresponde
            if (usuario.USUARIO_CAMBIOINICIAL == true)
            {
                // Guardamos el ID en sesión para usarlo en la pantalla de cambio
                HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID); // USUARIO_ID es string
                // Puedes también registrar "UsuarioNombre" si quieres mostrarlo
                HttpContext.Session.SetString("UsuarioNombre", usuario.USUARIO_NOMBRE);
                return RedirectToAction("CambiarContrasena", "Login");
            }

            // 6) Login válido -> guardamos datos mínimos en sesión
            HttpContext.Session.SetString("UsuarioId", usuario.USUARIO_ID);
            // Si tu tabla USUARIO tiene NOMBRE_EMPLEADO úsalo, si no, guarda el nombre de usuario
            string nombreParaMostrar = !string.IsNullOrWhiteSpace(usuario.USUARIO_NOMBRE)
                ? usuario.USUARIO_NOMBRE
                : usuario.USUARIO_NOMBRE;

            HttpContext.Session.SetString("Nombre", nombreParaMostrar);

            // 7) Redirigimos al Home (ajusta si tu landing es otra)
            return RedirectToAction("Index", "Home");
        }

        // GET: /Login/Logout
        public IActionResult Logout()
        {
            // Limpiamos la sesión y la cookie de sesión
            HttpContext.Session.Clear();
            HttpContext.Response.Cookies.Delete(".AspNetCore.Session");
            return RedirectToAction("Login", "Login");
        }

        // GET: /Login/CambiarContrasena
        public IActionResult CambiarContrasena()
        {
            // Verificamos que haya sesión
            var userId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login");

            return View(); // Views/Login/CambiarContrasena.cshtml
        }

        // POST: /Login/CambiarContrasena
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarContrasena(CambiarContrasenaViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1) Obtenemos el ID desde sesión (USUARIO_ID es string)
            var userId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login");

            // 2) Buscamos el usuario
            var usuario = _context.USUARIO.FirstOrDefault(u => u.USUARIO_ID == userId && u.ELIMINADO == false);
            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "No se encontró el usuario.");
                return View(model);
            }

            // 3) Generamos un nuevo SALT y el nuevo hash
            byte[] nuevoSalt = GenerateSalt(64); // 64 bytes recomendados
            byte[] nuevoHash = HashPasswordWithSaltSHA256(model.NuevaContrasena, nuevoSalt);

            // 4) Actualizamos los campos de credenciales
            usuario.USUARIO_SALT = nuevoSalt;
            usuario.USUARIO_CONTRASENA = nuevoHash;
            usuario.USUARIO_CAMBIOINICIAL = false;

            // 5) (Opcional) Metadatos si tu tabla los tiene
            // usuario.MODIFICADO_POR = HttpContext.Session.GetString("Nombre");
            // usuario.FECHA_MODIFICACION = DateTime.Now;

            _context.SaveChanges();

            TempData["Mensaje"] = "Contraseña actualizada correctamente. Ingresa tus credenciales para tu primer inicio de sesión.";
            return RedirectToAction("Login", "Login");
        }

        // =========================
        // Helpers criptográficos
        // =========================

        // Genera un SALT seguro de 'size' bytes usando RNGCSP
        private static byte[] GenerateSalt(int size)
        {
            var salt = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        // Calcula SHA-256( SALT || password ) y devuelve bytes
        private static byte[] HashPasswordWithSaltSHA256(string password, byte[] salt)
        {
            // Convertimos el password a bytes UTF8
            byte[] passBytes = Encoding.UTF8.GetBytes(password);

            // Concatenamos SALT + password
            byte[] salted = new byte[salt.Length + passBytes.Length];
            Buffer.BlockCopy(salt, 0, salted, 0, salt.Length);
            Buffer.BlockCopy(passBytes, 0, salted, salt.Length, passBytes.Length);

            using var sha = SHA256.Create();
            return sha.ComputeHash(salted);
        }

        // Comparación en tiempo constante para evitar ataques de timing
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
