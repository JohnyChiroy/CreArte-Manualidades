// Ruta: /Services/Auditoria/CurrentUserService.cs
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CreArte.Services.Auditoria
{
    /// <summary>
    /// Lee el usuario actual desde HttpContext con varios fallbacks.
    /// </summary>
    public sealed class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _http;

        public CurrentUserService(IHttpContextAccessor http)
        {
            _http = http;
        }

        public string GetUserNameOrSystem()
        {
            var user = _http?.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return "system";

            // 1) Nombre estándar
            var name = user.Identity?.Name;

            // 2) Claim Name
            if (string.IsNullOrWhiteSpace(name))
                name = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            // 3) OIDC/Azure AD
            if (string.IsNullOrWhiteSpace(name))
                name = user.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;

            // 4) Email
            if (string.IsNullOrWhiteSpace(name))
                name = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            return string.IsNullOrWhiteSpace(name) ? "system" : name;
        }
    }
}
