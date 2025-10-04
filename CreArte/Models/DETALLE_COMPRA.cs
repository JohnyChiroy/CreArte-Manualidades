using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class DETALLE_COMPRA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string DETALLE_COMPRA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string COMPRA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    public int CANTIDAD { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal? PRECIO_COMPRA { get; set; }

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

    public int? CANTIDAD_RECIBIDA { get; set; }

    [ForeignKey("COMPRA_ID")]
    [InverseProperty("DETALLE_COMPRA")]
    public virtual COMPRA COMPRA { get; set; } = null!;

    [ForeignKey("PRODUCTO_ID")]
    [InverseProperty("DETALLE_COMPRA")]
    public virtual PRODUCTO PRODUCTO { get; set; } = null!;
}
