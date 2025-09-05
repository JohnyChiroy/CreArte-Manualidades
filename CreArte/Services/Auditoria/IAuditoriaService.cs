// Ruta: /Services/Auditoria/IAuditoriaService.cs
using System;

namespace CreArte.Services.Auditoria
{
    /// <summary>
    /// Servicio para sellar campos de auditoría en entidades (por convención de nombres).
    /// </summary>
    public interface IAuditoriaService
    {
        /// <summary>
        /// Sella USUARIO_CREACION y FECHA_CREACION.
        /// Si la entidad tiene ELIMINADO, lo fuerza a false.
        /// </summary>
        void StampCreate(object entity);

        /// <summary>
        /// Sella USUARIO_MODIFICACION y FECHA_MODIFICACION.
        /// </summary>
        void StampUpdate(object entity);

        /// <summary>
        /// Sella borrado lógico: ELIMINADO = true, USUARIO_ELIMINACION y FECHA_ELIMINACION.
        /// </summary>
        void StampSoftDelete(object entity);

        /// <summary>
        /// (Opcional) Limpia los campos de eliminación para “reactivar” un registro.
        /// </summary>
        void ClearDeletion(object entity);
    }
}
