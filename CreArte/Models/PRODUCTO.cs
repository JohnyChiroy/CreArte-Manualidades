using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

[Index("PRODUCTO_NOMBRE", Name = "IX_PRODUCTO_NOMBRE")]
public partial class PRODUCTO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string PRODUCTO_NOMBRE { get; set; } = null!;

    [StringLength(250)]
    [Unicode(false)]
    public string? PRODUCTO_DESCRIPCION { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string SUBCATEGORIA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string TIPO_PRODUCTO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string UNIDAD_MEDIDA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string? TIPO_EMPAQUE_ID { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string? MARCA_ID { get; set; }

    [StringLength(500)]
    [Unicode(false)]
    public string? IMAGEN_PRODUCTO { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? PORCENTAJE_IVA { get; set; }

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

    [InverseProperty("PRODUCTO")]
    public virtual ICollection<DETALLE_COMPRA> DETALLE_COMPRA { get; set; } = new List<DETALLE_COMPRA>();

    [InverseProperty("PRODUCTO")]
    public virtual ICollection<DETALLE_PEDIDO> DETALLE_PEDIDO { get; set; } = new List<DETALLE_PEDIDO>();

    [InverseProperty("PRODUCTO")]
    public virtual ICollection<INVENTARIO> INVENTARIO { get; set; } = new List<INVENTARIO>();

    [InverseProperty("PRODUCTO")]
    public virtual ICollection<KARDEX> KARDEX { get; set; } = new List<KARDEX>();

    [ForeignKey("MARCA_ID")]
    [InverseProperty("PRODUCTO")]
    public virtual MARCA? MARCA { get; set; }

    [InverseProperty("PRODUCTO")]
    public virtual ICollection<PRECIO_HISTORICO> PRECIO_HISTORICO { get; set; } = new List<PRECIO_HISTORICO>();

    [ForeignKey("SUBCATEGORIA_ID")]
    [InverseProperty("PRODUCTO")]
    public virtual SUBCATEGORIA SUBCATEGORIA { get; set; } = null!;

    [ForeignKey("TIPO_EMPAQUE_ID")]
    [InverseProperty("PRODUCTO")]
    public virtual TIPO_EMPAQUE? TIPO_EMPAQUE { get; set; }

    [ForeignKey("TIPO_PRODUCTO_ID")]
    [InverseProperty("PRODUCTO")]
    public virtual TIPO_PRODUCTO TIPO_PRODUCTO { get; set; } = null!;

    [ForeignKey("UNIDAD_MEDIDA_ID")]
    [InverseProperty("PRODUCTO")]
    public virtual UNIDAD_MEDIDA UNIDAD_MEDIDA { get; set; } = null!;
}
