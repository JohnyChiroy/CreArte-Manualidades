using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class HISTORIAL_CONTRASENA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string HISTORIAL_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string USUARIO_ID { get; set; } = null!;

    public byte[] HASH { get; set; } = null!;

    [MaxLength(64)]
    public byte[]? SALT { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime FECHA_CAMBIO { get; set; }

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

    [ForeignKey("USUARIO_ID")]
    [InverseProperty("HISTORIAL_CONTRASENA")]
    public virtual USUARIO USUARIO { get; set; } = null!;
}
