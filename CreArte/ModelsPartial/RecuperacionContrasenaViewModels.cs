using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // VM para pedir usuario o correo y enviar el email
    public class RecuperacionContrasenaVM
    {
        [Display(Name = "Usuario o correo")]
        [Required(ErrorMessage = "Ingrese su usuario o su correo registrado.")]
        [StringLength(150)]
        public string UsuarioONCorreo { get; set; }
    }

    // VM para aplicar nueva contraseña (usando token)
    public class RestablecerContrasenaVM
    {
        [Required]
        public required string UsuarioNombre { get; set; }

        [Required]
        public required string Token { get; set; }

        [Display(Name = "Nueva contraseña")]
        [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
        [StringLength(64, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        public string NuevaContrasena { get; set; }

        [Display(Name = "Confirmar contraseña")]
        [Required(ErrorMessage = "Confirme la nueva contraseña.")]
        [DataType(DataType.Password)]
        [Compare("NuevaContrasena", ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarContrasena { get; set; }
    }
}
