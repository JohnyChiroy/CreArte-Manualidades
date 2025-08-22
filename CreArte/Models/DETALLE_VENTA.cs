using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("INVENTARIO_ID", "PRODUCTO_ID", Name = "IX_DETALLE_VENTA_PROD_INV")]
[Index("VENTA_ID", Name = "IX_DETALLE_VENTA_VENTA")]
public partial class DETALLE_VENTA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string DETALLE_VENTA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string VENTA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string INVENTARIO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    public int CANTIDAD { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal PRECIO_UNITARIO { get; set; }

    [Column(TypeName = "decimal(23, 2)")]
    public decimal? SUBTOTAL { get; set; }

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

    [ForeignKey("INVENTARIO_ID, PRODUCTO_ID")]
    [InverseProperty("DETALLE_VENTA")]
    public virtual INVENTARIO INVENTARIO { get; set; } = null!;

    [ForeignKey("VENTA_ID")]
    [InverseProperty("DETALLE_VENTA")]
    public virtual VENTA VENTA { get; set; } = null!;
}
