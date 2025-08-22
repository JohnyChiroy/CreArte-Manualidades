using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class AREA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string AREA_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string AREA_NOMBRE { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? AREA_DESCRIPCION { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string NIVEL_ID { get; set; } = null!;

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

    [ForeignKey("NIVEL_ID")]
    [InverseProperty("AREA")]
    public virtual NIVEL NIVEL { get; set; } = null!;

    [InverseProperty("AREA")]
    public virtual ICollection<PUESTO> PUESTO { get; set; } = new List<PUESTO>();
}
