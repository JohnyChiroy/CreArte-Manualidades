using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    // VM para pedir usuario o correo y enviar el email
    public class RecuperacionContrasenaVM
    {
        [Display(Name = "Usuario o correo")]
        [Required(ErrorMessage = "Ingrese su usuario o correo.")]
        public string? UsuarioONCorreo { get; set; }
    }

    // VM para aplicar nueva contraseña (usando token)
    //public class RestablecerContrasenaVM
    //{
    //    [Required]
    //    public required string UsuarioNombre { get; set; }

    //    [Required]
    //    public required string Token { get; set; }

    //    [Display(Name = "Nueva contraseña")]
    //    [Required(ErrorMessage = "Ingrese la nueva contraseña.")]
    //    [StringLength(64, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    //    [DataType(DataType.Password)]
    //    public string NuevaContrasena { get; set; }

    //    [Display(Name = "Confirmar contraseña")]
    //    [Required(ErrorMessage = "Confirme la nueva contraseña.")]
    //    [DataType(DataType.Password)]
    //    [Compare("NuevaContrasena", ErrorMessage = "Las contraseñas no coinciden.")]
    //    public string ConfirmarContrasena { get; set; }
    //}


    
        public class RestablecerContrasenaVM
        {
            [Required, DataType(DataType.Password)]
            [Display(Name = "Nueva contraseña")]
            [MinLength(8, ErrorMessage = "Mínimo 8 caracteres.")]
            public string? NuevaContrasena { get; set; }

            [Required, DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
            public string? ConfirmarContrasena { get; set; }
        }
    



    public class CambioContrasenaViewModel
    {
        [Required, DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        [MinLength(8, ErrorMessage = "Mínimo 8 caracteres.")]
        public string? NuevaContrasena { get; set; }

        [Required, DataType(DataType.Password)]
        [Display(Name = "Confirmar contraseña")]
        [Compare(nameof(NuevaContrasena), ErrorMessage = "Las contraseñas no coinciden.")]
        public string? ConfirmarContrasena { get; set; }
    }









}
