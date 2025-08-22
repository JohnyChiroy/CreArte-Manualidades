using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class EMPRESA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string EMPRESA_ID { get; set; } = null!;

    [StringLength(100)]
    public string? NOMBRE_LEGAL_EMPRESA { get; set; }

    [StringLength(200)]
    public string NOMBRE_COMERCIAL_EMPRESA { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string? NIT_EMPRESA { get; set; }

    [StringLength(300)]
    public string DIRECCION_EMPRESA { get; set; } = null!;

    [Column(TypeName = "numeric(8, 0)")]
    public decimal TELEFONO_EMPRESA { get; set; }

    [Column(TypeName = "numeric(8, 0)")]
    public decimal? WHATSAPP_EMPRESA { get; set; }

    [StringLength(100)]
    public string? CORREO_EMPRESA { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string? DESCRIPCION_EMPRESA { get; set; }

    [StringLength(500)]
    [Unicode(false)]
    public string? LOGO_EMPRESA { get; set; }

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
}
