using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class MARCA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string MARCA_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string MARCA_NOMBRE { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? MARCA_DESCRIPCION { get; set; }

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

    [InverseProperty("MARCA")]
    public virtual ICollection<PRODUCTO> PRODUCTO { get; set; } = new List<PRODUCTO>();
}
