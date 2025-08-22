using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class NOTIFICACION
{
    [Key]
    public int NOTIFICACION_ID { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string INVENTARIO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    [StringLength(15)]
    [Unicode(false)]
    public string TIPO_NOTIFICACION { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? MENSAJE { get; set; }

    public byte? NIVEL { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime FECHA_DETECCION { get; set; }

    public bool RESUELTA { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RESUELTA_EN { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? RESUELTA_POR { get; set; }

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
    [InverseProperty("NOTIFICACION")]
    public virtual INVENTARIO INVENTARIO { get; set; } = null!;
}
