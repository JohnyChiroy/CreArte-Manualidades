using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class CLIENTE
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string CLIENTE_ID { get; set; } = null!;

    [StringLength(250)]
    [Unicode(false)]
    public string? CLIENTE_NOTA { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string TIPO_CLIENTE_ID { get; set; } = null!;

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
    [InverseProperty("CLIENTE")]
    public virtual PERSONA CLIENTENavigation { get; set; } = null!;

    [InverseProperty("CLIENTE")]
    public virtual ICollection<PEDIDO> PEDIDO { get; set; } = new List<PEDIDO>();

    [ForeignKey("TIPO_CLIENTE_ID")]
    [InverseProperty("CLIENTE")]
    public virtual TIPO_CLIENTE TIPO_CLIENTE { get; set; } = null!;

    [InverseProperty("CLIENTE")]
    public virtual ICollection<VENTA> VENTA { get; set; } = new List<VENTA>();
}
