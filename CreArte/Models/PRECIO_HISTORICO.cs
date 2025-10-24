using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("PRODUCTO_ID", "DESDE", Name = "IX_PRECIO_HIST_PRODUCTO_DESDE", IsDescending = new[] { false, true })]
[Index("PRODUCTO_ID", "DESDE", Name = "IX_PRECIO_PRODUCTO_DESDE")]
public partial class PRECIO_HISTORICO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PRECIO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal PRECIO { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime DESDE { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? HASTA { get; set; }

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

    [ForeignKey("PRODUCTO_ID")]
    [InverseProperty("PRECIO_HISTORICO")]
    public virtual PRODUCTO PRODUCTO { get; set; } = null!;
}
