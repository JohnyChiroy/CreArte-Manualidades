using System;
using System.Collections.Generic;
using CreArte.Models;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Data;

public partial class CreArteDbContext : DbContext
{
    public CreArteDbContext()
    {
    }

    public CreArteDbContext(DbContextOptions<CreArteDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AREA> AREA { get; set; }

    public virtual DbSet<AUD_DDL> AUD_DDL { get; set; }

    public virtual DbSet<BITACORA> BITACORA { get; set; }

    public virtual DbSet<CAJA> CAJA { get; set; }

    public virtual DbSet<CAJA_SESION> CAJA_SESION { get; set; }

    public virtual DbSet<CATEGORIA> CATEGORIA { get; set; }

    public virtual DbSet<CLIENTE> CLIENTE { get; set; }

    public virtual DbSet<COMPRA> COMPRA { get; set; }

    public virtual DbSet<DETALLE_COMPRA> DETALLE_COMPRA { get; set; }

    public virtual DbSet<DETALLE_PEDIDO> DETALLE_PEDIDO { get; set; }

    public virtual DbSet<DETALLE_VENTA> DETALLE_VENTA { get; set; }

    public virtual DbSet<EMPLEADO> EMPLEADO { get; set; }

    public virtual DbSet<EMPRESA> EMPRESA { get; set; }

    public virtual DbSet<ESTADO_COMPRA> ESTADO_COMPRA { get; set; }

    public virtual DbSet<ESTADO_PEDIDO> ESTADO_PEDIDO { get; set; }

    public virtual DbSet<HISTORIAL_CONTRASENA> HISTORIAL_CONTRASENA { get; set; }

    public virtual DbSet<INVENTARIO> INVENTARIO { get; set; }

    public virtual DbSet<KARDEX> KARDEX { get; set; }

    public virtual DbSet<MARCA> MARCA { get; set; }

    public virtual DbSet<METODO_PAGO> METODO_PAGO { get; set; }

    public virtual DbSet<MODULO> MODULO { get; set; }

    public virtual DbSet<MOVIMIENTO_CAJA> MOVIMIENTO_CAJA { get; set; }

    public virtual DbSet<NIVEL> NIVEL { get; set; }

    public virtual DbSet<NOTIFICACION> NOTIFICACION { get; set; }

    public virtual DbSet<PEDIDO> PEDIDO { get; set; }

    public virtual DbSet<PERMISOS> PERMISOS { get; set; }

    public virtual DbSet<PERSONA> PERSONA { get; set; }

    public virtual DbSet<PRECIO_HISTORICO> PRECIO_HISTORICO { get; set; }

    public virtual DbSet<PRODUCTO> PRODUCTO { get; set; }

    public virtual DbSet<PROVEEDOR> PROVEEDOR { get; set; }

    public virtual DbSet<PUESTO> PUESTO { get; set; }

    public virtual DbSet<RECIBO> RECIBO { get; set; }

    public virtual DbSet<ROL> ROL { get; set; }

    public virtual DbSet<SUBCATEGORIA> SUBCATEGORIA { get; set; }

    public virtual DbSet<TIPO_CLIENTE> TIPO_CLIENTE { get; set; }

    public virtual DbSet<TIPO_EMPAQUE> TIPO_EMPAQUE { get; set; }

    public virtual DbSet<TIPO_PRODUCTO> TIPO_PRODUCTO { get; set; }

    public virtual DbSet<TOKEN_RECUPERACION> TOKEN_RECUPERACION { get; set; }

    public virtual DbSet<UNIDAD_MEDIDA> UNIDAD_MEDIDA { get; set; }

    public virtual DbSet<USUARIO> USUARIO { get; set; }

    public virtual DbSet<VENTA> VENTA { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    => optionsBuilder.UseSqlServer("workstation id=CreArteDB_P.mssql.somee.com;packet size=4096;user id=johnychiroy_SQLLogin_1;pwd=w2wlzijw6v;data source=CreArteDB_P.mssql.somee.com;persist security info=False;initial catalog=CreArteDB_P;TrustServerCertificate=True;");
    //=> optionsBuilder.UseSqlServer("Server=EC2AMAZ-S6EEQC3\\SQLEXPRESS;Database=CreArteDB_P;User Id=sa;Password=Atriox.117;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AREA>(entity =>
        {
            entity.HasKey(e => e.AREA_ID).HasName("PK_AREA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.NIVEL).WithMany(p => p.AREA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NIVEL_ID");
        });

        modelBuilder.Entity<AUD_DDL>(entity =>
        {
            entity.HasKey(e => e.AUD_ID).HasName("PK__AUD_DDL__C12C9F2E10AF9727");

            entity.Property(e => e.FECHA).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LOGIN_NAME).HasDefaultValueSql("(original_login())");
        });

