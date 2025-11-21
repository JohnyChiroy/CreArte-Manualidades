using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Auditoria; // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;

namespace CreArte.Controllers
{
    public class ProveedoresController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public ProveedoresController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /Proveedores?Search=...&Empresa=...&Estado=...
        // ============================================================
        public async Task<IActionResult> Index(
            string? Search,
            string? Empresa,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 0) Helper local para armar Nombre Completo de la persona
            string ComposeFullName(PERSONA p)
            {
                var parts = new[]
                {
                    p.PERSONA_PRIMERNOMBRE,
                    p.PERSONA_SEGUNDONOMBRE,
                    p.PERSONA_TERCERNOMBRE,
                    p.PERSONA_PRIMERAPELLIDO,
                    p.PERSONA_SEGUNDOAPELLIDO,
                    p.PERSONA_APELLIDOCASADA
                };
                return string.Join(" ", parts.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
            }

            // 1) Base (no eliminados)
            IQueryable<PROVEEDOR> q = _context.PROVEEDOR.Where(pv => !pv.ELIMINADO);

            // 2) Búsqueda global
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();

                // LIKE sobre campos de PERSONA concatenados + NIT + DPI + Empresa + ID
                q = q.Where(pv =>
                    EF.Functions.Like(pv.PROVEEDOR_ID, $"%{s}%") ||
                    EF.Functions.Like(pv.EMPRESA ?? "", $"%{s}%") ||
                    EF.Functions.Like(
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA ?? ""),
                        $"%{s}%"
                    ) ||
                    EF.Functions.Like(pv.PROVEEDORNavigation.PERSONA_NIT ?? "", $"%{s}%") ||
                    EF.Functions.Like(pv.PROVEEDORNavigation.PERSONA_CUI ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por Empresa (texto)
            if (!string.IsNullOrWhiteSpace(Empresa))
            {
                string e = Empresa.Trim();
                q = q.Where(pv => pv.EMPRESA != null && EF.Functions.Like(pv.EMPRESA, $"%{e}%"));
            }

            // 4) Estado
            if (Estado.HasValue)
                q = q.Where(pv => pv.ESTADO == Estado.Value);

            // 5) Orden (incluye orden por NOMBRE COMPLETO y EMPRESA)
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(pv => pv.PROVEEDOR_ID) : q.OrderByDescending(pv => pv.PROVEEDOR_ID),

                "nombre" => asc
                    ? q.OrderBy(pv =>
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA ?? ""))
                    : q.OrderByDescending(pv =>
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA ?? "")),

