using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class TOKEN_RECUPERACION
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string TOKEN_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string USUARIO_ID { get; set; } = null!;

    [StringLength(200)]
    [Unicode(false)]
    public string TOKEN_VALOR { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime TOKEN_EXPIRA { get; set; }

    public bool TOKEN_USADO { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? USADO_EN { get; set; }

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
    [InverseProperty("TOKEN_RECUPERACION")]
    public virtual USUARIO USUARIO { get; set; } = null!;
}
