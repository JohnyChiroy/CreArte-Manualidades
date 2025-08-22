using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class CAJA_SESION
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string SESION_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string CAJA_ID { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA_APERTURA { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string USUARIO_APERTURA_ID { get; set; } = null!;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal MONTO_INICIAL { get; set; }

    [StringLength(250)]
    [Unicode(false)]
    public string? NOTAAPERTURA { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FECHA_CIERRE { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string? USUARIO_CIERRE_ID { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal? MONTO_FINAL { get; set; }

    [StringLength(250)]
    [Unicode(false)]
    public string? NOTACIERRE { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string ESTADO_SESION { get; set; } = null!;

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

    [ForeignKey("CAJA_ID")]
    [InverseProperty("CAJA_SESION")]
    public virtual CAJA CAJA { get; set; } = null!;

    [InverseProperty("SESION")]
    public virtual ICollection<MOVIMIENTO_CAJA> MOVIMIENTO_CAJA { get; set; } = new List<MOVIMIENTO_CAJA>();

    [ForeignKey("USUARIO_APERTURA_ID")]
    [InverseProperty("CAJA_SESIONUSUARIO_APERTURA")]
    public virtual USUARIO USUARIO_APERTURA { get; set; } = null!;

    [ForeignKey("USUARIO_CIERRE_ID")]
    [InverseProperty("CAJA_SESIONUSUARIO_CIERRE")]
    public virtual USUARIO? USUARIO_CIERRE { get; set; }
}