                "empresa" => asc ? q.OrderBy(pv => pv.EMPRESA) : q.OrderByDescending(pv => pv.EMPRESA),
                "estado" => asc ? q.OrderBy(pv => pv.ESTADO) : q.OrderByDescending(pv => pv.ESTADO),
                _ => asc ? q.OrderBy(pv => pv.FECHA_CREACION) : q.OrderByDescending(pv => pv.FECHA_CREACION),
            };

            // 6) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // 7) Include navegación a PERSONA
            var qWithNavs = q.Include(pv => pv.PROVEEDORNavigation);

            var items = await qWithNavs
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 8) Diccionario ID -> Nombre Completo para la vista
            ViewBag.NombreCompletoMap = items.ToDictionary(
                pv => pv.PROVEEDOR_ID,
                pv => ComposeFullName(pv.PROVEEDORNavigation)
            );

            // 9) VM de salida
            var vm = new ProveedorViewModels
            {
                Items = items,
                Search = Search,
                Empresa = Empresa,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = Page,
                PageSize = PageSize,
                TotalPages = totalPages,
                TotalItems = total
            };

            return View(vm);
        }

        // ============================================================
        // DETAILS (Tarjeta/Modal) – /Proveedores/DetailsCard?id=...
        // Muestra PERSONA + PROVEEDOR en un VM simplificado.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.PROVEEDOR
                .AsNoTracking()
                .Include(pv => pv.PROVEEDORNavigation)
                .Where(pv => pv.PROVEEDOR_ID == id && !pv.ELIMINADO)
                .Select(pv => new ProveedorDetailsVM
                {
                    PROVEEDOR_ID = pv.PROVEEDOR_ID,
                    PERSONA_ID = pv.PROVEEDORNavigation.PERSONA_ID,

                    // Nombre completo (6 partes) en un solo campo
                    Nombres =
                        (
                            (pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                            (pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                            (pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                            (pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                            (pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                            (pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA ?? "")
                        ).Trim(),

                    // PERSONA
                    NIT = pv.PROVEEDORNavigation.PERSONA_NIT,
                    DPI = pv.PROVEEDORNavigation.PERSONA_CUI,
                    Telefono = pv.PROVEEDORNavigation.PERSONA_TELEFONOMOVIL,
                    TelefonoCasa = pv.PROVEEDORNavigation.PERSONA_TELEFONOCASA,
                    Correo = pv.PROVEEDORNavigation.PERSONA_CORREO,
                    Direccion = pv.PROVEEDORNavigation.PERSONA_DIRECCION,

                    // PROVEEDOR
                    EMPRESA = pv.EMPRESA,
                    PROVEEDOR_OBSERVACION = pv.PROVEEDOR_OBSERVACION,
                    ESTADO = pv.ESTADO,

                    // Auditoría
                    FECHA_CREACION = pv.FECHA_CREACION,
                    USUARIO_CREACION = pv.USUARIO_CREACION,
                    FECHA_MODIFICACION = pv.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = pv.USUARIO_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm); // Vista parcial "Views/Proveedores/Details.cshtml"
        }

        // ============================================================
        // CREATE (GET) – RUTA: GET /Proveedores/Create
        // Muestra formulario con ID generado (PROVEEDOR_ID = PERSONA_ID).
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var personaId = await SiguientePersonaIdAsync(); // PE + 8 dígitos

            var vm = new ProveedorCreateVM
            {
                PROVEEDOR_ID = personaId,
                PERSONA_ID = personaId,
                ESTADO = true
            };

            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /Proveedores/Create
        // Crea PERSONA + PROVEEDOR en 1 transacción.
        // Valida campos obligatorios PERSONA y unicidad DPI/NIT.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProveedorCreateVM vm)
        {
            // Recalcular IDs en servidor (seguridad)
            var personaId = await SiguientePersonaIdAsync();
            vm.PERSONA_ID = personaId;
            vm.PROVEEDOR_ID = personaId; // PROVEEDOR_ID == PERSONA_ID

            // --------- VALIDACIONES DE PERSONA (mismas que en Empleados) ---------
            if (string.IsNullOrWhiteSpace(vm.PERSONA_PRIMERNOMBRE))
                ModelState.AddModelError(nameof(vm.PERSONA_PRIMERNOMBRE), "El primer nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_PRIMERAPELLIDO))
                ModelState.AddModelError(nameof(vm.PERSONA_PRIMERAPELLIDO), "El primer apellido es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_CUI))
                ModelState.AddModelError(nameof(vm.PERSONA_CUI), "El CUI/DPI es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_TELEFONOMOVIL))
                ModelState.AddModelError(nameof(vm.PERSONA_TELEFONOMOVIL), "El teléfono móvil es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_CORREO))
                ModelState.AddModelError(nameof(vm.PERSONA_CORREO), "El correo es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_DIRECCION))
                ModelState.AddModelError(nameof(vm.PERSONA_DIRECCION), "La dirección es obligatoria.");

            // Unicidad DPI/CUI y NIT (si trae valor)
            string dpiNorm = (vm.PERSONA_CUI ?? "").Trim();
            if (!string.IsNullOrEmpty(dpiNorm))
            {
                bool dupDpi = await _context.PERSONA
                    .AnyAsync(p => !p.ELIMINADO && p.PERSONA_CUI == dpiNorm);
                if (dupDpi)
                    ModelState.AddModelError(nameof(vm.PERSONA_CUI), "Ya existe un registro con este CUI/DPI.");
            }

            string nitNorm = (vm.PERSONA_NIT ?? "").Trim();
            if (!string.IsNullOrEmpty(nitNorm))
            {
                bool dupNit = await _context.PERSONA
                    .AnyAsync(p => !p.ELIMINADO && p.PERSONA_NIT == nitNorm);
                if (dupNit)
                    ModelState.AddModelError(nameof(vm.PERSONA_NIT), "Ya existe un registro con este NIT.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }
            // ---------------------------------------------------------------------

            // Normalización de strings -> null si vienen vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // -----------------------------
                // 1) PERSONA (map completo)
                // -----------------------------
                var persona = new PERSONA
                {
                    PERSONA_ID = personaId,
                    PERSONA_PRIMERNOMBRE = vm.PERSONA_PRIMERNOMBRE!.Trim(),
                    PERSONA_SEGUNDONOMBRE = s2(vm.PERSONA_SEGUNDONOMBRE),
                    PERSONA_TERCERNOMBRE = s2(vm.PERSONA_TERCERNOMBRE),
                    PERSONA_PRIMERAPELLIDO = vm.PERSONA_PRIMERAPELLIDO!.Trim(),
                    PERSONA_SEGUNDOAPELLIDO = s2(vm.PERSONA_SEGUNDOAPELLIDO),
                    PERSONA_APELLIDOCASADA = s2(vm.PERSONA_APELLIDOCASADA),
                    PERSONA_NIT = s2(vm.PERSONA_NIT),
                    PERSONA_CUI = s2(vm.PERSONA_CUI),
                    PERSONA_DIRECCION = s2(vm.PERSONA_DIRECCION),
                    PERSONA_TELEFONOCASA = s2(vm.PERSONA_TELEFONOCASA),
                    PERSONA_TELEFONOMOVIL = s2(vm.PERSONA_TELEFONOMOVIL),
                    PERSONA_CORREO = s2(vm.PERSONA_CORREO),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(persona);
                _context.PERSONA.Add(persona);
                await _context.SaveChangesAsync();

                // -----------------------------
                // 2) PROVEEDOR (map)
                // -----------------------------
                var proveedor = new PROVEEDOR
                {
                    PROVEEDOR_ID = personaId, // = PERSONA_ID
                    PROVEEDOR_OBSERVACION = s2(vm.PROVEEDOR_OBSERVACION),
                    EMPRESA = s2(vm.EMPRESA),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(proveedor);
                _context.PROVEEDOR.Add(proveedor);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                // PRG + SweetAlert
                TempData["SwalTitle"] = "¡Proveedor guardado!";
                TempData["SwalText"] = $"El registro \"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "Proveedores");
                TempData["SwalCreateUrl"] = Url.Action("Create", "Proveedores");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "Ocurrió un error al crear el proveedor. Intenta nuevamente.");
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /Proveedores/Edit/{id}
        // Carga PERSONA + PROVEEDOR en un VM de edición.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var pv = await _context.PROVEEDOR
                .Include(x => x.PROVEEDORNavigation)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PROVEEDOR_ID == id && !x.ELIMINADO);

            if (pv == null) return NotFound();

            var vm = new ProveedorCreateVM
            {
                // IDs
                PROVEEDOR_ID = pv.PROVEEDOR_ID,
                PERSONA_ID = pv.PROVEEDOR_ID,

                // PERSONA
                PERSONA_PRIMERNOMBRE = pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE,
                PERSONA_SEGUNDONOMBRE = pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE,
                PERSONA_TERCERNOMBRE = pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE,
                PERSONA_PRIMERAPELLIDO = pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO,
                PERSONA_SEGUNDOAPELLIDO = pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO,
                PERSONA_APELLIDOCASADA = pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA,
                PERSONA_NIT = pv.PROVEEDORNavigation.PERSONA_NIT,
                PERSONA_CUI = pv.PROVEEDORNavigation.PERSONA_CUI,
                PERSONA_DIRECCION = pv.PROVEEDORNavigation.PERSONA_DIRECCION,
                PERSONA_TELEFONOCASA = pv.PROVEEDORNavigation.PERSONA_TELEFONOCASA,
                PERSONA_TELEFONOMOVIL = pv.PROVEEDORNavigation.PERSONA_TELEFONOMOVIL,
                PERSONA_CORREO = pv.PROVEEDORNavigation.PERSONA_CORREO,

                // PROVEEDOR
                EMPRESA = pv.EMPRESA,
                PROVEEDOR_OBSERVACION = pv.PROVEEDOR_OBSERVACION,

                ESTADO = pv.ESTADO
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /Proveedores/Edit/{id}
        // Actualiza PERSONA + PROVEEDOR. Valida unicidad DPI/NIT
        // (excluyendo el propio registro).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ProveedorCreateVM vm)
        {
            if (id != vm.PROVEEDOR_ID) return NotFound();

            // Validaciones PERSONA (mismas que en Create)
            if (string.IsNullOrWhiteSpace(vm.PERSONA_PRIMERNOMBRE))
                ModelState.AddModelError(nameof(vm.PERSONA_PRIMERNOMBRE), "El primer nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_PRIMERAPELLIDO))
                ModelState.AddModelError(nameof(vm.PERSONA_PRIMERAPELLIDO), "El primer apellido es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_CUI))
                ModelState.AddModelError(nameof(vm.PERSONA_CUI), "El CUI/DPI es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_TELEFONOMOVIL))
                ModelState.AddModelError(nameof(vm.PERSONA_TELEFONOMOVIL), "El teléfono móvil es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_CORREO))
                ModelState.AddModelError(nameof(vm.PERSONA_CORREO), "El correo es obligatorio.");

            if (string.IsNullOrWhiteSpace(vm.PERSONA_DIRECCION))
                ModelState.AddModelError(nameof(vm.PERSONA_DIRECCION), "La dirección es obligatoria.");

            // Unicidad DPI/NIT (excluye el propio)
            string dpiNorm = (vm.PERSONA_CUI ?? "").Trim();
            if (!string.IsNullOrEmpty(dpiNorm))
            {
                bool dupDpi = await _context.PERSONA
                    .AnyAsync(p => !p.ELIMINADO && p.PERSONA_CUI == dpiNorm && p.PERSONA_ID != id);
                if (dupDpi)
                    ModelState.AddModelError(nameof(vm.PERSONA_CUI), "Ya existe un registro con este CUI/DPI.");
            }

            string nitNorm = (vm.PERSONA_NIT ?? "").Trim();
            if (!string.IsNullOrEmpty(nitNorm))
            {
                bool dupNit = await _context.PERSONA
                    .AnyAsync(p => !p.ELIMINADO && p.PERSONA_NIT == nitNorm && p.PERSONA_ID != id);
                if (dupNit)
                    ModelState.AddModelError(nameof(vm.PERSONA_NIT), "Ya existe un registro con este NIT.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var proveedor = await _context.PROVEEDOR
                .Include(x => x.PROVEEDORNavigation)
                .FirstOrDefaultAsync(x => x.PROVEEDOR_ID == id && !x.ELIMINADO);

            if (proveedor == null) return NotFound();

            // Normalización
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // -----------------------------
            // PERSONA (aplicar cambios)
            // -----------------------------
            proveedor.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE = vm.PERSONA_PRIMERNOMBRE!.Trim();
            proveedor.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE = s2(vm.PERSONA_SEGUNDONOMBRE);
            proveedor.PROVEEDORNavigation.PERSONA_TERCERNOMBRE = s2(vm.PERSONA_TERCERNOMBRE);
            proveedor.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO = vm.PERSONA_PRIMERAPELLIDO!.Trim();
            proveedor.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO = s2(vm.PERSONA_SEGUNDOAPELLIDO);
            proveedor.PROVEEDORNavigation.PERSONA_APELLIDOCASADA = s2(vm.PERSONA_APELLIDOCASADA);
            proveedor.PROVEEDORNavigation.PERSONA_NIT = s2(vm.PERSONA_NIT);
            proveedor.PROVEEDORNavigation.PERSONA_CUI = s2(vm.PERSONA_CUI);
            proveedor.PROVEEDORNavigation.PERSONA_DIRECCION = s2(vm.PERSONA_DIRECCION);
            proveedor.PROVEEDORNavigation.PERSONA_TELEFONOCASA = s2(vm.PERSONA_TELEFONOCASA);
            proveedor.PROVEEDORNavigation.PERSONA_TELEFONOMOVIL = s2(vm.PERSONA_TELEFONOMOVIL);
            proveedor.PROVEEDORNavigation.PERSONA_CORREO = s2(vm.PERSONA_CORREO);
            proveedor.PROVEEDORNavigation.ESTADO = vm.ESTADO;

            // -----------------------------
            // PROVEEDOR (aplicar cambios)
            // -----------------------------
            proveedor.EMPRESA = s2(vm.EMPRESA);
            proveedor.PROVEEDOR_OBSERVACION = s2(vm.PROVEEDOR_OBSERVACION);
            proveedor.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(proveedor.PROVEEDORNavigation);
            _audit.StampUpdate(proveedor);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Proveedor actualizado!";
            TempData["SwalText"] = $"\"{proveedor.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE} {proveedor.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = proveedor.PROVEEDOR_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico del PROVEEDOR
        // (No elimina PERSONA para preservar integridad)
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var pv = await _context.PROVEEDOR
                .AsNoTracking()
                .Include(x => x.PROVEEDORNavigation)
                .FirstOrDefaultAsync(x => x.PROVEEDOR_ID == id);

            if (pv == null) return NotFound();
            return View(pv);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pv = await _context.PROVEEDOR.FindAsync(id);
            if (pv == null) return NotFound();

            _audit.StampSoftDelete(pv);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo PE00000001 para PERSONA (y PROVEEDOR usa el mismo)
        private async Task<string> SiguientePersonaIdAsync()
        {
            const string prefijo = "PE"; // Cambia a "PR" si quieres distinguir proveedores.
            const int ancho = 8;

            var ids = await _context.PERSONA
                .Select(p => p.PERSONA_ID)
                .Where(id => id.StartsWith(prefijo))
                .ToListAsync();

            int maxNum = 0;
            var rx = new Regex(@"^" + prefijo + @"(?<n>\d+)$");
            foreach (var id in ids)
            {
                var m = rx.Match(id);
                if (m.Success && int.TryParse(m.Groups["n"].Value, out int n))
                    if (n > maxNum) maxNum = n;
            }

            var siguiente = maxNum + 1;
            return prefijo + siguiente.ToString(new string('0', ancho));
        }


        //=======================REPORTE PDF
        [HttpGet]
        public async Task<IActionResult> ReportePDF(
    string? Search,
    string? Empresa,
    bool? Estado,
    string Sort = "id",
    string Dir = "asc")
        {
            // 1) Base (no eliminados)
            IQueryable<PROVEEDOR> q = _context.PROVEEDOR
                .AsNoTracking()
                .Where(pv => !pv.ELIMINADO);

            // 2) Búsqueda global
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();

                q = q.Where(pv =>
                    EF.Functions.Like(pv.PROVEEDOR_ID, $"%{s}%") ||
                    EF.Functions.Like(pv.EMPRESA ?? "", $"%{s}%") ||
                    EF.Functions.Like(
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA ?? ""),
                        $"%{s}%"
                    ) ||
                    EF.Functions.Like(pv.PROVEEDORNavigation.PERSONA_NIT ?? "", $"%{s}%") ||
                    EF.Functions.Like(pv.PROVEEDORNavigation.PERSONA_CUI ?? "", $"%{s}%")
                );
            }

            // 3) Filtro Empresa
            if (!string.IsNullOrWhiteSpace(Empresa))
            {
                string e = Empresa.Trim();
                q = q.Where(pv => pv.EMPRESA != null && EF.Functions.Like(pv.EMPRESA, $"%{e}%"));
            }

            // 4) Estado
            if (Estado.HasValue)
                q = q.Where(pv => pv.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(pv => pv.PROVEEDOR_ID) : q.OrderByDescending(pv => pv.PROVEEDOR_ID),

                "nombre" => asc
                    ? q.OrderBy(pv =>
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA ?? ""))
                    : q.OrderByDescending(pv =>
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (pv.PROVEEDORNavigation.PERSONA_APELLIDOCASADA ?? "")),

                "empresa" => asc ? q.OrderBy(pv => pv.EMPRESA) : q.OrderByDescending(pv => pv.EMPRESA),
                "estado" => asc ? q.OrderBy(pv => pv.ESTADO) : q.OrderByDescending(pv => pv.ESTADO),
                _ => asc ? q.OrderBy(pv => pv.FECHA_CREACION) : q.OrderByDescending(pv => pv.FECHA_CREACION),
            };

            // 6) Traer TODOS los datos (sin paginar), con PERSONA
            var items = await q
                .Include(pv => pv.PROVEEDORNavigation)
                .ToListAsync();

            int totActivos = items.Count(pv => pv.ESTADO);
            int totInactivos = items.Count(pv => !pv.ESTADO);

            // 7) ViewModel genérico
            var vm = new ReporteViewModel<PROVEEDOR>
            {
                Items = items,
                Search = Search,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = 1,
                PageSize = items.Count,
                TotalItems = items.Count,
                TotalPages = 1,
                ReportTitle = "Reporte de Proveedores",
                CompanyInfo = "CreArte Manualidades | Sololá, Guatemala | creartemanualidades2021@gmail.com",
                GeneratedBy = User?.Identity?.Name ?? "Usuario no autenticado",
                LogoUrl = Url.Content("~/Imagenes/logoCreArte.png")
            };

            vm.AddTotal("Activos", totActivos);
            vm.AddTotal("Inactivos", totInactivos);
            if (!string.IsNullOrWhiteSpace(Empresa))
                vm.ExtraFilters["Empresa"] = Empresa;

            // 8) PDF
            var pdf = new ViewAsPdf("ReporteProveedores", vm)
            {
                FileName = $"ReporteProveedores.pdf",
                ContentDisposition = ContentDisposition.Inline,
                PageSize = Size.Letter,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins { Left = 10, Right = 10, Top = 15, Bottom = 15 },
                CustomSwitches =
                    $"--footer-center \"Página [page] de [toPage]\"" +
                    $" --footer-right \"CreArte Manualidades © {DateTime.Now:yyyy}\"" +
                    $" --footer-font-size 9 --footer-spacing 3 --footer-line"
            };

            return pdf;
        }
    }
}
