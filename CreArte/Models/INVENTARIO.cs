using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("INVENTARIO_ID", "PRODUCTO_ID", Name = "UQ_INVENTARIO_ID_PROD", IsUnique = true)]
public partial class INVENTARIO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string INVENTARIO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    public int STOCK_ACTUAL { get; set; }

    public int STOCK_MINIMO { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal COSTO_UNITARIO { get; set; }

    public DateOnly? FECHA_VENCIMIENTO { get; set; }

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

    [InverseProperty("INVENTARIO")]
    public virtual ICollection<DETALLE_VENTA> DETALLE_VENTA { get; set; } = new List<DETALLE_VENTA>();

    [InverseProperty("INVENTARIO")]
    public virtual ICollection<NOTIFICACION> NOTIFICACION { get; set; } = new List<NOTIFICACION>();

    [ForeignKey("PRODUCTO_ID")]
    [InverseProperty("INVENTARIO")]
    public virtual PRODUCTO PRODUCTO { get; set; } = null!;
}
