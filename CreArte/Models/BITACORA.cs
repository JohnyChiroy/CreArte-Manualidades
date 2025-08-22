using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class BITACORA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string BITACORA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string? USUARIO_ID { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string ACCION { get; set; } = null!;

    [StringLength(1000)]
    [Unicode(false)]
    public string? DESCRIPCION { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime FECHA_ACCION { get; set; }

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
    [InverseProperty("BITACORA")]
    public virtual USUARIO? USUARIO { get; set; }
}
