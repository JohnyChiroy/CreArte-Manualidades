using Microsoft.AspNetCore.Mvc;

namespace CreArte.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Login()
        {
            // Aquí podrías agregar lógica para manejar el inicio de sesión, como validar credenciales.
            // Por ahora, simplemente retornamos la vista de inicio de sesión.
            // Puedes usar ViewData o ViewBag para pasar datos a la vista si es necesario.
            ViewData["Title"] = "Iniciar Sesión";
            ViewData["Message"] = "Por favor, ingresa tus credenciales para iniciar sesión.";
            // Retorna la vista de inicio de sesión.
            // Asegúrate de que tienes una vista llamada Login.cshtml en la carpeta Views/Login.
            // Si no tienes la vista, puedes crearla con el siguiente contenido básico:
            // <h2>@ViewData["Title"]</h2>
            // <p>@ViewData["Message"]</p>
            // <form method="post">
            //     <input type="text" name="username" placeholder="Usuario" required />
            //     <input type="password" name="password" placeholder="Contraseña" required />
            //     <button type="submit">Iniciar Sesión</button>
            // </form>
            // Si tienes una vista Login.cshtml, asegúrate de que esté en la carpeta Views/Login.
            // Si necesitas manejar el envío del formulario, puedes agregar un método POST aquí.
            return View();
        }
    }
}
