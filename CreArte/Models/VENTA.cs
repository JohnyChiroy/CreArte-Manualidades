using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("FECHA", "USUARIO_ID", Name = "IX_VENTA_FECHA_USUARIO")]
public partial class VENTA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string VENTA_ID { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string CLIENTE_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string USUARIO_ID { get; set; } = null!;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal TOTAL { get; set; }

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

    [ForeignKey("CLIENTE_ID")]
    [InverseProperty("VENTA")]
    public virtual CLIENTE CLIENTE { get; set; } = null!;

    [InverseProperty("VENTA")]
    public virtual ICollection<DETALLE_VENTA> DETALLE_VENTA { get; set; } = new List<DETALLE_VENTA>();

    [InverseProperty("VENTA")]
    public virtual ICollection<RECIBO> RECIBO { get; set; } = new List<RECIBO>();

    [ForeignKey("USUARIO_ID")]
    [InverseProperty("VENTA")]
    public virtual USUARIO USUARIO { get; set; } = null!;
}
