using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using CreArte.Services.Security;

namespace CreArte.Extensions
{
    public static class SecurityHtmlExtensions
    {
        // ===== Mapeo Controller -> MODULO_ID (idéntico a su INSERT) =====
        // Acepta singular/plural según su BD.
        private static readonly Dictionary<string, string> CtrlToModulo = new(StringComparer.OrdinalIgnoreCase)
        {
            ["compras"] = "COMPRAS",
            ["inventario"] = "INVENTARIO",
            ["kardex"] = "KARDEX",
            ["caja"] = "CAJA",
            ["ventas"] = "VENTAS",
            ["pedidos"] = "PEDIDOS",
            ["reportes"] = "REPORTES",

            ["productos"] = "PRODUCTOS",
            ["proveedores"] = "PROVEEDOR",   // en BD es singular
            ["clientes"] = "CLIENTES",
            ["empleados"] = "EMPLEADOS",

            ["usuarios"] = "USUARIOS",
            ["roles"] = "ROLES",
            ["permisos"] = "PERMISOS",

            ["areas"] = "AREAS",
            ["puestos"] = "PUESTOS",
            ["niveles"] = "NIVELES",
            ["categorias"] = "CATEGORIA",   // singular en BD
            ["subcategorias"] = "SUBCATEGOR",  // tal cual en su INSERT
            ["marcas"] = "MARCAS",
            ["unidadesmedida"] = "UNIDMEDIDA",
            ["tiposempaque"] = "TIPEMPAQUE",
            ["tiposproducto"] = "TIPOSPROD",
            ["tiposcliente"] = "TIPCLIENTE",
        };

        private static string? MapControllerToModulo(string? controller)
        {
            if (string.IsNullOrWhiteSpace(controller)) return null;
            return CtrlToModulo.TryGetValue(controller.Trim().ToLowerInvariant(), out var mod) ? mod : null;
        }

        private static bool Check(IHtmlHelper html, string moduloOrController, string op)
        {
            var http = html.ViewContext.HttpContext;
            var user = http.User ?? new ClaimsPrincipal();
            var svc = http.RequestServices.GetRequiredService<ICreArtePermissionService>();

            // Permite pasar "EMPLEADOS" (modulo) o "Empleados" (controller)
            string moduloId =
                CtrlToModulo.ContainsKey(moduloOrController.Trim().ToLowerInvariant())
                    ? MapControllerToModulo(moduloOrController)!            // venía como nombre de controlador
                    : moduloOrController.Trim().ToUpperInvariant();        // venía como MODULO_ID

            var key = (op ?? "VER").Trim().ToUpperInvariant();

            return key switch
            {
                "CREAR" or "CREATE" => svc.CanCreate(user, moduloId),
                "EDITAR" or "EDIT" => svc.CanEdit(user, moduloId),
                "ELIMINAR" or "DELETE" => svc.CanDelete(user, moduloId),
                _ => svc.CanView(user, moduloId), // VER/VIEW por defecto
            };
        }

        // ===== Helper 1: explícito (usted indica módulo o controller) =====
        // Uso: @Html.ShowIf("EMPLEADOS","CREAR", @<a>...</a>)
        public static IHtmlContent ShowIf(this IHtmlHelper html,
                                          string moduloOrController,
                                          string op,
                                          Func<object, HelperResult> body)
        {
            return Check(html, moduloOrController, op) ? body(null!) : HtmlString.Empty;
        }

        // Versión con nombre “ShowIfAsync” para compatibilidad con su sintaxis anterior
        public static System.Threading.Tasks.Task<IHtmlContent> ShowIfAsync(this IHtmlHelper html,
                                          string moduloOrController,
                                          string op,
                                          Func<object, HelperResult> body,
                                          string? actionNameIgnored = null)
        {
            IHtmlContent result = ShowIf(html, moduloOrController, op, body);
            return System.Threading.Tasks.Task.FromResult(result);
        }

        // ===== Helper 2: usa el controlador actual (menos escritura) =====
        // Uso: @Html.ShowIfCurrent("CREAR", @<a asp-action="Create">NUEVO</a>)
        public static IHtmlContent ShowIfCurrent(this IHtmlHelper html,
                                                 string op,
                                                 Func<object, HelperResult> body)
        {
            var ctrl = html.ViewContext.RouteData.Values["controller"]?.ToString();
            var modulo = MapControllerToModulo(ctrl ?? "");
            if (string.IsNullOrWhiteSpace(modulo)) return HtmlString.Empty;
            return Check(html, modulo, op) ? body(null!) : HtmlString.Empty;
        }
    }
}
