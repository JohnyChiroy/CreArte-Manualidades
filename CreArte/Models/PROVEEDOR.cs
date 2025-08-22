using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class PROVEEDOR
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PROVEEDOR_ID { get; set; } = null!;

    [StringLength(250)]
    [Unicode(false)]
    public string? PROVEEDOR_OBSERVACION { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? EMPRESA { get; set; }

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

    [InverseProperty("PROVEEDOR")]
    public virtual ICollection<COMPRA> COMPRA { get; set; } = new List<COMPRA>();

    [ForeignKey("PROVEEDOR_ID")]
    [InverseProperty("PROVEEDOR")]
    public virtual PERSONA PROVEEDORNavigation { get; set; } = null!;
}
