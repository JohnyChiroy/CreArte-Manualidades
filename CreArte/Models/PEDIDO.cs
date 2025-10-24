using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("CLIENTE_ID", "ESTADO_PEDIDO_ID", "FECHA_PEDIDO", Name = "IX_PEDIDO_PROV_ESTADO")]
public partial class PEDIDO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PEDIDO_ID { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA_PEDIDO { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string ESTADO_PEDIDO_ID { get; set; } = null!;

    public DateOnly? FECHA_ENTREGA_PEDIDO { get; set; }

    [StringLength(250)]
    [Unicode(false)]
    public string? OBSERVACIONES_PEDIDO { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string CLIENTE_ID { get; set; } = null!;

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

    public bool REQUIERE_ANTICIPO { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal ANTICIPO_MINIMO { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string? ANTICIPO_ESTADO { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal TOTAL_PEDIDO { get; set; }

    [ForeignKey("CLIENTE_ID")]
    [InverseProperty("PEDIDO")]
    public virtual CLIENTE CLIENTE { get; set; } = null!;

    [InverseProperty("PEDIDO")]
    public virtual ICollection<DETALLE_PEDIDO> DETALLE_PEDIDO { get; set; } = new List<DETALLE_PEDIDO>();

    [ForeignKey("ESTADO_PEDIDO_ID")]
    [InverseProperty("PEDIDO")]
    public virtual ESTADO_PEDIDO ESTADO_PEDIDO { get; set; } = null!;
}
