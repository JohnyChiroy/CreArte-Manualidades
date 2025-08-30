using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // ViewModel para el formulario de cambio de contraseña forzado
    public class CambiarContrasenaViewModel
    {
        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Debe tener al menos 8 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public required string NuevaContrasena { get; set; }

        [Required(ErrorMessage = "Debes confirmar la nueva contraseña.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar nueva contraseña")]
        public required string ConfirmarContrasena { get; set; }
    }
}
