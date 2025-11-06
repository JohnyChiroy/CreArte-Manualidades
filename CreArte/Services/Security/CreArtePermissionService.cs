using CreArte.Data;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CreArte.Services.Security
{
    public class CreArtePermissionService : ICreArtePermissionService
    {
        private readonly CreArteDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CreArtePermissionService> _log;

        private record PermFlags(bool Ver, bool Crear, bool Editar, bool Eliminar);

        public CreArtePermissionService(CreArteDbContext db, IMemoryCache cache, ILogger<CreArtePermissionService> log)
        {
            _db = db; _cache = cache; _log = log;
        }

        // ====== Mapeo Controller -> MODULO_ID (exacto a su INSERT) ======
        private static readonly Dictionary<string, string> CtrlToModulo = new(StringComparer.OrdinalIgnoreCase )
        {
            ["compras"] = "COMPRAS",
            ["inventario"] = "INVENTARIO",
            ["kardex"] = "KARDEX",
            ["caja"] = "CAJA",
            ["ventas"] = "VENTAS",
            ["pedidos"] = "PEDIDOS",
            ["reportes"] = "REPORTES",

            ["productos"] = "PRODUCTOS",
            ["proveedores"] = "PROVEEDOR",   // singular en BD
            ["clientes"] = "CLIENTES",
            ["empleados"] = "EMPLEADOS",

            ["usuarios"] = "USUARIOS",
            ["roles"] = "ROLES",
            ["permisos"] = "PERMISOS",

            ["areas"] = "AREAS",
            ["puestos"] = "PUESTOS",
            ["niveles"] = "NIVELES",
            ["categorias"] = "CATEGORIA",   // singular en BD
            ["subcategorias"] = "SUBCATEGOR",  // ID exacto de su INSERT
            ["marcas"] = "MARCAS",
            ["unidadesmedida"] = "UNIDMEDIDA",
            ["tiposempaque"] = "TIPEMPAQUE",
            ["tiposproducto"] = "TIPOSPROD",
            ["tiposcliente"] = "TIPCLIENTE",



        };

      
        // ====== Sinónimos UI -> MODULO_ID canónico (para Sidebar / vistas) ======
        private static readonly Dictionary<string, string> ModSyn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PROVEEDORES"] = "PROVEEDOR",
            ["CATEGORIAS"] = "CATEGORIA",
            ["SUBCATEGORIAS"] = "SUBCATEGOR",
            ["UNIDADESMEDIDA"] = "UNIDMEDIDA",
            ["TIPOSEMPAQUE"] = "TIPEMPAQUE",
            ["TIPOSPRODUCTO"] = "TIPOSPROD",
            ["TIPOSCLIENTE"] = "TIPCLIENTE",

        };

        // ================= API (ClaimsPrincipal) =================
        public bool CanView(ClaimsPrincipal user, string moduloId) => GetFlag(user, moduloId, f => f.Ver);
        public bool CanCreate(ClaimsPrincipal user, string moduloId) => GetFlag(user, moduloId, f => f.Crear);
        public bool CanEdit(ClaimsPrincipal user, string moduloId) => GetFlag(user, moduloId, f => f.Editar);
        public bool CanDelete(ClaimsPrincipal user, string moduloId) => GetFlag(user, moduloId, f => f.Eliminar);

        // ================= API (rolId) =================
        public bool CanViewByRole(string rolId, string moduloId) => GetFlagByRole(rolId, moduloId, f => f.Ver);
        public bool CanCreateByRole(string rolId, string moduloId) => GetFlagByRole(rolId, moduloId, f => f.Crear);
        public bool CanEditByRole(string rolId, string moduloId) => GetFlagByRole(rolId, moduloId, f => f.Editar);
        public bool CanDeleteByRole(string rolId, string moduloId) => GetFlagByRole(rolId, moduloId, f => f.Eliminar);

        // Aliases
        public bool CanView(string rolId, string moduloId) => CanViewByRole(rolId, moduloId);
        public bool CanCreate(string rolId, string moduloId) => CanCreateByRole(rolId, moduloId);
        public bool CanEdit(string rolId, string moduloId) => CanEditByRole(rolId, moduloId);
        public bool CanDelete(string rolId, string moduloId) => CanDeleteByRole(rolId, moduloId);

        // ====== Autorización por ruta (para el atributo) ======
        public Task<bool> HasPermissionByRouteAsync(string rolId, string controller, string action, string op, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rolId)) return Task.FromResult(false);

            var modId = MapControllerToModuloId(controller);
            if (string.IsNullOrWhiteSpace(modId)) return Task.FromResult(false);

            var opKey = (op ?? "ver").Trim().ToLowerInvariant();
            bool ok = opKey switch
            {
                "view" or "ver" => CanViewByRole(rolId, modId),
                "create" or "crear" => CanCreateByRole(rolId, modId),
                "edit" or "editar" => CanEditByRole(rolId, modId),
                "delete" or "eliminar" => CanDeleteByRole(rolId, modId),
                _ => CanViewByRole(rolId, modId)
            };

            return Task.FromResult(ok);
        }

        private static string? MapControllerToModuloId(string controller)
        {
            var key = (controller ?? "").Trim().ToLowerInvariant();
            return CtrlToModulo.TryGetValue(key, out var modId) ? modId : null;
        }

        // ================= Núcleo =================
        private bool GetFlag(ClaimsPrincipal user, string moduloId, Func<PermFlags, bool> selector)
        {
            var rolId = ResolveRoleId(user);
            if (string.IsNullOrWhiteSpace(rolId)) return false;
            return GetFlagByRole(rolId!, moduloId, selector);
        }

        private bool GetFlagByRole(string rolId, string moduloId, Func<PermFlags, bool> selector)
        {
            if (string.IsNullOrWhiteSpace(rolId) || string.IsNullOrWhiteSpace(moduloId))
                return false;

            var roleKey = NormRole(rolId);
            var modKey = NormModule(moduloId);   // ⬅️ canoniza sinónimos

            var map = GetOrLoadRolePerms(roleKey);
            return map.TryGetValue(modKey, out var flags) ? selector(flags) : false; // default DENY
        }

        private Dictionary<string, PermFlags> GetOrLoadRolePerms(string roleKey)
        {
            var cacheKey = CacheKey(roleKey);
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, PermFlags>? cached) && cached != null)
                return cached;

            var rows = _db.PERMISOS
                .AsNoTracking()
                .Where(p => !p.ELIMINADO && p.ESTADO == true && p.ROL_ID == roleKey)
                .Join(_db.MODULO.AsNoTracking().Where(m => !m.ELIMINADO && m.ESTADO == true),
                      p => p.MODULO_ID, m => m.MODULO_ID,
                      (p, m) => new { m.MODULO_ID, p.VISUALIZAR, p.CREAR, p.MODIFICAR, p.ELIMINAR })
                .ToList();

            var dict = rows
                .GroupBy(r => NormModule(r.MODULO_ID)) // también canoniza aquí
                .ToDictionary(
                    g => g.Key,
                    g => new PermFlags(
                        g.Any(x => x.VISUALIZAR),
                        g.Any(x => x.CREAR),
                        g.Any(x => x.MODIFICAR),
                        g.Any(x => x.ELIMINAR)
                    ),
                    StringComparer.OrdinalIgnoreCase);

            _cache.Set(cacheKey, dict, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                SlidingExpiration = TimeSpan.FromMinutes(5)
            });

            return dict;
        }

        // ================= Rol =================
        private string? ResolveRoleId(ClaimsPrincipal user)
        {
            var role = user.FindFirstValue(ClaimTypes.Role)
                       ?? user.FindFirstValue("ROL_ID")
                       ?? user.FindFirstValue("rol_id");

            if (!string.IsNullOrWhiteSpace(role)) return NormRole(role);

            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(uid)) return null;

            try
            {
                var rid = _db.USUARIO.AsNoTracking()
                    .Where(u => !u.ELIMINADO && u.USUARIO_ID == uid)
                    .Select(u => u.ROL_ID)
                    .FirstOrDefault();

                return string.IsNullOrWhiteSpace(rid) ? null : NormRole(rid!);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No fue posible resolver el rol para el usuario {UserId}", uid);
                return null;
            }
        }

        // ================= Caché =================
        public void InvalidateModule(string rolId, string moduloId) => InvalidateRole(rolId);
        public void InvalidateRole(string rolId)
        {
            if (string.IsNullOrWhiteSpace(rolId)) return;
            _cache.Remove(CacheKey(NormRole(rolId)));
        }

        // ================= Helpers =================
        private static string CacheKey(string roleKey) => $"perms:role:{roleKey}";
        private static string NormRole(string rolId) => (rolId ?? "").Trim().ToUpperInvariant();
        private static string NormModule(string mid)
        {
            var key = (mid ?? "").Trim().ToUpperInvariant();
            return ModSyn.TryGetValue(key, out var canonical) ? canonical : key;
        }
    }
}
