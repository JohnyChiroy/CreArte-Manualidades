// Filters/CreArteAuthorizeAttribute.cs
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CreArte.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CreArte.Filters
{
    /// <summary>
    /// Uso: [CreArteAuthorize("VER"|"CREAR"|"EDITAR"|"ELIMINAR")]
    /// Evalúa permisos por ruta: controller -> MODULO_ID y op -> flag (ver/crear/editar/eliminar).
    /// </summary>
    public class CreArteAuthorizeAttribute : TypeFilterAttribute
    {
        public CreArteAuthorizeAttribute(string op) : base(typeof(CreArteAuthorizeFilter))
        {
            Arguments = new object[] { op };
        }

        private class CreArteAuthorizeFilter : IAsyncActionFilter
        {
            private readonly ICreArtePermissionService _svc;
            private readonly string _op;

            public CreArteAuthorizeFilter(ICreArtePermissionService svc, string op)
            {
                _svc = svc;
                _op = op;
            }

            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                // Respetar [AllowAnonymous]
                if (context.Filters.OfType<IAllowAnonymousFilter>().Any())
                {
                    await next();
                    return;
                }

                var http = context.HttpContext;
                var user = http.User;

                // Heurística simple para AJAX/JSON
                bool isAjax =
                    http.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    http.Request.Headers["Accept"].Any(x => x.Contains("application/json"));

                // Debe estar autenticado
                if (user?.Identity?.IsAuthenticated != true)
                {
                    context.Result = isAjax ? new UnauthorizedResult() : new ChallengeResult();
                    return;
                }

                // === Resolver rolId desde varios claims; fallback a Session ===
                var rolId =
                       user.FindFirst("ROL_ID")?.Value
                    ?? user.FindFirst("rol_id")?.Value
                    ?? user.FindFirst(ClaimTypes.Role)?.Value
                    ?? http.Session.GetString("RolId");

                if (string.IsNullOrWhiteSpace(rolId))
                {
                    context.Result = isAjax
                        ? new StatusCodeResult(StatusCodes.Status403Forbidden)
                        : new RedirectToActionResult("AccesoDenegado", "Home", null);
                    return;
                }

                // Tomar controller y action actuales
                var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
                var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;

                // La op se acepta en español/inglés y mayúsculas/minúsculas (el servicio ya lo maneja)
                var ok = await _svc.HasPermissionByRouteAsync(rolId, controller, action, _op);
                if (!ok)
                {
                    context.Result = isAjax
                        ? new StatusCodeResult(StatusCodes.Status403Forbidden)
                        : new RedirectToActionResult("AccesoDenegado", "Home", null);
                    return;
                }

                await next();
            }
        }
    }
}
