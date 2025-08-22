using System.ComponentModel.DataAnnotations;

namespace CreArte.ModelsPartial
{
    public class LoginViewModels
    {
        [Required(ErrorMessage = "El campo Usuario es obligatorio.")]
        [Display(Name = "Usuario")]
        public string USUARIO_NOMBRE { get; set; }

        [Required(ErrorMessage = "El campo Contraseña es obligatorio.")]
        [Display(Name = "Contraseña")]
        public string USUARIO_CONTRASENA { get; set; }

        [Display(Name = "Recordarme")]
        public bool Recordarme { get; set; } = false;
    }
}
