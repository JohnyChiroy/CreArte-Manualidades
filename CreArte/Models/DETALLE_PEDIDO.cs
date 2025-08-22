using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("PEDIDO_ID", Name = "IX_DETALLE_PEDIDO_PEDIDO")]
[Index("PRODUCTO_ID", Name = "IX_DETALLE_PEDIDO_PRODUCTO")]
public partial class DETALLE_PEDIDO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string DETALLE_PEDIDO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PEDIDO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    public int CANTIDAD { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal? PRECIO_PEDIDO { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal? PRECIO_VENTA { get; set; }

    public DateOnly? FECHA_VENCIMIENTO { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal SUBTOTAL { get; set; }

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

    [ForeignKey("PEDIDO_ID")]
    [InverseProperty("DETALLE_PEDIDO")]
    public virtual PEDIDO PEDIDO { get; set; } = null!;

    [ForeignKey("PRODUCTO_ID")]
    [InverseProperty("DETALLE_PEDIDO")]
    public virtual PRODUCTO PRODUCTO { get; set; } = null!;
}
