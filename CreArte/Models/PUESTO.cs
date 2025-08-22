using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class PUESTO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PUESTO_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string PUESTO_NOMBRE { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? PUESTO_DESCRIPCION { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string AREA_ID { get; set; } = null!;

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

    [ForeignKey("AREA_ID")]
    [InverseProperty("PUESTO")]
    public virtual AREA AREA { get; set; } = null!;

    [InverseProperty("PUESTO")]
    public virtual ICollection<EMPLEADO> EMPLEADO { get; set; } = new List<EMPLEADO>();

    [ForeignKey("NIVEL_ID")]
    [InverseProperty("PUESTO")]
    public virtual NIVEL NIVEL { get; set; } = null!;
}
