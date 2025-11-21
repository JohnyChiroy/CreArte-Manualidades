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
    public class EmpleadosController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IAuditoriaService _audit;

        public EmpleadosController(CreArteDbContext context, IAuditoriaService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ============================================================
        // LISTADO /Empleados?...
        // ============================================================
        public async Task<IActionResult> Index(
    string? Search,
    string? Puesto,
    string? Genero,
    DateTime? FechaIngresoIni,  // del request (DateTime?)
    DateTime? FechaIngresoFin,  // del request (DateTime?)
    bool? Estado,
    string Sort = "id",
    string Dir = "asc",
    int Page = 1,
    int PageSize = 10)
        {
            
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
            IQueryable<EMPLEADO> q = _context.EMPLEADO.Where(e => !e.ELIMINADO);

            // 2) Búsqueda global (ID, Puesto, Nombre completo)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();

                q = q.Where(e =>
                    EF.Functions.Like(e.EMPLEADO_ID, $"%{s}%") ||
                    EF.Functions.Like(e.PUESTO.PUESTO_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(
                        (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? ""),
                        $"%{s}%"
                    )
                );
            }

            // 3) Filtro por Puesto
            if (!string.IsNullOrWhiteSpace(Puesto))
            {
                if (Puesto == "__BLANKS__")
                    q = q.Where(e => e.PUESTO == null || string.IsNullOrEmpty(e.PUESTO.PUESTO_NOMBRE));
                else if (Puesto == "__NONBLANKS__")
                    q = q.Where(e => e.PUESTO != null && !string.IsNullOrEmpty(e.PUESTO.PUESTO_NOMBRE));
                else
                {
                    string s = Puesto.Trim();
                    q = q.Where(e => EF.Functions.Like(e.PUESTO.PUESTO_NOMBRE, $"%{s}%"));
                }
            }

            // 4) Filtro por Género
            if (!string.IsNullOrWhiteSpace(Genero))
            {
                string g = Genero.Trim();
                q = q.Where(e => e.EMPLEADO_GENERO != null && e.EMPLEADO_GENERO == g);
            }

            // 5) Filtros por FECHA DE INGRESO (DateTime? -> DateOnly)
            if (FechaIngresoIni.HasValue)
            {
                var fIni = DateOnly.FromDateTime(FechaIngresoIni.Value.Date);
                q = q.Where(e => e.EMPLEADO_FECHAINGRESO >= fIni);
            }
            if (FechaIngresoFin.HasValue)
            {
                var fFin = DateOnly.FromDateTime(FechaIngresoFin.Value.Date);
                q = q.Where(e => e.EMPLEADO_FECHAINGRESO <= fFin);
            }

            // 6) Estado
            if (Estado.HasValue)
                q = q.Where(e => e.ESTADO == Estado.Value);

            // 7) Orden (incluye orden por NOMBRE COMPLETO)
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(e => e.EMPLEADO_ID) : q.OrderByDescending(e => e.EMPLEADO_ID),

                // *** Orden por nombre completo concatenado ***
                "nombre" => asc
                    ? q.OrderBy(e =>
                        (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? ""))
                    : q.OrderByDescending(e =>
                        (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? "")),

                "puesto" => asc ? q.OrderBy(e => e.PUESTO.PUESTO_NOMBRE) : q.OrderByDescending(e => e.PUESTO.PUESTO_NOMBRE),
                "ingreso" => asc ? q.OrderBy(e => e.EMPLEADO_FECHAINGRESO) : q.OrderByDescending(e => e.EMPLEADO_FECHAINGRESO),
                "estado" => asc ? q.OrderBy(e => e.ESTADO) : q.OrderByDescending(e => e.ESTADO),
                _ => asc ? q.OrderBy(e => e.FECHA_CREACION) : q.OrderByDescending(e => e.FECHA_CREACION),
            };

            // 8) Paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // 9) Include
            var qWithNavs = q.Include(e => e.EMPLEADONavigation)
                             .Include(e => e.PUESTO);

            var items = await qWithNavs
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 9.1) Enviamos a la vista un diccionario EMPLEADO_ID -> NOMBRE COMPLETO
            ViewBag.NombreCompletoMap = items.ToDictionary(
                e => e.EMPLEADO_ID,
                e => ComposeFullName(e.EMPLEADONavigation)
            );

            // 10) VM salida (mantén DateTime? en VM para inputs)
            var vm = new EmpleadoViewModels
            {
                Items = items,
                Search = Search,
                Puesto = Puesto,
                Genero = Genero,
                FechaIngresoIni = FechaIngresoIni,
                FechaIngresoFin = FechaIngresoFin,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = Page,
                PageSize = PageSize,
                TotalPages = totalPages,
                TotalItems = total,
                Puestos = await CargarPuestosAsync(),
                Generos = GetGenerosSelect()
            };

            return View(vm);
        }

        // ============================================================
        // DETAILS  – /Empleados/DetailsCard?id=...
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DetailsCard(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta el id.");

            var vm = await _context.EMPLEADO
                .AsNoTracking()
                .Include(e => e.EMPLEADONavigation)
                .Include(e => e.PUESTO)
                .Where(e => e.EMPLEADO_ID == id && !e.ELIMINADO)
                .Select(e => new EmpleadoDetailsVM
                {
                    EMPLEADO_ID = e.EMPLEADO_ID,
                    PERSONA_ID = e.EMPLEADONavigation.PERSONA_ID,

                    Nombres =
                        (
                            (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                            (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? "")
                        ).Trim(),

                    // Dejamos Apellidos vacío intencionalmente para que la vista
                    // use solo Model.Nombres (que ya trae el nombre completo).
                    Apellidos = "",

                    // PERSONA (otros datos)
                    NIT = e.EMPLEADONavigation.PERSONA_NIT,
                    TelefonoCasa = e.EMPLEADONavigation.PERSONA_TELEFONOCASA,
                    DPI = e.EMPLEADONavigation.PERSONA_CUI,
                    Telefono = e.EMPLEADONavigation.PERSONA_TELEFONOMOVIL,
                    Correo = e.EMPLEADONavigation.PERSONA_CORREO,
                    Direccion = e.EMPLEADONavigation.PERSONA_DIRECCION,

                    // EMPLEADO
                    EMPLEADO_FECHANACIMIENTO = e.EMPLEADO_FECHANACIMIENTO,
                    EMPLEADO_FECHAINGRESO = e.EMPLEADO_FECHAINGRESO,
                    EMPLEADO_GENERO = e.EMPLEADO_GENERO,
                    PUESTO_ID = e.PUESTO_ID,
                    PUESTO_NOMBRE = e.PUESTO.PUESTO_NOMBRE,
                    ESTADO = e.ESTADO,

                    // Auditoría
                    FECHA_CREACION = e.FECHA_CREACION,
                    USUARIO_CREACION = e.USUARIO_CREACION,
                    FECHA_MODIFICACION = e.FECHA_MODIFICACION,
                    USUARIO_MODIFICACION = e.USUARIO_MODIFICACION
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();
            return PartialView("Details", vm);
        }

        // ============================================================
        // CREATE (GET) – RUTA: GET /Empleados/Create
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var personaId = await SiguientePersonaIdAsync(); // PE + 8 dígitos

            var vm = new EmpleadoCreateVM
            {
                // IDs (EMPLEADO_ID = PERSONA_ID)
                EMPLEADO_ID = personaId,
                PERSONA_ID = personaId,

                // Defaults para el formulario (VM usa DateTime? en inputs)
                EMPLEADO_FECHAINGRESO = DateTime.Today,
                ESTADO = true,

                // Combos
                Puestos = await CargarPuestosAsync(),
                Generos = GetGenerosSelect()
            };

            return View(vm);
        }

        // ============================================================
        // CREATE (POST) – RUTA: POST /Empleados/Create
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmpleadoCreateVM vm)
        {
            // Recalcular IDs en servidor (seguridad)
            var personaId = await SiguientePersonaIdAsync();
            vm.PERSONA_ID = personaId;
            vm.EMPLEADO_ID = personaId; // EMPLEADO_ID == PERSONA_ID

            // --------- VALIDACIONES DE LOS 10 CAMPOS OBLIGATORIOS ---------
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

            if (string.IsNullOrWhiteSpace(vm.EMPLEADO_GENERO))
                ModelState.AddModelError(nameof(vm.EMPLEADO_GENERO), "El género es obligatorio.");

            if (!vm.EMPLEADO_FECHANACIMIENTO.HasValue)
                ModelState.AddModelError(nameof(vm.EMPLEADO_FECHANACIMIENTO), "La fecha de nacimiento es obligatoria.");

            if (!vm.EMPLEADO_FECHAINGRESO.HasValue)
                ModelState.AddModelError(nameof(vm.EMPLEADO_FECHAINGRESO), "La fecha de ingreso es obligatoria.");

            // Validar FK Puesto (activo y no eliminado)
            bool puestoOk = !string.IsNullOrWhiteSpace(vm.PUESTO_ID)
                            && await _context.PUESTO.AnyAsync(p => p.PUESTO_ID == vm.PUESTO_ID && !p.ELIMINADO && p.ESTADO);
            if (!puestoOk)
                ModelState.AddModelError(nameof(vm.PUESTO_ID), "El puesto seleccionado no existe o no está activo.");

            // ============================
            // VALIDACIÓN DE EDAD > 12
            // ============================
            if (vm.EMPLEADO_FECHANACIMIENTO.HasValue && vm.EMPLEADO_FECHAINGRESO.HasValue)
            {
                DateTime fn = vm.EMPLEADO_FECHANACIMIENTO.Value.Date;
                DateTime fi = vm.EMPLEADO_FECHAINGRESO.Value.Date;

                int edad = fi.Year - fn.Year;
                if (fi < fn.AddYears(edad)) edad--;
                if (edad <= 12)
                    ModelState.AddModelError(nameof(vm.EMPLEADO_FECHANACIMIENTO),
                        "El empleado debe tener más de 12 años (≥ 13) a la fecha de ingreso.");
            }

            // ==========================================
            //  UNICIDAD: DPI/CUI y NIT 
            // ==========================================
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
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }
            // --------------------------------------------------------------

            // Normalización de strings -> null si vienen vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Conversión de fechas del VM (DateTime?) a ENTIDAD (DateOnly)
            DateOnly toDateOnly(DateTime dt) => DateOnly.FromDateTime(dt.Date);
            DateOnly? toDateOnlyOrNull(DateTime? dt) => dt.HasValue ? DateOnly.FromDateTime(dt.Value.Date) : (DateOnly?)null;

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
                    PERSONA_TELEFONOCASA = s2(vm.PERSONA_TELEFONOCASA),   // opcional
                    PERSONA_TELEFONOMOVIL = s2(vm.PERSONA_TELEFONOMOVIL),
                    PERSONA_CORREO = s2(vm.PERSONA_CORREO),

                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(persona);
                _context.PERSONA.Add(persona);
                await _context.SaveChangesAsync();

                // -----------------------------
                // 2) EMPLEADO (map + conversión fechas)
                // -----------------------------
                var empleado = new EMPLEADO
                {
                    EMPLEADO_ID = personaId, // = PERSONA_ID
                    EMPLEADO_FECHANACIMIENTO = toDateOnlyOrNull(vm.EMPLEADO_FECHANACIMIENTO),
                    EMPLEADO_FECHAINGRESO = toDateOnly(vm.EMPLEADO_FECHAINGRESO!.Value),
                    EMPLEADO_GENERO = s2(vm.EMPLEADO_GENERO),
                    PUESTO_ID = vm.PUESTO_ID!,
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(empleado);
                _context.EMPLEADO.Add(empleado);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                // PRG + SweetAlert
                TempData["SwalTitle"] = "¡Empleado guardado!";
                TempData["SwalText"] = $"El registro \"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}\" se creó correctamente.";
                TempData["SwalIndexUrl"] = Url.Action("Index", "Empleados");
                TempData["SwalCreateUrl"] = Url.Action("Create", "Empleados");
                return RedirectToAction(nameof(Create));
            }
            catch
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "Ocurrió un error al crear el empleado. Intenta nuevamente.");
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /Empleados/Edit/{id}
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var e = await _context.EMPLEADO
                .Include(x => x.EMPLEADONavigation)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EMPLEADO_ID == id && !x.ELIMINADO);

            if (e == null) return NotFound();

            // Helper de conversión ENTIDAD (DateOnly) -> VM (DateTime?)
            DateTime? toDateTimeOrNull(DateOnly? d) => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null;
            DateTime toDateTime(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

            var vm = new EmpleadoCreateVM
            {
                // IDs
                EMPLEADO_ID = e.EMPLEADO_ID,
                PERSONA_ID = e.EMPLEADO_ID,

                // PERSONA (map completo)
                PERSONA_PRIMERNOMBRE = e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE,
                PERSONA_SEGUNDONOMBRE = e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE,
                PERSONA_TERCERNOMBRE = e.EMPLEADONavigation.PERSONA_TERCERNOMBRE,
                PERSONA_PRIMERAPELLIDO = e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO,
                PERSONA_SEGUNDOAPELLIDO = e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO,
                PERSONA_APELLIDOCASADA = e.EMPLEADONavigation.PERSONA_APELLIDOCASADA,
                PERSONA_NIT = e.EMPLEADONavigation.PERSONA_NIT,
                PERSONA_CUI = e.EMPLEADONavigation.PERSONA_CUI,
                PERSONA_DIRECCION = e.EMPLEADONavigation.PERSONA_DIRECCION,
                PERSONA_TELEFONOCASA = e.EMPLEADONavigation.PERSONA_TELEFONOCASA,   // <-- Teléfono Casa
                PERSONA_TELEFONOMOVIL = e.EMPLEADONavigation.PERSONA_TELEFONOMOVIL,
                PERSONA_CORREO = e.EMPLEADONavigation.PERSONA_CORREO,

                // EMPLEADO (DateOnly -> DateTime? para inputs)
                EMPLEADO_FECHANACIMIENTO = toDateTimeOrNull(e.EMPLEADO_FECHANACIMIENTO),
                EMPLEADO_FECHAINGRESO = toDateTime(e.EMPLEADO_FECHAINGRESO),
                EMPLEADO_GENERO = e.EMPLEADO_GENERO,
                PUESTO_ID = e.PUESTO_ID,
                ESTADO = e.ESTADO,

                // Combos
                Puestos = await CargarPuestosAsync(),
                Generos = GetGenerosSelect()
            };

            return View(vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /Empleados/Edit/{id}
        // + Valida edad > 12 y unicidad de DPI/NIT (excluyendo el propio registro).
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EmpleadoCreateVM vm)
        {
            if (id != vm.EMPLEADO_ID) return NotFound();

            // --------- VALIDACIONES DE LOS 10 CAMPOS OBLIGATORIOS ---------
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

            if (string.IsNullOrWhiteSpace(vm.EMPLEADO_GENERO))
                ModelState.AddModelError(nameof(vm.EMPLEADO_GENERO), "El género es obligatorio.");

            if (!vm.EMPLEADO_FECHANACIMIENTO.HasValue)
                ModelState.AddModelError(nameof(vm.EMPLEADO_FECHANACIMIENTO), "La fecha de nacimiento es obligatoria.");

            if (!vm.EMPLEADO_FECHAINGRESO.HasValue)
                ModelState.AddModelError(nameof(vm.EMPLEADO_FECHAINGRESO), "La fecha de ingreso es obligatoria.");

            // Validar FK Puesto (activo y no eliminado)
            bool puestoOk = !string.IsNullOrWhiteSpace(vm.PUESTO_ID)
                            && await _context.PUESTO.AnyAsync(p => p.PUESTO_ID == vm.PUESTO_ID && !p.ELIMINADO && p.ESTADO);
            if (!puestoOk)
                ModelState.AddModelError(nameof(vm.PUESTO_ID), "El puesto seleccionado no existe o no está activo.");

            // ============================
            // VALIDACIÓN DE EDAD > 12
            // ============================
            if (vm.EMPLEADO_FECHANACIMIENTO.HasValue && vm.EMPLEADO_FECHAINGRESO.HasValue)
            {
                DateTime fn = vm.EMPLEADO_FECHANACIMIENTO.Value.Date;
                DateTime fi = vm.EMPLEADO_FECHAINGRESO.Value.Date;

                int edad = fi.Year - fn.Year;
                if (fi < fn.AddYears(edad)) edad--;
                if (edad <= 12)
                    ModelState.AddModelError(nameof(vm.EMPLEADO_FECHANACIMIENTO),
                        "El empleado debe tener más de 12 años (≥ 13) a la fecha de ingreso.");
            }

            // ==========================================
            // UNICIDAD: DPI/CUI y NIT (excluye propio)
            // ==========================================
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
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }
            // --------------------------------------------------------------

            var emp = await _context.EMPLEADO
                .Include(x => x.EMPLEADONavigation)
                .FirstOrDefaultAsync(x => x.EMPLEADO_ID == id && !x.ELIMINADO);
            if (emp == null) return NotFound();

            // Normalización de strings -> null si vienen vacíos
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

            // Conversión VM -> ENTIDAD
            DateOnly toDateOnly(DateTime dt) => DateOnly.FromDateTime(dt.Date);
            DateOnly? toDateOnlyOrNull(DateTime? dt) => dt.HasValue ? DateOnly.FromDateTime(dt.Value.Date) : (DateOnly?)null;

            // -----------------------------
            // PERSONA (aplicar cambios)
            // -----------------------------
            emp.EMPLEADONavigation.PERSONA_PRIMERNOMBRE = vm.PERSONA_PRIMERNOMBRE!.Trim();
            emp.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE = s2(vm.PERSONA_SEGUNDONOMBRE);
            emp.EMPLEADONavigation.PERSONA_TERCERNOMBRE = s2(vm.PERSONA_TERCERNOMBRE);
            emp.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO = vm.PERSONA_PRIMERAPELLIDO!.Trim();
            emp.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO = s2(vm.PERSONA_SEGUNDOAPELLIDO);
            emp.EMPLEADONavigation.PERSONA_APELLIDOCASADA = s2(vm.PERSONA_APELLIDOCASADA);
            emp.EMPLEADONavigation.PERSONA_NIT = s2(vm.PERSONA_NIT);
            emp.EMPLEADONavigation.PERSONA_CUI = s2(vm.PERSONA_CUI);
            emp.EMPLEADONavigation.PERSONA_DIRECCION = s2(vm.PERSONA_DIRECCION);
            emp.EMPLEADONavigation.PERSONA_TELEFONOCASA = s2(vm.PERSONA_TELEFONOCASA); // opcional
            emp.EMPLEADONavigation.PERSONA_TELEFONOMOVIL = s2(vm.PERSONA_TELEFONOMOVIL);
            emp.EMPLEADONavigation.PERSONA_CORREO = s2(vm.PERSONA_CORREO);
            emp.EMPLEADONavigation.ESTADO = vm.ESTADO;

            // -----------------------------
            // EMPLEADO (aplicar cambios)
            // -----------------------------
            emp.EMPLEADO_FECHANACIMIENTO = toDateOnlyOrNull(vm.EMPLEADO_FECHANACIMIENTO);
            emp.EMPLEADO_FECHAINGRESO = toDateOnly(vm.EMPLEADO_FECHAINGRESO!.Value);
            emp.EMPLEADO_GENERO = s2(vm.EMPLEADO_GENERO);
            emp.PUESTO_ID = vm.PUESTO_ID!;
            emp.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(emp.EMPLEADONavigation);
            _audit.StampUpdate(emp);

            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Empleado actualizado!";
            TempData["SwalText"] = $"\"{emp.EMPLEADONavigation.PERSONA_PRIMERNOMBRE} {emp.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = emp.EMPLEADO_ID });
        }


        // ============================================================
        // DELETE (GET/POST) – Borrado lógico del EMPLEADO
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var e = await _context.EMPLEADO
                .AsNoTracking()
                .Include(x => x.EMPLEADONavigation)
                .Include(x => x.PUESTO)
                .FirstOrDefaultAsync(x => x.EMPLEADO_ID == id);

            if (e == null) return NotFound();

            return View(e);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var e = await _context.EMPLEADO.FindAsync(id);
            if (e == null) return NotFound();

            _audit.StampSoftDelete(e);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===================== HELPERS ==============================

        // Genera IDs tipo PE00000001 para PERSONA (y EMPLEADO usa el mismo)
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

        // Carga de Puestos activos y no eliminados (para combo)
        private async Task<List<SelectListItem>> CargarPuestosAsync()
        {
            return await _context.PUESTO
                .Where(p => !p.ELIMINADO && p.ESTADO)
                .OrderBy(p => p.PUESTO_NOMBRE)
                .Select(p => new SelectListItem
                {
                    Text = p.PUESTO_NOMBRE,
                    Value = p.PUESTO_ID
                })
                .ToListAsync();
        }

        // Catálogo simple de Género (si tienes tabla, reemplaza por consulta)
        private List<SelectListItem> GetGenerosSelect() => new()
        {
            new SelectListItem { Text = "Seleccione ↓", Value = "" },
            new SelectListItem { Text = "Femenino", Value = "Femenino" },
            new SelectListItem { Text = "Masculino", Value = "Masculino" }
        };

        [HttpGet]
        public async Task<IActionResult> ReportePDF(
    string? Search,
    string? Puesto,
    string? Genero,
    DateTime? FechaIngresoIni,
    DateTime? FechaIngresoFin,
    bool? Estado,
    string Sort = "id",
    string Dir = "asc")
        {
            IQueryable<EMPLEADO> q = _context.EMPLEADO
                .AsNoTracking()
                .Where(e => !e.ELIMINADO);

            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(e =>
                    EF.Functions.Like(e.EMPLEADO_ID, $"%{s}%") ||
                    EF.Functions.Like(e.PUESTO.PUESTO_NOMBRE, $"%{s}%") ||
                    EF.Functions.Like(
                        (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? ""),
                        $"%{s}%"
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(Puesto))
            {
                if (Puesto == "__BLANKS__")
                    q = q.Where(e => e.PUESTO == null || string.IsNullOrEmpty(e.PUESTO.PUESTO_NOMBRE));
                else if (Puesto == "__NONBLANKS__")
                    q = q.Where(e => e.PUESTO != null && !string.IsNullOrEmpty(e.PUESTO.PUESTO_NOMBRE));
                else
                {
                    string s = Puesto.Trim();
                    q = q.Where(e => EF.Functions.Like(e.PUESTO.PUESTO_NOMBRE, $"%{s}%"));
                }
            }

            if (!string.IsNullOrWhiteSpace(Genero))
            {
                string g = Genero.Trim();
                q = q.Where(e => e.EMPLEADO_GENERO != null && e.EMPLEADO_GENERO == g);
            }

            if (FechaIngresoIni.HasValue)
            {
                var fIni = DateOnly.FromDateTime(FechaIngresoIni.Value.Date);
                q = q.Where(e => e.EMPLEADO_FECHAINGRESO >= fIni);
            }
            if (FechaIngresoFin.HasValue)
            {
                var fFin = DateOnly.FromDateTime(FechaIngresoFin.Value.Date);
                q = q.Where(e => e.EMPLEADO_FECHAINGRESO <= fFin);
            }

            if (Estado.HasValue)
                q = q.Where(e => e.ESTADO == Estado.Value);

            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(e => e.EMPLEADO_ID) : q.OrderByDescending(e => e.EMPLEADO_ID),
                "nombre" => asc
                    ? q.OrderBy(e =>
                        (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? ""))
                    : q.OrderByDescending(e =>
                        (e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (e.EMPLEADONavigation.PERSONA_APELLIDOCASADA ?? "")),
                "puesto" => asc ? q.OrderBy(e => e.PUESTO.PUESTO_NOMBRE) : q.OrderByDescending(e => e.PUESTO.PUESTO_NOMBRE),
                "ingreso" => asc ? q.OrderBy(e => e.EMPLEADO_FECHAINGRESO) : q.OrderByDescending(e => e.EMPLEADO_FECHAINGRESO),
                "estado" => asc ? q.OrderBy(e => e.ESTADO) : q.OrderByDescending(e => e.ESTADO),
                _ => asc ? q.OrderBy(e => e.FECHA_CREACION) : q.OrderByDescending(e => e.FECHA_CREACION),
            };

            var items = await q
                .Include(e => e.EMPLEADONavigation)
                .Include(e => e.PUESTO)
                .ToListAsync();

            int totActivos = items.Count(e => e.ESTADO);
            int totInactivos = items.Count(e => !e.ESTADO);

            var vm = new ReporteViewModel<EMPLEADO>
            {
                Items = items,
                Search = Search,
                FechaInicio = FechaIngresoIni,
                FechaFin = FechaIngresoFin,
                Estado = Estado,
                Sort = Sort,
                Dir = Dir,
                Page = 1,
                PageSize = items.Count,
                TotalItems = items.Count,
                TotalPages = 1,
                ReportTitle = "Reporte de Empleados",
                CompanyInfo = "CreArte Manualidades | Sololá, Guatemala | creartemanualidades2021@gmail.com",
                GeneratedBy = User?.Identity?.Name ?? "Usuario no autenticado",
                LogoUrl = Url.Content("~/Imagenes/logoCreArte.png")
            };

            vm.AddTotal("Activos", totActivos);
            vm.AddTotal("Inactivos", totInactivos);
            if (!string.IsNullOrWhiteSpace(Puesto)) vm.ExtraFilters["Puesto"] = Puesto;
            if (!string.IsNullOrWhiteSpace(Genero)) vm.ExtraFilters["Género"] = Genero;

            var pdf = new ViewAsPdf("ReporteEmpleados", vm)
            {
                FileName = $"ReporteEmpleados.pdf",
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
