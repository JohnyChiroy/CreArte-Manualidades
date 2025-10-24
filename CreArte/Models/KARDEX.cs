using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("PRODUCTO_ID", "FECHA", Name = "IX_KARDEX_PROD_FEC")]
[Index("PRODUCTO_ID", "FECHA", Name = "IX_KARDEX_PROD_FECHA", IsDescending = new[] { false, true })]
[Index("TIPO_MOVIMIENTO", "FECHA", Name = "IX_KARDEX_TIPO_FECHA", IsDescending = new[] { false, true })]
public partial class KARDEX
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string KARDEX_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string TIPO_MOVIMIENTO { get; set; } = null!;

    public int CANTIDAD { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal? COSTO_UNITARIO { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? REFERENCIA { get; set; }

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
    [InverseProperty("KARDEX")]
    public virtual PRODUCTO PRODUCTO { get; set; } = null!;
}
