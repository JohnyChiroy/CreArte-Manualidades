using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CreArte.ModelsPartial
{
    // VM para configuración de EMPRESA (create/update)
    // - Strings para validar teléfono/whatsapp como 8 dígitos.
    // - IFormFile para el logo (archivo, no BD).
    public class EmpresaConfigVM
    {
        [Required, StringLength(10)]
        [Display(Name = "ID Empresa")]
        public string EMPRESA_ID { get; set; }

        [StringLength(100)]
        [Display(Name = "Nombre Legal")]
        public string NOMBRE_LEGAL_EMPRESA { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Nombre Comercial")]
        public string NOMBRE_COMERCIAL_EMPRESA { get; set; }

        [StringLength(20)]
        [Display(Name = "NIT")]
        public string NIT_EMPRESA { get; set; }

        [Required, StringLength(300)]
        [Display(Name = "Dirección")]
        public string DIRECCION_EMPRESA { get; set; }

        [Required]
        [Display(Name = "Teléfono (8 dígitos)")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "Debe contener exactamente 8 dígitos.")]
        public string TELEFONO_EMPRESA { get; set; }   // BD: NUMERIC(8) NOT NULL

        [Display(Name = "WhatsApp (8 dígitos)")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "Debe contener exactamente 8 dígitos.")]
        public string WHATSAPP_EMPRESA { get; set; }   // BD: NUMERIC(8) NULL

        [StringLength(100)]
        [EmailAddress(ErrorMessage = "Correo no válido.")]
        [Display(Name = "Correo")]
        public string CORREO_EMPRESA { get; set; }

        [StringLength(30)]
        [Display(Name = "Descripción corta")]
        public string DESCRIPCION_EMPRESA { get; set; }

        [Display(Name = "Logo actual")]
        public string LOGO_EMPRESA { get; set; }       // Ruta pública (se guarda en BD)

        [Display(Name = "Cambiar logo")]
        public IFormFile LogoFile { get; set; }        // Archivo a subir

        // Auditoría (solo lectura)
        public string USUARIO_CREACION { get; set; }
        public DateTime? FECHA_CREACION { get; set; }
        public string USUARIO_MODIFICACION { get; set; }
        public DateTime? FECHA_MODIFICACION { get; set; }

        [Display(Name = "Activo")]
        public bool ESTADO { get; set; }
        public bool ELIMINADO { get; set; }
    }
}
