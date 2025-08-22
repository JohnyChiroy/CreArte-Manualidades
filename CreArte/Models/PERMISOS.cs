using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("MODULO_ID", Name = "IX_PERMISOS_MODULO")]
[Index("ROL_ID", Name = "IX_PERMISOS_ROL")]
public partial class PERMISOS
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PERMISOS_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string ROL_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string MODULO_ID { get; set; } = null!;

    public bool VISUALIZAR { get; set; }

    public bool CREAR { get; set; }

    public bool MODIFICAR { get; set; }

    public bool ELIMINAR { get; set; }

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

    [ForeignKey("MODULO_ID")]
    [InverseProperty("PERMISOS")]
    public virtual MODULO MODULO { get; set; } = null!;

    [ForeignKey("ROL_ID")]
    [InverseProperty("PERMISOS")]
    public virtual ROL ROL { get; set; } = null!;
}
