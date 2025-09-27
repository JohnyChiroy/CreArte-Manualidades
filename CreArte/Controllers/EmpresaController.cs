// ===============================================
// RUTA: Controllers/EmpresaController.cs
// DESCRIPCIÓN:
//  - Página única de configuración de EMPRESA con 2 modos:
//    ▸ Index (solo lectura con botón Editar si existe)
//    ▸ Edit (GET/POST) para crear o actualizar (primera vez o cambios)
//  - Usa auditoría IAuditoriaService (igual que EmpleadosController).
//  - Mantiene estilos y UX (ua-*, pill estado, PRG SweetAlert).
// ===============================================
using CreArte.Data;
using CreArte.Models;
using CreArte.ModelsPartial;
using CreArte.Services.Auditoria; // IAuditoriaService
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Controllers
{
    [Authorize]
    [Route("Empresa")]
    public class EmpresaController : Controller
    {
        private readonly CreArteDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IAuditoriaService _audit;

        public EmpresaController(CreArteDbContext context, IWebHostEnvironment env, IAuditoriaService audit)
        {
            _context = context;
            _env = env;
            _audit = audit;
        }

        // ============================================================
        // INDEX (READ-ONLY) – RUTA: GET /Empresa
        //  - Si no hay registro, redirige a Edit (para registrar la 1a vez).
        //  - Si existe, muestra tarjeta de solo lectura + botón Editar.
        // ============================================================
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var e = await _context.EMPRESA
                .AsNoTracking()
                .Where(x => !x.ELIMINADO)
                .OrderByDescending(x => x.FECHA_CREACION)
                .FirstOrDefaultAsync();

            if (e == null)
            {
                // Primera vez: ir directo al formulario de registro
                return RedirectToAction(nameof(Edit));
            }

            var vm = new EmpresaConfigVM
            {
                EMPRESA_ID = e.EMPRESA_ID,
                NOMBRE_LEGAL_EMPRESA = e.NOMBRE_LEGAL_EMPRESA,
                NOMBRE_COMERCIAL_EMPRESA = e.NOMBRE_COMERCIAL_EMPRESA,
                NIT_EMPRESA = e.NIT_EMPRESA,
                DIRECCION_EMPRESA = e.DIRECCION_EMPRESA,
                TELEFONO_EMPRESA = e.TELEFONO_EMPRESA.ToString("0"),
                WHATSAPP_EMPRESA = e.WHATSAPP_EMPRESA.HasValue ? e.WHATSAPP_EMPRESA.Value.ToString("0") : null,
                CORREO_EMPRESA = e.CORREO_EMPRESA,
                DESCRIPCION_EMPRESA = e.DESCRIPCION_EMPRESA,
                LOGO_EMPRESA = e.LOGO_EMPRESA,
                USUARIO_CREACION = e.USUARIO_CREACION,
                FECHA_CREACION = e.FECHA_CREACION,
                USUARIO_MODIFICACION = e.USUARIO_MODIFICACION,
                FECHA_MODIFICACION = e.FECHA_MODIFICACION,
                ESTADO = e.ESTADO,
                ELIMINADO = e.ELIMINADO
            };

            return View("Index", vm); // Vista de solo lectura
        }

        // ============================================================
        // EDIT (GET) – RUTA: GET /Empresa/Edit
        //  - Si existe la empresa, carga para edición.
        //  - Si no existe, prepara VM por defecto para REGISTRO inicial.
        // ============================================================
        [HttpGet("Edit")]
        public async Task<IActionResult> Edit()
        {
            var e = await _context.EMPRESA
                .AsNoTracking()
                .Where(x => !x.ELIMINADO)
                .OrderByDescending(x => x.FECHA_CREACION)
                .FirstOrDefaultAsync();

            if (e == null)
            {
                // Registro inicial (primera vez)
                var vmNuevo = new EmpresaConfigVM
                {
                    EMPRESA_ID = "EMP001",
                    ESTADO = true
                };
                return View("Edit", vmNuevo);
            }

            var vm = new EmpresaConfigVM
            {
                EMPRESA_ID = e.EMPRESA_ID,
                NOMBRE_LEGAL_EMPRESA = e.NOMBRE_LEGAL_EMPRESA,
                NOMBRE_COMERCIAL_EMPRESA = e.NOMBRE_COMERCIAL_EMPRESA,
                NIT_EMPRESA = e.NIT_EMPRESA,
                DIRECCION_EMPRESA = e.DIRECCION_EMPRESA,
                TELEFONO_EMPRESA = e.TELEFONO_EMPRESA.ToString("0"),
                WHATSAPP_EMPRESA = e.WHATSAPP_EMPRESA.HasValue ? e.WHATSAPP_EMPRESA.Value.ToString("0") : null,
                CORREO_EMPRESA = e.CORREO_EMPRESA,
                DESCRIPCION_EMPRESA = e.DESCRIPCION_EMPRESA,
                LOGO_EMPRESA = e.LOGO_EMPRESA,
                USUARIO_CREACION = e.USUARIO_CREACION,
                FECHA_CREACION = e.FECHA_CREACION,
                USUARIO_MODIFICACION = e.USUARIO_MODIFICACION,
                FECHA_MODIFICACION = e.FECHA_MODIFICACION,
                ESTADO = e.ESTADO,
                ELIMINADO = e.ELIMINADO
            };

            return View("Edit", vm);
        }

        // ============================================================
        // EDIT (POST) – RUTA: POST /Empresa/Edit
        //  - Crea (si no existe) o actualiza (si existe).
        //  - Convierte string->decimal/decimal? (tel/whatsapp).
        //  - Maneja carga y guardado de logo.
        //  - PRG con SweetAlert y redirección a Index (read-only).
        // ============================================================
        [HttpPost("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmpresaConfigVM vm)
        {
            if (!ModelState.IsValid) return View("Edit", vm);

            // Normalizaciones estilo Empleados
            string? s2(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();
            vm.NOMBRE_COMERCIAL_EMPRESA = (vm.NOMBRE_COMERCIAL_EMPRESA ?? "").Trim();
            vm.NOMBRE_LEGAL_EMPRESA = (vm.NOMBRE_LEGAL_EMPRESA ?? "").Trim();
            vm.DIRECCION_EMPRESA = (vm.DIRECCION_EMPRESA ?? "").Trim();
            vm.NIT_EMPRESA = (vm.NIT_EMPRESA ?? "").Trim();
            vm.CORREO_EMPRESA = (vm.CORREO_EMPRESA ?? "").Trim();

            // Conversión TEL (obligatorio) → decimal no-nullable
            if (!decimal.TryParse(vm.TELEFONO_EMPRESA, out var telValue))
            {
                ModelState.AddModelError(nameof(vm.TELEFONO_EMPRESA), "El teléfono debe ser numérico (8 dígitos).");
                return View("Edit", vm);
            }

            // Conversión WhatsApp (opcional) → decimal?
            decimal? waValue = null;
            if (!string.IsNullOrWhiteSpace(vm.WHATSAPP_EMPRESA))
            {
                if (decimal.TryParse(vm.WHATSAPP_EMPRESA, out var tmp)) waValue = tmp;
                else
                {
                    ModelState.AddModelError(nameof(vm.WHATSAPP_EMPRESA), "El WhatsApp debe ser numérico (8 dígitos).");
                    return View("Edit", vm);
                }
            }

            // Manejo de logo
            string newLogoPath = null;
            if (vm.LogoFile != null && vm.LogoFile.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "logos");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var ext = Path.GetExtension(vm.LogoFile.FileName);
                var fileName = $"{vm.EMPRESA_ID}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
                var physicalPath = Path.Combine(folder, fileName);
                using (var stream = System.IO.File.Create(physicalPath))
                {
                    await vm.LogoFile.CopyToAsync(stream);
                }
                newLogoPath = $"/uploads/logos/{fileName}";
            }

            // Buscar existente por ID (único) y no eliminado
            var e = await _context.EMPRESA
                .FirstOrDefaultAsync(x => x.EMPRESA_ID == vm.EMPRESA_ID && !x.ELIMINADO);

            if (e == null)
            {
                // ===== Crear =====
                var nuevo = new EMPRESA
                {
                    EMPRESA_ID = vm.EMPRESA_ID,
                    NOMBRE_LEGAL_EMPRESA = s2(vm.NOMBRE_LEGAL_EMPRESA),
                    NOMBRE_COMERCIAL_EMPRESA = vm.NOMBRE_COMERCIAL_EMPRESA,
                    NIT_EMPRESA = s2(vm.NIT_EMPRESA),
                    DIRECCION_EMPRESA = vm.DIRECCION_EMPRESA,
                    TELEFONO_EMPRESA = telValue,      // NOT NULL
                    WHATSAPP_EMPRESA = waValue,       // NULL
                    CORREO_EMPRESA = s2(vm.CORREO_EMPRESA),
                    DESCRIPCION_EMPRESA = s2(vm.DESCRIPCION_EMPRESA),
                    LOGO_EMPRESA = newLogoPath ?? vm.LOGO_EMPRESA,
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false
                };

                _audit.StampCreate(nuevo); // Igual que Empleados
                _context.EMPRESA.Add(nuevo);
                await _context.SaveChangesAsync();

                TempData["SwalTitle"] = "¡Empresa registrada!";
                TempData["SwalText"] = "Se guardó la información de la empresa.";
            }
            else
            {
                // ===== Actualizar =====
                e.NOMBRE_LEGAL_EMPRESA = s2(vm.NOMBRE_LEGAL_EMPRESA);
                e.NOMBRE_COMERCIAL_EMPRESA = vm.NOMBRE_COMERCIAL_EMPRESA;
                e.NIT_EMPRESA = s2(vm.NIT_EMPRESA);
                e.DIRECCION_EMPRESA = vm.DIRECCION_EMPRESA;
                e.TELEFONO_EMPRESA = telValue;
                e.WHATSAPP_EMPRESA = waValue;
                e.CORREO_EMPRESA = s2(vm.CORREO_EMPRESA);
                e.DESCRIPCION_EMPRESA = s2(vm.DESCRIPCION_EMPRESA);
                if (!string.IsNullOrEmpty(newLogoPath)) e.LOGO_EMPRESA = newLogoPath;
                e.ESTADO = vm.ESTADO;

                _audit.StampUpdate(e);
                await _context.SaveChangesAsync();

                TempData["SwalTitle"] = "¡Empresa actualizada!";
                TempData["SwalText"] = "Los cambios se guardaron correctamente.";
            }

            // PRG → volver a solo lectura
            return RedirectToAction(nameof(Index));
        }
    }
}