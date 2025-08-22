using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("PROVEEDOR_ID", "ESTADO_COMPRA_ID", "FECHA_COMPRA", Name = "IX_COMPRA_PROV_ESTADO")]
public partial class COMPRA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string COMPRA_ID { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA_COMPRA { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string ESTADO_COMPRA_ID { get; set; } = null!;

    public DateOnly? FECHA_ENTREGA_COMPRA { get; set; }

    [StringLength(250)]
    [Unicode(false)]
    public string? OBSERVACIONES_COMPRA { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string PROVEEDOR_ID { get; set; } = null!;

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

    [InverseProperty("COMPRA")]
    public virtual ICollection<DETALLE_COMPRA> DETALLE_COMPRA { get; set; } = new List<DETALLE_COMPRA>();

    [ForeignKey("ESTADO_COMPRA_ID")]
    [InverseProperty("COMPRA")]
    public virtual ESTADO_COMPRA ESTADO_COMPRA { get; set; } = null!;

    [ForeignKey("PROVEEDOR_ID")]
    [InverseProperty("COMPRA")]
    public virtual PROVEEDOR PROVEEDOR { get; set; } = null!;
}
