using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class MODULO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string MODULO_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string MODULO_NOMBRE { get; set; } = null!;

    [StringLength(250)]
    [Unicode(false)]
    public string? MODULO_DESCRIPCION { get; set; }

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

    [InverseProperty("MODULO")]
    public virtual ICollection<PERMISOS> PERMISOS { get; set; } = new List<PERMISOS>();
}
