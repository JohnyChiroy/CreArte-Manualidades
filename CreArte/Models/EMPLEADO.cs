using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class EMPLEADO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string EMPLEADO_ID { get; set; } = null!;

    public DateOnly? EMPLEADO_FECHANACIMIENTO { get; set; }

    public DateOnly EMPLEADO_FECHAINGRESO { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string? EMPLEADO_GENERO { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string PUESTO_ID { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string USUARIO_CREACION { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA_CREACION { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? USUARIO_MODIFICACION { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FECHA_MODIFICACION { get; set; }

    public bool ELIMINADO { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? USUARIO_ELIMINACION { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FECHA_ELIMINACION { get; set; }

    public bool ESTADO { get; set; }

    [ForeignKey("EMPLEADO_ID")]
    [InverseProperty("EMPLEADO")]
    public virtual PERSONA EMPLEADONavigation { get; set; } = null!;

    [ForeignKey("PUESTO_ID")]
    [InverseProperty("EMPLEADO")]
    public virtual PUESTO PUESTO { get; set; } = null!;

    [InverseProperty("EMPLEADO")]
    public virtual ICollection<USUARIO> USUARIO { get; set; } = new List<USUARIO>();

    
        // =========================================================
        // Navegación a PERSONA (1 a 1)
        // Nota: La FK es EMPLEADO.EMPLEADO_ID -> PERSONA.PERSONA_ID,
        // por eso usamos el mismo campo como FK.
        //// =========================================================
        //[ForeignKey(nameof(EMPLEADO_ID))]
        //public virtual PERSONA PERSONA { get; set; } = default!;
}
