using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CreArte.Services.Security
{
    public interface ICreArtePermissionService
    {
        // ===== Consultas por usuario (ClaimsPrincipal) =====
        bool CanView(ClaimsPrincipal user, string moduloId);
        bool CanCreate(ClaimsPrincipal user, string moduloId);
        bool CanEdit(ClaimsPrincipal user, string moduloId);
        bool CanDelete(ClaimsPrincipal user, string moduloId);

        // ===== Consultas por rolId (nombres “ByRole”) =====
        bool CanViewByRole(string rolId, string moduloId);
        bool CanCreateByRole(string rolId, string moduloId);
        bool CanEditByRole(string rolId, string moduloId);
        bool CanDeleteByRole(string rolId, string moduloId);

        // ===== Aliases por rolId (para compatibilidad) =====
        bool CanView(string rolId, string moduloId);
        bool CanCreate(string rolId, string moduloId);
        bool CanEdit(string rolId, string moduloId);
        bool CanDelete(string rolId, string moduloId);

        // ===== Soporte para atributos/middleware por ruta =====
        Task<bool> HasPermissionByRouteAsync(string rolId, string controller, string action, string op, CancellationToken ct = default);

        // ===== Caché =====
        void InvalidateModule(string rolId, string moduloId);
        void InvalidateRole(string rolId);
    }
}
