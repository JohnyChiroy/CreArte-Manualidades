using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class TIPO_CLIENTE
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string TIPO_CLIENTE_ID { get; set; } = null!;

    [StringLength(30)]
    [Unicode(false)]
    public string TIPO_CLIENTE_NOMBRE { get; set; } = null!;

    [StringLength(30)]
    [Unicode(false)]
    public string? TIPO_CLIENTE_DESCRIPCION { get; set; }

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

    [InverseProperty("TIPO_CLIENTE")]
    public virtual ICollection<CLIENTE> CLIENTE { get; set; } = new List<CLIENTE>();
}
