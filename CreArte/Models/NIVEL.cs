using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class NIVEL
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string NIVEL_ID { get; set; } = null!;

    [StringLength(30)]
    [Unicode(false)]
    public string NIVEL_NOMBRE { get; set; } = null!;

    [StringLength(30)]
    [Unicode(false)]
    public string? NIVEL_DESCRIPCION { get; set; }

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

    [InverseProperty("NIVEL")]
    public virtual ICollection<AREA> AREA { get; set; } = new List<AREA>();

    [InverseProperty("NIVEL")]
    public virtual ICollection<PUESTO> PUESTO { get; set; } = new List<PUESTO>();
}
