using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("EMPLEADO_ID", Name = "IX_USUARIO_EMPLEADO")]
[Index("USUARIO_NOMBRE", Name = "IX_USUARIO_NOMBRE")]
[Index("USUARIO_NOMBRE", Name = "UQ_USUARIO_NOMBRE", IsUnique = true)]
public partial class USUARIO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string USUARIO_ID { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string USUARIO_NOMBRE { get; set; } = null!;

    public byte[] USUARIO_CONTRASENA { get; set; } = null!;

    [MaxLength(64)]
    public byte[]? USUARIO_SALT { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime USUARIO_FECHAREGISTRO { get; set; }

    public bool USUARIO_CAMBIOINICIAL { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string ROL_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string EMPLEADO_ID { get; set; } = null!;

    [StringLength(150)]
    [Unicode(false)]
    public string? USUARIO_CORREO { get; set; }

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

    [InverseProperty("USUARIO")]
    public virtual ICollection<BITACORA> BITACORA { get; set; } = new List<BITACORA>();

    [InverseProperty("USUARIO_APERTURA")]
    public virtual ICollection<CAJA_SESION> CAJA_SESIONUSUARIO_APERTURA { get; set; } = new List<CAJA_SESION>();

    [InverseProperty("USUARIO_CIERRE")]
    public virtual ICollection<CAJA_SESION> CAJA_SESIONUSUARIO_CIERRE { get; set; } = new List<CAJA_SESION>();

    [ForeignKey("EMPLEADO_ID")]
    [InverseProperty("USUARIO")]
    public virtual EMPLEADO EMPLEADO { get; set; } = null!;

    [InverseProperty("USUARIO")]
    public virtual ICollection<HISTORIAL_CONTRASENA> HISTORIAL_CONTRASENA { get; set; } = new List<HISTORIAL_CONTRASENA>();

    [ForeignKey("ROL_ID")]
    [InverseProperty("USUARIO")]
    public virtual ROL ROL { get; set; } = null!;

    [InverseProperty("USUARIO")]
    public virtual ICollection<TOKEN_RECUPERACION> TOKEN_RECUPERACION { get; set; } = new List<TOKEN_RECUPERACION>();

    [InverseProperty("USUARIO")]
    public virtual ICollection<VENTA> VENTA { get; set; } = new List<VENTA>();
}
