// Ruta: /Services/Auditoria/AuditoriaService.cs
using System;
using System.Reflection;

namespace CreArte.Services.Auditoria
{
    /// <summary>
    /// Implementación basada en convención de nombres de propiedades.
    /// No requiere interfaces en las entidades y sirve para tablas existentes.
    /// </summary>
    public sealed class AuditoriaService : IAuditoriaService
    {
        private readonly ICurrentUserService _currentUser;

        public AuditoriaService(ICurrentUserService currentUser)
        {
            _currentUser = currentUser;
        }

        public void StampCreate(object entity)
        {
            if (entity is null) return;

            var user = _currentUser.GetUserNameOrSystem();
            var now = DateTime.Now;

            SetProperty(entity, "USUARIO_CREACION", user);
            SetProperty(entity, "FECHA_CREACION", now);

            // En creación dejamos limpio modificación/eliminación
            SetProperty(entity, "USUARIO_MODIFICACION", null);
            SetProperty(entity, "FECHA_MODIFICACION", null);
            SetProperty(entity, "USUARIO_ELIMINACION", null);
            SetProperty(entity, "FECHA_ELIMINACION", null);

            // Si tiene ELIMINADO, forzamos a false
            SetProperty(entity, "ELIMINADO", false);
        }

        public void StampUpdate(object entity)
        {
            if (entity is null) return;

            var user = _currentUser.GetUserNameOrSystem();
            var now = DateTime.Now;

            SetProperty(entity, "USUARIO_MODIFICACION", user);
            SetProperty(entity, "FECHA_MODIFICACION", now);
        }

        public void StampSoftDelete(object entity)
        {
            if (entity is null) return;

            var user = _currentUser.GetUserNameOrSystem();
            var now = DateTime.Now;

            SetProperty(entity, "ELIMINADO", true);
            SetProperty(entity, "USUARIO_ELIMINACION", user);
            SetProperty(entity, "FECHA_ELIMINACION", now);
        }

        public void ClearDeletion(object entity)
        {
            if (entity is null) return;

            SetProperty(entity, "ELIMINADO", false);
            SetProperty(entity, "USUARIO_ELIMINACION", null);
            SetProperty(entity, "FECHA_ELIMINACION", null);
        }

        // --------------------------
        // Helpers por reflexión
        // --------------------------
        private static void SetProperty(object target, string propName, object value)
        {
            var prop = target.GetType().GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || !prop.CanWrite) return;

            // Convertimos tipo si hace falta (DateTime?, bool?)
            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            try
            {
                object safeValue = value == null ? null : Convert.ChangeType(value, targetType);
                prop.SetValue(target, safeValue);
            }
            catch
            {
                // Si no se puede convertir, no reventar: simplemente omite
            }
        }
    }
}
