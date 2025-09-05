// Ruta: /Services/Auditoria/ICurrentUserService.cs
using System;

namespace CreArte.Services.Auditoria
{
    /// <summary>
    /// Servicio para obtener el usuario autenticado actual.
    /// Desacoplado de HttpContext para fácil testeo.
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// Devuelve el nombre de usuario amigable para auditoría.
        /// </summary>
        string GetUserNameOrSystem();
    }
}
