// ===============================================
// RUTA: Controllers/EmpleadosController.cs
// DESCRIPCIÓN: CRUD de EMPLEADO con captura integrada
//              de datos de PERSONA (EMP = PERSONA_ID),
//              filtros/orden/paginación y auditoría.
//              ▸ IMPORTANTE: En entidad EF (EMPLEADO) las fechas son DateOnly/DateOnly?,
//                y en los ViewModels (formularios) son DateTime?.
//                Por eso convertimos explícitamente en Create/Edit/Index.
// ===============================================
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
        // LISTADO: GET /Empleados?Search=...&Puesto=...&Genero=...
        //          &FechaIngresoIni=...&FechaIngresoFin=...&Estado=...
        //          &Sort=...&Dir=...&Page=1&PageSize=10
        // ============================================================
        public async Task<IActionResult> Index(
            string? Search,
            string? Puesto,
            string? Genero,
            DateTime? FechaIngresoIni,  // <- del request (DateTime?)
            DateTime? FechaIngresoFin,  // <- del request (DateTime?)
            bool? Estado,
            string Sort = "id",
            string Dir = "asc",
            int Page = 1,
            int PageSize = 10)
        {
            // 1) Base: IQueryable<EMPLEADO> sin Include para permitir composición
            IQueryable<EMPLEADO> q = _context.EMPLEADO
                .Where(e => !e.ELIMINADO);

            // 2) Búsqueda global (por ID empleado, Persona (nombres/apellidos), Puesto)
            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                q = q.Where(e =>
                    EF.Functions.Like(e.EMPLEADO_ID, $"%{s}%") ||
                    EF.Functions.Like(e.PUESTO.PUESTO_NOMBRE, $"%{s}%") ||
                    // Ajusta a los nombres reales de tus columnas PERSONA
                    EF.Functions.Like(e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE, $"%{s}%") ||
                    EF.Functions.Like(e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO, $"%{s}%"));
            }

            // 3) Filtro por Puesto (texto/vacíos/marcadores)
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

            // 4) Filtro por Género (valor directo)
            if (!string.IsNullOrWhiteSpace(Genero))
            {
                string g = Genero.Trim();
                q = q.Where(e => e.EMPLEADO_GENERO != null && e.EMPLEADO_GENERO == g);
            }

            // ================== 5) FILTROS POR FECHA DE INGRESO ==================
            // En entidad EF: EMPLEADO_FECHAINGRESO es DateOnly
            // En parámetros del request: DateTime?
            // Convertimos DateTime? -> DateOnly antes de comparar
            if (FechaIngresoIni.HasValue)
            {
                var fIni = DateOnly.FromDateTime(FechaIngresoIni.Value.Date);
                q = q.Where(e => e.EMPLEADO_FECHAINGRESO >= fIni);
            }

            if (FechaIngresoFin.HasValue)
            {
                var fFin = DateOnly.FromDateTime(FechaIngresoFin.Value.Date);
                // Rango inclusivo [ini..fin]
                q = q.Where(e => e.EMPLEADO_FECHAINGRESO <= fFin);

                // Si prefieres semicluso (ini <= x < fin+1), usa esto en su lugar:
                // var fFinExcl = fFin.AddDays(1);
                // q = q.Where(e => e.EMPLEADO_FECHAINGRESO < fFinExcl);
            }

            // 6) Filtro por Estado
            if (Estado.HasValue)
                q = q.Where(e => e.ESTADO == Estado.Value);

            // 7) Ordenamiento (sobre IQueryable sin Include)
            bool asc = string.Equals(Dir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (Sort?.ToLower()) switch
            {
                "id" => asc ? q.OrderBy(e => e.EMPLEADO_ID) : q.OrderByDescending(e => e.EMPLEADO_ID),
                "nombre" => asc ? q.OrderBy(e => e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE).ThenBy(e => e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO)
                                 : q.OrderByDescending(e => e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE).ThenByDescending(e => e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO),
                "puesto" => asc ? q.OrderBy(e => e.PUESTO.PUESTO_NOMBRE) : q.OrderByDescending(e => e.PUESTO.PUESTO_NOMBRE),
                "ingreso" => asc ? q.OrderBy(e => e.EMPLEADO_FECHAINGRESO) : q.OrderByDescending(e => e.EMPLEADO_FECHAINGRESO),
                "estado" => asc ? q.OrderBy(e => e.ESTADO) : q.OrderByDescending(e => e.ESTADO),
                _ => asc ? q.OrderBy(e => e.FECHA_CREACION) : q.OrderByDescending(e => e.FECHA_CREACION),
            };

            // 8) Total para paginación
            int total = await q.CountAsync();
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);
            if (Page < 1) Page = 1;
            if (Page > totalPages && totalPages > 0) Page = totalPages;

            // 9) Include de navegaciones que se mostrarán en la vista
            var qWithNavs = q
                .Include(e => e.EMPLEADONavigation)
                .Include(e => e.PUESTO);

            var items = await qWithNavs
                .Skip((Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // 10) ViewModel de salida (mantén DateTime? en VM para inputs <input type="date">)
            var vm = new EmpleadoViewModels
            {
                Items = items,
                Search = Search,
                Puesto = Puesto,
                Genero = Genero,
                FechaIngresoIni = FechaIngresoIni, // VM usa DateTime?
                FechaIngresoFin = FechaIngresoFin, // VM usa DateTime?
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
        // DETAILS para modal (PartialView)
        // GET: /Empleados/DetailsCard?id=PE00000001
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
                    // PERSONA (ajusta a tu esquema real)
                    PERSONA_ID = e.EMPLEADONavigation.PERSONA_ID,
                    Nombres = e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE,
                    Apellidos = e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO,
                    DPI = e.EMPLEADONavigation.PERSONA_CUI,
                    Telefono = e.EMPLEADONavigation.PERSONA_TELEFONOMOVIL,
                    Correo = e.EMPLEADONavigation.PERSONA_CORREO,
                    Direccion = e.EMPLEADONavigation.PERSONA_DIRECCION,

                    // EMPLEADO
                    // OJO: Si tu EmpleadoDetailsVM define DateTime, convierte aquí con ToDateTime(...)
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

        // =====================================
        // CREATE (GET)
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var personaId = await SiguientePersonaIdAsync(); // PE + 8 dígitos
            var vm = new EmpleadoCreateVM
            {
                // IDs (EMPLEADO_ID = PERSONA_ID)
                EMPLEADO_ID = personaId,
                PERSONA_ID = personaId,

                // Defaults (VM usa DateTime? para inputs HTML)
                //EMPLEADO_FECHAINGRESO = DateTime.Today,
                //ESTADO = true,

                // Combos
                Puestos = await CargarPuestosAsync(),
                Generos = GetGenerosSelect()
            };

            return View(vm);
        }

        // =====================================
        // CREATE (POST) – Crea PERSONA + EMPLEADO
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmpleadoCreateVM vm)
        {
            // Re-asignamos IDs desde servidor por seguridad
            var personaId = await SiguientePersonaIdAsync();
            vm.PERSONA_ID = personaId;
            vm.EMPLEADO_ID = personaId; // EMPLEADO_ID == PERSONA_ID

            if (!ModelState.IsValid)
            {
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }

            // Validar FK Puesto
            bool puestoOk = await _context.PUESTO.AnyAsync(p => p.PUESTO_ID == vm.PUESTO_ID && !p.ELIMINADO && p.ESTADO);
            if (!puestoOk)
                ModelState.AddModelError(nameof(vm.PUESTO_ID), "El puesto seleccionado no existe o no está activo.");

            if (!ModelState.IsValid)
            {
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }

            // Normalizar PERSONA (Ajusta propiedades a tu modelo real)
            string nombres = (vm.Nombres ?? "").Trim();
            string apellidos = (vm.Apellidos ?? "").Trim();
            string? dpi = string.IsNullOrWhiteSpace(vm.DPI) ? null : vm.DPI.Trim();
            string? tel = string.IsNullOrWhiteSpace(vm.Telefono) ? null : vm.Telefono.Trim();
            string? corr = string.IsNullOrWhiteSpace(vm.Correo) ? null : vm.Correo.Trim();
            string? dir = string.IsNullOrWhiteSpace(vm.Direccion) ? null : vm.Direccion.Trim();

            // Validaciones ejemplo (puedes ampliar)
            if (string.IsNullOrWhiteSpace(nombres))
                ModelState.AddModelError(nameof(vm.Nombres), "Los nombres son obligatorios.");
            if (string.IsNullOrWhiteSpace(apellidos))
                ModelState.AddModelError(nameof(vm.Apellidos), "Los apellidos son obligatorios.");

            if (!ModelState.IsValid)
            {
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }

            // Transacción: insertar PERSONA y luego EMPLEADO
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Map VM -> PERSONA (ajusta a tu entidad real)
                var persona = new PERSONA
                {
                    PERSONA_ID = personaId,
                    PERSONA_PRIMERNOMBRE = nombres,
                    PERSONA_PRIMERAPELLIDO = apellidos,
                    PERSONA_CUI = dpi,
                    PERSONA_TELEFONOMOVIL = tel,
                    PERSONA_CORREO = corr,
                    PERSONA_DIRECCION = dir,
                    ESTADO = true,
                    ELIMINADO = false
                };
                _audit.StampCreate(persona);
                _context.PERSONA.Add(persona);
                await _context.SaveChangesAsync();

                // Map VM -> EMPLEADO  (CONVERSIÓN DateTime? -> DateOnly/DateOnly?)
                var empleado = new EMPLEADO
                {
                    EMPLEADO_ID = personaId, // igual que PERSONA

                    // DateTime? -> DateOnly?
                    //EMPLEADO_FECHANACIMIENTO = vm.EMPLEADO_FECHANACIMIENTO.HasValue
                    //    ? DateOnly.FromDateTime(vm.EMPLEADO_FECHANACIMIENTO.Value)
                    //    : (DateOnly?)null,
                    EMPLEADO_FECHANACIMIENTO = vm.EMPLEADO_FECHANACIMIENTO!.Value,
                    // DateTime? -> DateOnly (requerido)
                    //EMPLEADO_FECHAINGRESO= DateOnly.FromDateTime(vm.EMPLEADO_FECHAINGRESO!.Value),
                    EMPLEADO_FECHAINGRESO = vm.EMPLEADO_FECHAINGRESO!.Value,
                    EMPLEADO_GENERO = vm.EMPLEADO_GENERO,
                    PUESTO_ID = vm.PUESTO_ID,
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };
                _audit.StampCreate(empleado);
                _context.EMPLEADO.Add(empleado);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                // PRG + SweetAlert (re-uso de tu patrón)
                TempData["SwalTitle"] = "¡Empleado guardado!";
                TempData["SwalText"] = $"El registro \"{nombres} {apellidos}\" se creó correctamente.";
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

        // =====================================
        // EDIT (GET) – Edita PERSONA + EMPLEADO
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var e = await _context.EMPLEADO
                .Include(x => x.EMPLEADONavigation)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EMPLEADO_ID == id && !x.ELIMINADO);

            if (e == null) return NotFound();

            var vm = new EmpleadoCreateVM
            {
                // IDs
                EMPLEADO_ID = e.EMPLEADO_ID,
                PERSONA_ID = e.EMPLEADO_ID,

                // PERSONA (ajusta según tus columnas)
                Nombres = e.EMPLEADONavigation.PERSONA_PRIMERNOMBRE,
                Apellidos = e.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO,
                DPI = e.EMPLEADONavigation.PERSONA_CUI,
                Telefono = e.EMPLEADONavigation.PERSONA_TELEFONOMOVIL,
                Correo = e.EMPLEADONavigation.PERSONA_CORREO,
                Direccion = e.EMPLEADONavigation.PERSONA_DIRECCION,

                // EMPLEADO  (CONVERSIÓN DateOnly -> DateTime? para inputs)
                //EMPLEADO_FECHANACIMIENTO = e.EMPLEADO_FECHANACIMIENTO.HasValue
                //    ? e.EMPLEADO_FECHANACIMIENTO.Value.ToDateTime(TimeOnly.MinValue)
                //    : (DateTime?)null,
                EMPLEADO_FECHANACIMIENTO = e.EMPLEADO_FECHANACIMIENTO,

                //EMPLEADO_FECHAINGRESO = e.EMPLEADO_FECHAINGRESO.ToDateTime(TimeOnly.MinValue),
                EMPLEADO_FECHAINGRESO = e.EMPLEADO_FECHAINGRESO,
                EMPLEADO_GENERO = e.EMPLEADO_GENERO,
                PUESTO_ID = e.PUESTO_ID,
                ESTADO = e.ESTADO,

                // Combos
                Puestos = await CargarPuestosAsync(),
                Generos = GetGenerosSelect()
            };

            return View(vm);
        }

        // =====================================
        // EDIT (POST) – Actualiza PERSONA + EMPLEADO
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EmpleadoCreateVM vm)
        {
            if (id != vm.EMPLEADO_ID) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }

            var emp = await _context.EMPLEADO
                .Include(x => x.EMPLEADONavigation)
                .FirstOrDefaultAsync(x => x.EMPLEADO_ID == id && !x.ELIMINADO);
            if (emp == null) return NotFound();

            // Validar FK Puesto
            bool puestoOk = await _context.PUESTO.AnyAsync(p => p.PUESTO_ID == vm.PUESTO_ID && !p.ELIMINADO && p.ESTADO);
            if (!puestoOk)
                ModelState.AddModelError(nameof(vm.PUESTO_ID), "El puesto seleccionado no existe o no está activo.");

            // Normalizar PERSONA
            string nombres = (vm.Nombres ?? "").Trim();
            string apellidos = (vm.Apellidos ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nombres))
                ModelState.AddModelError(nameof(vm.Nombres), "Los nombres son obligatorios.");
            if (string.IsNullOrWhiteSpace(apellidos))
                ModelState.AddModelError(nameof(vm.Apellidos), "Los apellidos son obligatorios.");

            if (!ModelState.IsValid)
            {
                vm.Puestos = await CargarPuestosAsync();
                vm.Generos = GetGenerosSelect();
                return View(vm);
            }

            // Detectar cambios (conversión VM DateTime? -> DateOnly/DateOnly? para comparar)
            bool hayCambios =
                // PERSONA
                !string.Equals(emp.EMPLEADONavigation.PERSONA_PRIMERNOMBRE, nombres) ||
                !string.Equals(emp.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO, apellidos) ||
                !string.Equals(emp.EMPLEADONavigation.PERSONA_CUI, vm.DPI) ||
                !string.Equals(emp.EMPLEADONavigation.PERSONA_TELEFONOMOVIL, vm.Telefono) ||
                !string.Equals(emp.EMPLEADONavigation.PERSONA_CORREO, vm.Correo) ||
                !string.Equals(emp.EMPLEADONavigation.PERSONA_DIRECCION, vm.Direccion) ||
                // EMPLEADO
                emp.EMPLEADO_FECHANACIMIENTO != (
                    //vm.EMPLEADO_FECHANACIMIENTO.HasValue
                    //    ? DateOnly.FromDateTime(vm.EMPLEADO_FECHANACIMIENTO.Value)
                    //    : (DateOnly?)null
                    vm.EMPLEADO_FECHANACIMIENTO!.Value
                ) ||
                //emp.EMPLEADO_FECHAINGRESO != DateOnly.FromDateTime(vm.EMPLEADO_FECHAINGRESO!.Value) ||
                emp.EMPLEADO_FECHAINGRESO != vm.EMPLEADO_FECHAINGRESO!.Value ||
                emp.PUESTO_ID != vm.PUESTO_ID ||
                emp.EMPLEADO_GENERO != vm.EMPLEADO_GENERO ||
                emp.ESTADO != vm.ESTADO;

            if (!hayCambios)
            {
                TempData["SwalOneBtnFlag"] = "nochange";
                TempData["SwalTitle"] = "Sin cambios";
                TempData["SwalText"] = "No se modificó ningún dato.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Aplicar cambios (asignaciones con conversión a DateOnly/DateOnly?)
            emp.EMPLEADONavigation.PERSONA_PRIMERNOMBRE = nombres;
            emp.EMPLEADONavigation.PERSONA_PRIMERAPELLIDO = apellidos;
            emp.EMPLEADONavigation.PERSONA_CUI = string.IsNullOrWhiteSpace(vm.DPI) ? null : vm.DPI!.Trim();
            emp.EMPLEADONavigation.PERSONA_TELEFONOMOVIL = string.IsNullOrWhiteSpace(vm.Telefono) ? null : vm.Telefono!.Trim();
            emp.EMPLEADONavigation.PERSONA_CORREO = string.IsNullOrWhiteSpace(vm.Correo) ? null : vm.Correo!.Trim();
            emp.EMPLEADONavigation.PERSONA_DIRECCION = string.IsNullOrWhiteSpace(vm.Direccion) ? null : vm.Direccion!.Trim();

            //emp.EMPLEADO_FECHANACIMIENTO = vm.EMPLEADO_FECHANACIMIENTO.HasValue
            //    ? DateOnly.FromDateTime(vm.EMPLEADO_FECHANACIMIENTO.Value)
            //    : (DateOnly?)null;
            emp.EMPLEADO_FECHANACIMIENTO = vm.EMPLEADO_FECHANACIMIENTO!.Value;

            //emp.EMPLEADO_FECHAINGRESO = DateOnly.FromDateTime(vm.EMPLEADO_FECHAINGRESO!.Value);
            emp.EMPLEADO_FECHAINGRESO = vm.EMPLEADO_FECHAINGRESO!.Value;
            emp.EMPLEADO_GENERO = vm.EMPLEADO_GENERO;
            emp.PUESTO_ID = vm.PUESTO_ID!;
            emp.ESTADO = vm.ESTADO;

            // Auditoría
            _audit.StampUpdate(emp.EMPLEADONavigation);
            _audit.StampUpdate(emp);
            await _context.SaveChangesAsync();

            TempData["SwalOneBtnFlag"] = "updated";
            TempData["SwalTitle"] = "¡Empleado actualizado!";
            TempData["SwalText"] = $"\"{nombres} {apellidos}\" se actualizó correctamente.";
            return RedirectToAction(nameof(Edit), new { id = emp.EMPLEADO_ID });
        }

        // ============================================================
        // DELETE (GET/POST) – Borrado lógico del EMPLEADO
        // (No elimina PERSONA para preservar integridad y reutilización)
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
    }
}