        modelBuilder.Entity<BITACORA>(entity =>
        {
            entity.HasKey(e => e.BITACORA_ID).HasName("PK_BITACORA_ID");

            entity.Property(e => e.FECHA_ACCION).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.USUARIO).WithMany(p => p.BITACORA).HasConstraintName("FK_BITACORA_USUARIO");
        });

        modelBuilder.Entity<CAJA>(entity =>
        {
            entity.HasKey(e => e.CAJA_ID).HasName("PK_CAJA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<CAJA_SESION>(entity =>
        {
            entity.HasIndex(e => e.CAJA_ID, "UX_CAJA_SESION_CAJA_ABIERTA")
                .IsUnique()
                .HasFilter("([ESTADO_SESION]='ABIERTA' AND [ELIMINADO]=(0))");

            entity.Property(e => e.ESTADO_SESION).HasDefaultValue("ABIERTA");
            entity.Property(e => e.FECHA_APERTURA).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CAJA).WithOne(p => p.CAJA_SESION)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CAJA_SESION_CAJA");

            entity.HasOne(d => d.USUARIO_APERTURA).WithMany(p => p.CAJA_SESIONUSUARIO_APERTURA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CAJA_SESION_USR_APER");

            entity.HasOne(d => d.USUARIO_CIERRE).WithMany(p => p.CAJA_SESIONUSUARIO_CIERRE).HasConstraintName("FK_CAJA_SESION_USR_CIERRE");
        });

        modelBuilder.Entity<CATEGORIA>(entity =>
        {
            entity.HasKey(e => e.CATEGORIA_ID).HasName("PK_CATEGORIA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<CLIENTE>(entity =>
        {
            entity.HasKey(e => e.CLIENTE_ID).HasName("PK_CLIENTE_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CLIENTENavigation).WithOne(p => p.CLIENTE)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CLIENTE_PERSONA");

            entity.HasOne(d => d.TIPO_CLIENTE).WithMany(p => p.CLIENTE)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TIPO_CLIENTE_ID");
        });

        modelBuilder.Entity<COMPRA>(entity =>
        {
            entity.Property(e => e.FECHA_COMPRA).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ESTADO_COMPRA).WithMany(p => p.COMPRA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_COMPRA_ESTADO");

            entity.HasOne(d => d.PROVEEDOR).WithMany(p => p.COMPRA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PEDIDO_PROVEEDOR");
        });

        modelBuilder.Entity<DETALLE_COMPRA>(entity =>
        {
            entity.HasKey(e => e.DETALLE_COMPRA_ID).HasName("PK_DETALLE_COMPRA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.COMPRA).WithMany(p => p.DETALLE_COMPRA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DETALLE_COMPRA_COMPRA");

            entity.HasOne(d => d.PRODUCTO).WithMany(p => p.DETALLE_COMPRA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DETALLE_COMPRA_PRODUCTO");
        });

        modelBuilder.Entity<DETALLE_PEDIDO>(entity =>
        {
            entity.HasKey(e => e.DETALLE_PEDIDO_ID).HasName("PK_DETALLE_PEDIDO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PEDIDO).WithMany(p => p.DETALLE_PEDIDO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DETALLE_PEDIDO_PEDIDO");

            entity.HasOne(d => d.PRODUCTO).WithMany(p => p.DETALLE_PEDIDO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DETALLE_PEDIDO_PRODUCTO");
        });

        modelBuilder.Entity<DETALLE_VENTA>(entity =>
        {
            entity.HasKey(e => e.DETALLE_VENTA_ID).HasName("PK_DETALLE_VENTA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SUBTOTAL).HasComputedColumnSql("([CANTIDAD]*[PRECIO_UNITARIO])", true);

            entity.HasOne(d => d.VENTA).WithMany(p => p.DETALLE_VENTA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DETALLE_VENTA_VENTA");

            entity.HasOne(d => d.INVENTARIO).WithMany(p => p.DETALLE_VENTA)
                .HasPrincipalKey(p => new { p.INVENTARIO_ID, p.PRODUCTO_ID })
                .HasForeignKey(d => new { d.INVENTARIO_ID, d.PRODUCTO_ID })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DETALLE_VENTA_PRODUCTOINVENTARIO");
        });

        modelBuilder.Entity<EMPLEADO>(entity =>
        {
            entity.HasKey(e => e.EMPLEADO_ID).HasName("PK_EMPLEADO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.EMPLEADONavigation).WithOne(p => p.EMPLEADO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMPLEADO_PERSONA");

            entity.HasOne(d => d.PUESTO).WithMany(p => p.EMPLEADO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMPLEADO_PUESTO");
        });

        modelBuilder.Entity<EMPRESA>(entity =>
        {
            entity.HasKey(e => e.EMPRESA_ID).HasName("PK_EMPRESA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ESTADO_COMPRA>(entity =>
        {
            entity.HasKey(e => e.ESTADO_COMPRA_ID).HasName("PK_ESTADO_COMPRA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ESTADO_PEDIDO>(entity =>
        {
            entity.HasKey(e => e.ESTADO_PEDIDO_ID).HasName("PK_ESTADO_PEDIDO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<HISTORIAL_CONTRASENA>(entity =>
        {
            entity.HasKey(e => e.HISTORIAL_ID).HasName("PK_HISTORIAL_ID");

            entity.Property(e => e.FECHA_CAMBIO).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.USUARIO).WithMany(p => p.HISTORIAL_CONTRASENA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HISTORIAL_USUARIO");
        });

        modelBuilder.Entity<INVENTARIO>(entity =>
        {
            entity.HasKey(e => e.INVENTARIO_ID).HasName("PK_INVENTARIO_ID");

            entity.HasIndex(e => e.PRODUCTO_ID, "IX_INV_PRODUCTO_NOELIM").HasFilter("([ELIMINADO]=(0))");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PRODUCTO).WithMany(p => p.INVENTARIO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_INVENTARIO_PRODUCTO");
        });

        modelBuilder.Entity<KARDEX>(entity =>
        {
            entity.HasKey(e => e.KARDEX_ID).HasName("PK_KARDEX_ID");

            entity.Property(e => e.FECHA).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PRODUCTO).WithMany(p => p.KARDEX)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KARDEX_PRODUCTO");
        });

        modelBuilder.Entity<MARCA>(entity =>
        {
            entity.HasKey(e => e.MARCA_ID).HasName("PK_MARCA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<METODO_PAGO>(entity =>
        {
            entity.HasKey(e => e.METODO_PAGO_ID).HasName("PK_METODO_PAGO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<MODULO>(entity =>
        {
            entity.HasKey(e => e.MODULO_ID).HasName("PK_MODULO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<MOVIMIENTO_CAJA>(entity =>
        {
            entity.Property(e => e.FECHA).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.SESION).WithMany(p => p.MOVIMIENTO_CAJA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MOV_CAJA_SESION");
        });

        modelBuilder.Entity<NIVEL>(entity =>
        {
            entity.HasKey(e => e.NIVEL_ID).HasName("PK_NIVEL_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<NOTIFICACION>(entity =>
        {
            entity.HasKey(e => e.NOTIFICACION_ID).HasName("PK__NOTIFICA__5318F8DAF0074F30");

            entity.HasIndex(e => new { e.INVENTARIO_ID, e.TIPO_NOTIFICACION }, "UX_NOTIF_ACTIVA")
                .IsUnique()
                .HasFilter("([RESUELTA]=(0) AND [ELIMINADO]=(0))");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_DETECCION).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.USUARIO_CREACION).HasDefaultValue("SYSTEM");

            entity.HasOne(d => d.INVENTARIO).WithMany(p => p.NOTIFICACION)
                .HasPrincipalKey(p => new { p.INVENTARIO_ID, p.PRODUCTO_ID })
                .HasForeignKey(d => new { d.INVENTARIO_ID, d.PRODUCTO_ID })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NOTIF_INV");
        });

        modelBuilder.Entity<PEDIDO>(entity =>
        {
            entity.HasKey(e => e.PEDIDO_ID).HasName("PK_PEDIDO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_PEDIDO).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CLIENTE).WithMany(p => p.PEDIDO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PEDIDO_CLIENTE");

            entity.HasOne(d => d.ESTADO_PEDIDO).WithMany(p => p.PEDIDO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PEDIDO_ESTADO");
        });

        modelBuilder.Entity<PERMISOS>(entity =>
        {
            entity.HasKey(e => e.PERMISOS_ID).HasName("PK_PERMISOS_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MODULO).WithMany(p => p.PERMISOS)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PERMISOS_MODULO");

            entity.HasOne(d => d.ROL).WithMany(p => p.PERMISOS)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PERMISOS_ROL");
        });

        modelBuilder.Entity<PERSONA>(entity =>
        {
            entity.HasKey(e => e.PERSONA_ID).HasName("PK_PERSONA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.PERSONA_FECHAREGISTRO).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<PRECIO_HISTORICO>(entity =>
        {
            entity.HasKey(e => e.PRECIO_ID).HasName("PK_PRECIO_ID");

            entity.HasIndex(e => e.PRODUCTO_ID, "UX_PRECIO_HISTORICO_VIGENTE")
                .IsUnique()
                .HasFilter("([HASTA] IS NULL AND [ELIMINADO]=(0))");

            entity.Property(e => e.DESDE).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PRODUCTO).WithOne(p => p.PRECIO_HISTORICO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PRECIO_PRODUCTO");
        });

        modelBuilder.Entity<PRODUCTO>(entity =>
        {
            entity.HasKey(e => e.PRODUCTO_ID).HasName("PK_PRODUCTO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MARCA).WithMany(p => p.PRODUCTO).HasConstraintName("FK_PRODUCTO_MARCA");

            entity.HasOne(d => d.SUBCATEGORIA).WithMany(p => p.PRODUCTO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PRODUCTO_SUBCATEGORIA");

            entity.HasOne(d => d.TIPO_EMPAQUE).WithMany(p => p.PRODUCTO).HasConstraintName("FK_PRODUCTO_EMPAQUE");

            entity.HasOne(d => d.TIPO_PRODUCTO).WithMany(p => p.PRODUCTO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PRODUCTO_TIPO");

            entity.HasOne(d => d.UNIDAD_MEDIDA).WithMany(p => p.PRODUCTO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PRODUCTO_UM");
        });

        modelBuilder.Entity<PROVEEDOR>(entity =>
        {
            entity.HasKey(e => e.PROVEEDOR_ID).HasName("PK_PROVEEDOR_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PROVEEDORNavigation).WithOne(p => p.PROVEEDOR)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PROVEEDOR_PERSONA");
        });

        modelBuilder.Entity<PUESTO>(entity =>
        {
            entity.HasKey(e => e.PUESTO_ID).HasName("PK_PUESTO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.AREA).WithMany(p => p.PUESTO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PUESTO_AREA");

            entity.HasOne(d => d.NIVEL).WithMany(p => p.PUESTO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PUESTO_NIVEL_ID");
        });

        modelBuilder.Entity<RECIBO>(entity =>
        {
            entity.HasKey(e => e.RECIBO_ID).HasName("PK_RECIBO_ID");

            entity.Property(e => e.FECHA).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.METODO_PAGO).WithMany(p => p.RECIBO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RECIBO_METODO");

            entity.HasOne(d => d.VENTA).WithMany(p => p.RECIBO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RECIBO_VENTA");
        });

        modelBuilder.Entity<ROL>(entity =>
        {
            entity.HasKey(e => e.ROL_ID).HasName("PK_ROL_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<SUBCATEGORIA>(entity =>
        {
            entity.HasKey(e => e.SUBCATEGORIA_ID).HasName("PK_SUBCATEGORIA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CATEGORIA).WithMany(p => p.SUBCATEGORIA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SUBCATEGORIA_CATEGORIA");
        });

        modelBuilder.Entity<TIPO_CLIENTE>(entity =>
        {
            entity.HasKey(e => e.TIPO_CLIENTE_ID).HasName("PK_TIPO_CLIENTE_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<TIPO_EMPAQUE>(entity =>
        {
            entity.HasKey(e => e.TIPO_EMPAQUE_ID).HasName("PK_TIPO_EMPAQUE_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<TIPO_PRODUCTO>(entity =>
        {
            entity.HasKey(e => e.TIPO_PRODUCTO_ID).HasName("PK_TIPO_PRODUCTO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<TOKEN_RECUPERACION>(entity =>
        {
            entity.HasKey(e => e.TOKEN_ID).HasName("PK_TOKEN_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.USUARIO).WithMany(p => p.TOKEN_RECUPERACION)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TOKEN_USUARIO");
        });

        modelBuilder.Entity<UNIDAD_MEDIDA>(entity =>
        {
            entity.HasKey(e => e.UNIDAD_MEDIDA_ID).HasName("PK_UNIDAD_MEDIDA_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<USUARIO>(entity =>
        {
            entity.HasKey(e => e.USUARIO_ID).HasName("PK_USUARIO_ID");

            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.USUARIO_CAMBIOINICIAL).HasDefaultValue(true);
            entity.Property(e => e.USUARIO_FECHAREGISTRO).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.EMPLEADO).WithMany(p => p.USUARIO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USUARIO_EMPLEADO");

            entity.HasOne(d => d.ROL).WithMany(p => p.USUARIO)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USUARIO_ROL");
        });

        modelBuilder.Entity<VENTA>(entity =>
        {
            entity.HasKey(e => e.VENTA_ID).HasName("PK_VENTA_ID");

            entity.Property(e => e.FECHA).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FECHA_CREACION).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.CLIENTE).WithMany(p => p.VENTA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VENTA_CLIENTE");

            entity.HasOne(d => d.USUARIO).WithMany(p => p.VENTA)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VENTA_USUARIO");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
