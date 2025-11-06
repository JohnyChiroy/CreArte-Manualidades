// Controllers/Seguridad/PermisosController.cs
using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Controllers.Seguridad
{
    public class PermisosController : Controller
    {
        private readonly CreArteDbContext _db;
        private readonly ICreArtePermissionService _perms;

        public PermisosController(CreArteDbContext db, ICreArtePermissionService perms)
        {
            _db = db; _perms = perms;
        }

        // === ID corto sin secuencia/prefijo ===
        private static string NewPermisoId() => Guid.NewGuid().ToString("N")[..10];

        // === INDEX: listado por ROL + filtros + paginación manual ===
        [HttpGet]
        public async Task<IActionResult> Index(
            string? estado = null,   // "true"/"false" o null
            string? q = null,
            int page = 1,
            int pageSize = 10,
            string? sort = "rol",
            string? dir = "asc",
            CancellationToken ct = default)
        {
            var baseQry =
                from p in _db.PERMISOS.AsNoTracking()
                join r in _db.ROL.AsNoTracking() on p.ROL_ID equals r.ROL_ID
                where !p.ELIMINADO
                select new { p, r };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.p.PERMISOS_ID!, term) ||
                    EF.Functions.Like(x.p.ROL_ID!, term) ||
                    EF.Functions.Like(x.r.ROL_NOMBRE!, term));
            }

            if (!string.IsNullOrWhiteSpace(estado) && bool.TryParse(estado, out var st))
                baseQry = baseQry.Where(x => x.p.ESTADO == st);

            var agrupado =
                baseQry.GroupBy(x => new { x.p.ROL_ID, x.r.ROL_NOMBRE })
                       .Select(g => new PermisoListVM
                       {
                           RolId = g.Key.ROL_ID!,
                           RolNombre = g.Key.ROL_NOMBRE!,
                           PermisosId = g.Min(y => y.p.PERMISOS_ID!)!,
                           FechaCreacion = g.Min(y => y.p.FECHA_CREACION)
                       });

            IOrderedQueryable<PermisoListVM> ordenado = (sort ?? "rol").ToLower() switch
            {
                "nombre" => (dir ?? "asc").ToLower() == "desc"
                    ? agrupado.OrderByDescending(x => x.RolNombre)
                    : agrupado.OrderBy(x => x.RolNombre),
                "fecha" => (dir ?? "asc").ToLower() == "desc"
                    ? agrupado.OrderByDescending(x => x.FechaCreacion)
                    : agrupado.OrderBy(x => x.FechaCreacion),
                _ => (dir ?? "asc").ToLower() == "desc"
                    ? agrupado.OrderByDescending(x => x.RolId)
                    : agrupado.OrderBy(x => x.RolId)
            };

            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;
            if (page <= 0) page = 1;

            var totalItems = await ordenado.CountAsync(ct);
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var items = await ordenado
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var model = new PermisosIndexVM
            {
                Items = items,
                Q = q,
                Estado = estado,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                Sort = sort,
                Dir = dir
            };

            return View(model);
        }

        // === CREATE (asignación masiva) ===
        [HttpGet]
        public async Task<IActionResult> Create(string? rolId = null)
        {
            var vm = new PermisosModuloBulkVM
            {
                Roles = await _db.ROL
                    .Where(r => !r.ELIMINADO && r.ESTADO == true)
                    .OrderBy(r => r.ROL_NOMBRE)
                    .Select(r => new SelectListItem { Value = r.ROL_ID, Text = r.ROL_NOMBRE! })
                    .ToListAsync(),
                RolId = rolId ?? ""
            };

            var mods = await _db.MODULO.AsNoTracking()
                .Where(m => !m.ELIMINADO && m.ESTADO == true)
                .OrderBy(m => m.MODULO_NOMBRE)
                .Select(m => new { m.MODULO_ID, m.MODULO_NOMBRE })
                .ToListAsync();

            var existentes = new Dictionary<string, (bool v, bool c, bool e, bool d)>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(rolId))
            {
                existentes = await _db.PERMISOS.AsNoTracking()
                    .Where(p => !p.ELIMINADO && p.ESTADO == true && p.ROL_ID == rolId)
                    .ToDictionaryAsync(p => p.MODULO_ID,
                        p => (p.VISUALIZAR, p.CREAR, p.MODIFICAR, p.ELIMINAR),
                        StringComparer.OrdinalIgnoreCase);
            }

            vm.Modulos = mods.Select(m =>
            {
                var ex = existentes.TryGetValue(m.MODULO_ID, out var f) ? f : (false, false, false, false);
                return new ModuloPermItemVM
                {
                    ModuloId = m.MODULO_ID,
                    ModuloNombre = m.MODULO_NOMBRE!,
                    Ver = ex.Item1,
                    Crear = ex.Item2,
                    Editar = ex.Item3,
                    Eliminar = ex.Item4,
                    YaAsignado = ex.Item1 || ex.Item2 || ex.Item3 || ex.Item4
                };
            }).ToList();

            return View(vm);
        }

        // === SAVE ASSIGN ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAssign(PermisosModuloBulkVM vm, CancellationToken ct = default)
        {
            string? rolIdForm = Request.HasFormContentType ? Request.Form["RolId"].FirstOrDefault() : null;
            vm.RolId = (vm.RolId ?? rolIdForm ?? "").Trim();

            if (string.IsNullOrWhiteSpace(vm.RolId))
            {
                TempData["SwalErr"] = "Seleccione un rol.";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
            }

            var totalVmItems = vm.Modulos?.Count ?? 0;
            if (totalVmItems == 0)
            {
                TempData["SwalErr"] = "No llegaron ítems de 'Modulos' al servidor.";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
            }

            // Rol válido
            var rolOk = await _db.ROL.AnyAsync(r => !r.ELIMINADO && r.ESTADO == true && r.ROL_ID == vm.RolId, ct);
            if (!rolOk)
            {
                TempData["SwalErr"] = "El Rol no existe o está inactivo.";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
            }

            // Solo módulos activos
            var modSet = new HashSet<string>(
                await _db.MODULO.Where(m => !m.ELIMINADO && m.ESTADO == true)
                    .Select(m => m.MODULO_ID).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            // Filtrar candidatos de inserción
            var items = (vm.Modulos ?? new())
                .Where(i => !i.YaAsignado && (i.Ver || i.Crear || i.Editar || i.Eliminar))
                .Where(i => modSet.Contains(i.ModuloId))
                .ToList();

            if (items.Count == 0)
            {
                TempData["SwalWarn"] = "No hay acciones para asignar.";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
            }

            // Evitar duplicados en BD
            var yaSet = new HashSet<string>(
                await _db.PERMISOS.AsNoTracking()
                    .Where(p => !p.ELIMINADO && p.ROL_ID == vm.RolId)
                    .Select(p => p.MODULO_ID).ToListAsync(ct),
                StringComparer.OrdinalIgnoreCase);

            var porInsertar = items.Where(i => !yaSet.Contains(i.ModuloId)).ToList();
            if (porInsertar.Count == 0)
            {
                TempData["SwalWarn"] = "No se realizaron inserciones (posibles duplicados).";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
            }

            // (Opcional) Normalización: si hay C/E/E sin Ver, implicar Ver
            foreach (var it in porInsertar)
            {
                if (!it.Ver && (it.Crear || it.Editar || it.Eliminar))
                    it.Ver = true; // comente esta línea si quiere política estricta
            }

            foreach (var it in porInsertar)
            {
                _db.PERMISOS.Add(new PERMISOS
                {
                    PERMISOS_ID = NewPermisoId(),
                    ROL_ID = vm.RolId,
                    MODULO_ID = it.ModuloId,
                    VISUALIZAR = it.Ver,       // << clave: VISUALIZAR proviene de "Ver"
                    CREAR = it.Crear,
                    MODIFICAR = it.Editar,
                    ELIMINAR = it.Eliminar,
                    USUARIO_CREACION = User.Identity?.Name ?? "SYSTEM",
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = true
                });
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                foreach (var it in porInsertar) _perms.InvalidateModule(vm.RolId, it.ModuloId);

                TempData["SavedOk"] = true;
                TempData["SavedCount"] = porInsertar.Count;
            }
            catch (DbUpdateException ex)
            {
                TempData["SwalErr"] = "Error al guardar permisos en base de datos.";
                TempData["Dbg"] = ex.GetBaseException().Message;
            }

            return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
        }

        // === EDIT BULK ===
        [HttpGet]
        public async Task<IActionResult> EditBulk(string? rolId = null)
        {
            var vm = new PermisosModuloBulkVM
            {
                Roles = await _db.ROL
                    .Where(r => !r.ELIMINADO && r.ESTADO == true)
                    .OrderBy(r => r.ROL_NOMBRE)
                    .Select(r => new SelectListItem { Value = r.ROL_ID, Text = r.ROL_NOMBRE! })
                    .ToListAsync(),
                RolId = rolId ?? ""
            };

            if (string.IsNullOrWhiteSpace(vm.RolId))
                return View(vm);

            var mods = await _db.MODULO.AsNoTracking()
                .Where(m => !m.ELIMINADO && m.ESTADO == true)
                .OrderBy(m => m.MODULO_NOMBRE)
                .Select(m => new { m.MODULO_ID, m.MODULO_NOMBRE })
                .ToListAsync();

            var map = await _db.PERMISOS.AsNoTracking()
                .Where(p => !p.ELIMINADO && p.ROL_ID == vm.RolId)
                .ToDictionaryAsync(p => p.MODULO_ID,
                    p => (p.VISUALIZAR, p.CREAR, p.MODIFICAR, p.ELIMINAR, p.ESTADO),
                    StringComparer.OrdinalIgnoreCase);

            vm.Modulos = mods.Select(m =>
            {
                var ok = map.TryGetValue(m.MODULO_ID, out var f);
                return new ModuloPermItemVM
                {
                    ModuloId = m.MODULO_ID,
                    ModuloNombre = m.MODULO_NOMBRE!,
                    Ver = ok && f.Item1,
                    Crear = ok && f.Item2,
                    Editar = ok && f.Item3,
                    Eliminar = ok && f.Item4,
                    YaAsignado = ok && (f.Item1 || f.Item2 || f.Item3 || f.Item4)
                };
            }).ToList();

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBulk(PermisosModuloBulkVM vm, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(vm.RolId))
            {
                TempData["SwalErr"] = "Seleccione un rol.";
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId });
            }

            var items = (vm.Modulos ?? new()).Where(i => i.Touched).ToList();
            if (items.Count == 0)
            {
                TempData["SwalWarn"] = "No se detectaron cambios.";
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId });
            }

            var existentes = await _db.PERMISOS
                .Where(p => !p.ELIMINADO && p.ROL_ID == vm.RolId)
                .ToListAsync(ct);

            var map = existentes.ToDictionary(e => e.MODULO_ID, e => e, StringComparer.OrdinalIgnoreCase);

            var porInsertar = new List<PERMISOS>();
            int cambios = 0;

            foreach (var it in items)
            {
                // (Opcional) Normalización: implicar "Ver" si hay C/E/E
                if (!it.Ver && (it.Crear || it.Editar || it.Eliminar))
                    it.Ver = true; // comente para política estricta

                if (!map.TryGetValue(it.ModuloId, out var ex))
                {
                    porInsertar.Add(new PERMISOS
                    {
                        PERMISOS_ID = NewPermisoId(),
                        ROL_ID = vm.RolId,
                        MODULO_ID = it.ModuloId,
                        VISUALIZAR = it.Ver,       // gobernado por "Ver"
                        CREAR = it.Crear,
                        MODIFICAR = it.Editar,
                        ELIMINAR = it.Eliminar,
                        USUARIO_CREACION = User.Identity?.Name ?? "SYSTEM",
                        FECHA_CREACION = DateTime.Now,
                        ESTADO = true
                    });
                    cambios++;
                }
                else
                {
                    if (ex.VISUALIZAR != it.Ver || ex.CREAR != it.Crear || ex.MODIFICAR != it.Editar || ex.ELIMINAR != it.Eliminar)
                    {
                        ex.VISUALIZAR = it.Ver;
                        ex.CREAR = it.Crear;
                        ex.MODIFICAR = it.Editar;
                        ex.ELIMINAR = it.Eliminar;
                        ex.USUARIO_MODIFICACION = User.Identity?.Name ?? "SYSTEM";
                        ex.FECHA_MODIFICACION = DateTime.Now;
                        cambios++;
                    }
                }
            }

            if (porInsertar.Count > 0)
                _db.PERMISOS.AddRange(porInsertar);

            if (cambios == 0)
            {
                TempData["SwalWarn"] = "No se detectaron cambios.";
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId });
            }

            await _db.SaveChangesAsync(ct);
            foreach (var it in items) _perms.InvalidateModule(vm.RolId, it.ModuloId);

            TempData["SwalOk"] = $"Permisos actualizados correctamente ({cambios} cambio(s)).";
            return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId });
        }
    }
}
