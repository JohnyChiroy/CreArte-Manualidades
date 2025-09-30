using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Auditoria; // IAuditoriaService
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CreArte.Controllers
{
    public class ClientesController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public ClientesController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO – /Clientes?...
        // Filtros: Search (global), TipoCliente, Estado
        // Orden: id | nombre | tipo | estado | fecha
        // Paginación: Page/PageSize
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? Search,
            string? TipoCliente,
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // Helper local para armar nombre completo
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

            // 1) Base
            IQueryable<CLIENTE> q = _context.CLIENTE.Where(c => !c.ELIMINADO);

            // 2) Búsqueda global (ID, Tipo, Nombre completo, NIT, DPI, Teléfono, Correo)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();

                q = q.Where(c =>
                    EF.Functions.Like(c.CLIENTE_ID, $"%{s}%") ||
                    EF.Functions.Like(c.TIPO_CLIENTE.TIPO_CLIENTE_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(
                        (c.CLIENTENavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_APELLIDOCASADA ?? ""),
                        $"%{s}%"
                    ) ||
                    EF.Functions.Like(c.CLIENTENavigation.PERSONA_NIT ?? "", $"%{s}%") ||
                    EF.Functions.Like(c.CLIENTENavigation.PERSONA_CUI ?? "", $"%{s}%") ||
                    EF.Functions.Like(c.CLIENTENavigation.PERSONA_TELEFONOMOVIL ?? "", $"%{s}%") ||
                    EF.Functions.Like(c.CLIENTENavigation.PERSONA_CORREO ?? "", $"%{s}%")
                );
            }

            // 3) Filtro por Tipo de Cliente
            if (!string.IsNullOrWhiteSpace(TipoCliente))
            {
                if (TipoCliente == "__BLANKS__")
                    q = q.Where(c => c.TIPO_CLIENTE == null || string.IsNullOrEmpty(c.TIPO_CLIENTE.TIPO_CLIENTE_NOMBRE));
                else if (TipoCliente == "__NONBLANKS__")
                    q = q.Where(c => c.TIPO_CLIENTE != null && !string.IsNullOrEmpty(c.TIPO_CLIENTE.TIPO_CLIENTE_NOMBRE));
                else
                {
                    string s = TipoCliente.Trim();
                    q = q.Where(c => EF.Functions.Like(c.TIPO_CLIENTE.TIPO_CLIENTE_NOMBRE, $"%{s}%"));
                }
            }

            // 4) Estado
            if (Estado.HasValue)
                q = q.Where(c => c.ESTADO == Estado.Value);

            // 5) Orden
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(c => c.CLIENTE_ID) : q.OrderByDescending(c => c.CLIENTE_ID),

                "nombre" => asc
                    ? q.OrderBy(c =>
                        (c.CLIENTENavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_APELLIDOCASADA ?? ""))
                    : q.OrderByDescending(c =>
                        (c.CLIENTENavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (c.CLIENTENavigation.PERSONA_APELLIDOCASADA ?? "")),

                "tipo" => asc ? q.OrderBy(c => c.TIPO_CLIENTE.TIPO_CLIENTE_NOMBRE) : q.OrderByDescending(c => c.TIPO_CLIENTE.TIPO_CLIENTE_NOMBRE),
                "estado" => asc ? q.OrderBy(c => c.ESTADO) : q.OrderByDescending(c => c.ESTADO),
                _ => asc ? q.OrderBy(c => c.FECHA_CREACION) : q.OrderByDescending(c => c.FECHA_CREACION),
            };

            // 6) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // 7) Include
            var qWithNavs = q.Include(c => c.CLIENTENavigation)
                             .Include(c => c.TIPO_CLIENTE);

            var items = await qWithNavs
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 8) Mapa de Nombres
            ViewBag.NombreCompletoMap = items.ToDictionary(
                c => c.CLIENTE_ID,
                c => ComposeFullName(c.CLIENTENavigation)
            );

            // 9) VM salida
            var vm = new ClienteViewModels
            {
                Items = items,
                Search = Search,
                TipoCliente = TipoCliente,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = Page,
                PageSize = PageSize,
                TotalPages = totalPages,
                TotalItems = total,
                TiposCliente = await CargarTiposClienteAsync()
            };

            return View(vm);
        }

        // ============================================================
        // DETAILS para modal (PartialView) – /Clientes/DetailsCard?id=...
        // Nombres = Nombre COMPLETO (6 partes)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.CLIENTE
                .AsNoTracking()
                .Include(c => c.CLIENTENavigation)
                .Include(c => c.TIPO_CLIENTE)
                .Where(c => c.CLIENTE_ID == id && !c.ELIMINADO)
                .Select(c => new ClienteDetailsVM
                {
                    CLIENTE_ID = c.CLIENTE_ID,
                    PERSONA_ID = c.CLIENTE_ID,

                    // Nombre completo (6 partes, ignorando nulls)
                    Nombres =
                        (
                            (c.CLIENTENavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                            (c.CLIENTENavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                            (c.CLIENTENavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                            (c.CLIENTENavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                            (c.CLIENTENavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                            (c.CLIENTENavigation.PERSONA_APELLIDOCASADA ?? "")
                        ).Trim(),

                    // PERSONA
                    NIT = c.CLIENTENavigation.PERSONA_NIT,
                    DPI = c.CLIENTENavigation.PERSONA_CUI,
                    TelefonoCasa = c.CLIENTENavigation.PERSONA_TELEFONOCASA,
                    Telefono = c.CLIENTENavigation.PERSONA_TELEFONOMOVIL,
                    Correo = c.CLIENTENavigation.PERSONA_CORREO,
                    Direccion = c.CLIENTENavigation.PERSONA_DIRECCION,

                    // CLIENTE
                    CLIENTE_NOTA = c.CLIENTE_NOTA,
                    TIPO_CLIENTE_ID = c.TIPO_CLIENTE_ID,
                    TIPO_CLIENTE_NOMBRE = c.TIPO_CLIENTE.TIPO_CLIENTE_NOMBRE,
                    ESTADO = c.ESTADO,

                    // Auditoría
                    FECHA_CREACION = c.FECHA_CREACION,
                    USUARIO_CREACION = c.USUARIO_CREACION,
                    FECHA_MODIFICACION = c.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = c.USUARIO_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm);
        }

        // ============================================================
        // CREATE (GET) – /Clientes/Create
        // Muestra formulario con ID generado y combo de tipo cliente.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var personaId = await SiguientePersonaIdAsync(); // PE + 8 dígitos

            var vm = new ClienteCreateVM
            {
                CLIENTE_ID = personaId,
                PERSONA_ID = personaId,
                ESTADO = true,
                TiposCliente = await CargarTiposClienteAsync()
            };

            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – /Clientes/Create
        // Crea PERSONA + CLIENTE en 1 transacción.
        // + Valida unicidad de DPI/NIT y FK TipoCliente (activo y no eliminado).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteCreateVM vm)
        {
            // Recalcular IDs en servidor (seguridad)
            var personaId = await SiguientePersonaIdAsync();
            vm.PERSONA_ID = personaId;
            vm.CLIENTE_ID = personaId; // CLIENTE_ID == PERSONA_ID

            // ===== VALIDACIONES PERSONA (OBLIGATORIOS) =====
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

            // Validar FK TipoCliente (activo y no eliminado)
            bool tipoOk = !string.IsNullOrWhiteSpace(vm.TIPO_CLIENTE_ID)
                          && await _context.TIPO_CLIENTE.AnyAsync(t => t.TIPO_CLIENTE_ID == vm.TIPO_CLIENTE_ID && !t.ELIMINADO && t.ESTADO);
            if (!tipoOk)
                ModelState.AddModelError(nameof(vm.TIPO_CLIENTE_ID), "El tipo de cliente seleccionado no existe o no está activo.");

            // ===== UNICIDAD DPI / NIT =====
            string dpiNorm = (vm.PERSONA_CUI ?? "").Trim();
            if (!string.IsNullOrEmpty(dpiNorm))
            {
                bool dupDpi = await _context.PERSONA.AnyAsync(p => !p.ELIMINADO && p.PERSONA_CUI == dpiNorm);
                if (dupDpi)
                    ModelState.AddModelError(nameof(vm.PERSONA_CUI), "Ya existe un registro con este CUI/DPI.");
            }

            string nitNorm = (vm.PERSONA_NIT ?? "").Trim();
            if (!string.IsNullOrEmpty(nitNorm))
            {
                bool dupNit = await _context.PERSONA.AnyAsync(p => !p.ELIMINADO && p.PERSONA_NIT == nitNorm);
                if (dupNit)
                    ModelState.AddModelError(nameof(vm.PERSONA_NIT), "Ya existe un registro con este NIT.");
            }

            if (!ModelState.IsValid)
            {
                vm.TiposCliente = await CargarTiposClienteAsync();
                return View(vm);
            }

            // Normalizador: string vacío -> null
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1) PERSONA
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

                // 2) CLIENTE
                var cliente = new CLIENTE
                {
                    CLIENTE_ID = personaId, // = PERSONA_ID
                    CLIENTE_NOTA = s2(vm.CLIENTE_NOTA),
                    TIPO_CLIENTE_ID = vm.TIPO_CLIENTE_ID!,
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(cliente);
                _context.CLIENTE.Add(cliente);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                // PRG + SweetAlert (mismo patrón que Empleados)
                TempData["SwalTitle"] = "¡Cliente guardado!";
                TempData["SwalText"] = $"El registro \"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "Clientes");
                TempData["SwalCreateUrl"] = Url.Action("Create", "Clientes");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "Ocurrió un error al crear el cliente. Intenta nuevamente.");
                vm.TiposCliente = await CargarTiposClienteAsync();
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – /Clientes/Edit/{id}
        // Carga PERSONA + CLIENTE para edición (no hay DateOnly aquí).
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.CLIENTE
                .Include(x => x.CLIENTENavigation)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CLIENTE_ID == id && !x.ELIMINADO);

            if (c == null) return NotFound();

            var vm = new ClienteCreateVM
            {
                // IDs
                CLIENTE_ID = c.CLIENTE_ID,
                PERSONA_ID = c.CLIENTE_ID,

                // PERSONA
                PERSONA_PRIMERNOMBRE = c.CLIENTENavigation.PERSONA_PRIMERNOMBRE,
                PERSONA_SEGUNDONOMBRE = c.CLIENTENavigation.PERSONA_SEGUNDONOMBRE,
                PERSONA_TERCERNOMBRE = c.CLIENTENavigation.PERSONA_TERCERNOMBRE,
                PERSONA_PRIMERAPELLIDO = c.CLIENTENavigation.PERSONA_PRIMERAPELLIDO,
                PERSONA_SEGUNDOAPELLIDO = c.CLIENTENavigation.PERSONA_SEGUNDOAPELLIDO,
                PERSONA_APELLIDOCASADA = c.CLIENTENavigation.PERSONA_APELLIDOCASADA,
                PERSONA_NIT = c.CLIENTENavigation.PERSONA_NIT,
                PERSONA_CUI = c.CLIENTENavigation.PERSONA_CUI,
                PERSONA_DIRECCION = c.CLIENTENavigation.PERSONA_DIRECCION,
                PERSONA_TELEFONOCASA = c.CLIENTENavigation.PERSONA_TELEFONOCASA,
                PERSONA_TELEFONOMOVIL = c.CLIENTENavigation.PERSONA_TELEFONOMOVIL,
                PERSONA_CORREO = c.CLIENTENavigation.PERSONA_CORREO,

                // CLIENTE
                CLIENTE_NOTA = c.CLIENTE_NOTA,
                TIPO_CLIENTE_ID = c.TIPO_CLIENTE_ID,
                ESTADO = c.ESTADO,

                // Combos
                TiposCliente = await CargarTiposClienteAsync()
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – /Clientes/Edit/{id}
        // Actualiza PERSONA + CLIENTE. Valida DPI/NIT (excluye propio) y FK.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ClienteCreateVM vm)
        {
            if (id != vm.CLIENTE_ID) return NotFound();

            // ===== VALIDACIONES PERSONA (OBLIGATORIOS) =====
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

            // Validar FK TipoCliente (activo y no eliminado)
            bool tipoOk = !string.IsNullOrWhiteSpace(vm.TIPO_CLIENTE_ID)
                          && await _context.TIPO_CLIENTE.AnyAsync(t => t.TIPO_CLIENTE_ID == vm.TIPO_CLIENTE_ID && !t.ELIMINADO && t.ESTADO);
            if (!tipoOk)
                ModelState.AddModelError(nameof(vm.TIPO_CLIENTE_ID), "El tipo de cliente seleccionado no existe o no está activo.");

            // ===== UNICIDAD DPI / NIT (excluyendo el propio) =====
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
                vm.TiposCliente = await CargarTiposClienteAsync();
                return View(vm);
            }

            var cli = await _context.CLIENTE
                .Include(x => x.CLIENTENavigation)
                .FirstOrDefaultAsync(x => x.CLIENTE_ID == id && !x.ELIMINADO);
            if (cli == null) return NotFound();

            // Normalizador: string vacío -> null
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // PERSONA (aplicar cambios)
            cli.CLIENTENavigation.PERSONA_PRIMERNOMBRE = vm.PERSONA_PRIMERNOMBRE!.Trim();
            cli.CLIENTENavigation.PERSONA_SEGUNDONOMBRE = s2(vm.PERSONA_SEGUNDONOMBRE);
            cli.CLIENTENavigation.PERSONA_TERCERNOMBRE = s2(vm.PERSONA_TERCERNOMBRE);
            cli.CLIENTENavigation.PERSONA_PRIMERAPELLIDO = vm.PERSONA_PRIMERAPELLIDO!.Trim();
            cli.CLIENTENavigation.PERSONA_SEGUNDOAPELLIDO = s2(vm.PERSONA_SEGUNDOAPELLIDO);
            cli.CLIENTENavigation.PERSONA_APELLIDOCASADA = s2(vm.PERSONA_APELLIDOCASADA);
            cli.CLIENTENavigation.PERSONA_NIT = s2(vm.PERSONA_NIT);
            cli.CLIENTENavigation.PERSONA_CUI = s2(vm.PERSONA_CUI);
            cli.CLIENTENavigation.PERSONA_DIRECCION = s2(vm.PERSONA_DIRECCION);
            cli.CLIENTENavigation.PERSONA_TELEFONOCASA = s2(vm.PERSONA_TELEFONOCASA);
            cli.CLIENTENavigation.PERSONA_TELEFONOMOVIL = s2(vm.PERSONA_TELEFONOMOVIL);
            cli.CLIENTENavigation.PERSONA_CORREO = s2(vm.PERSONA_CORREO);
            cli.CLIENTENavigation.ESTADO = vm.ESTADO;

            // CLIENTE (aplicar cambios)
            cli.CLIENTE_NOTA = s2(vm.CLIENTE_NOTA);
            cli.TIPO_CLIENTE_ID = vm.TIPO_CLIENTE_ID!;
            cli.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(cli.CLIENTENavigation);
            _audit.StampUpdate(cli);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Cliente actualizado!";
            TempData["SwalText"] = $"\"{cli.CLIENTENavigation.PERSONA_PRIMERNOMBRE} {cli.CLIENTENavigation.PERSONA_PRIMERAPELLIDO}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = cli.CLIENTE_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico del CLIENTE
        // (No elimina PERSONA para preservar integridad referencial)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var c = await _context.CLIENTE
                .AsNoTracking()
                .Include(x => x.CLIENTENavigation)
                .Include(x => x.TIPO_CLIENTE)
                .FirstOrDefaultAsync(x => x.CLIENTE_ID == id);

            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var c = await _context.CLIENTE.FindAsync(id);
            if (c == null) return NotFound();

            _audit.StampSoftDelete(c);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo PE00000001 para PERSONA (y CLIENTE usa el mismo)
        private async Task<string> SiguientePersonaIdAsync()
        {
            const string prefijo = "PE";
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

        // Carga Tipos de Cliente activos y no eliminados (para combo)
        private async Task<List<SelectListItem>> CargarTiposClienteAsync()
        {
            return await _context.TIPO_CLIENTE
                .Where(t => !t.ELIMINADO && t.ESTADO)
                .OrderBy(t => t.TIPO_CLIENTE_NOMBRE)
                .Select(t => new SelectListItem
                {
                    Text = t.TIPO_CLIENTE_NOMBRE,
                    Value = t.TIPO_CLIENTE_ID
                })
                .ToListAsync();
        }
    }
}
