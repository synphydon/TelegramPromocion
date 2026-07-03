using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Promocion.dts.Entidades;

namespace Promocion.dts.Datos;

public partial class ContextoTelegram : DbContext
{
    public ContextoTelegram(DbContextOptions<ContextoTelegram> options)
        : base(options)
    {
    }

    public virtual DbSet<ArchivosPublicacion> ArchivosPublicacions { get; set; }

    public virtual DbSet<EnviosPublicacion> EnviosPublicacions { get; set; }

    public virtual DbSet<Programacione> Programaciones { get; set; }

    public virtual DbSet<Publicacione> Publicaciones { get; set; }

    public virtual DbSet<SesionesConversacion> SesionesConversacions { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArchivosPublicacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("ArchivosPublicacion");

            entity.HasIndex(e => new { e.PublicacionId, e.Orden }, "UX_ArchivosPublicacion_PublicacionId_Orden").IsUnique();

            entity.Property(e => e.FechaCreacion)
                .HasMaxLength(6)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
            entity.Property(e => e.NombreOriginal).HasMaxLength(255);
            entity.Property(e => e.RutaRelativa).HasMaxLength(1000);
            entity.Property(e => e.TelegramFileId).HasMaxLength(255);
            entity.Property(e => e.Tipo).HasMaxLength(20);

            entity.HasOne(d => d.Publicacion).WithMany(p => p.ArchivosPublicacions)
                .HasForeignKey(d => d.PublicacionId)
                .HasConstraintName("FK_ArchivosPublicacion_Publicaciones");
        });

        modelBuilder.Entity<EnviosPublicacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("EnviosPublicacion");

            entity.HasIndex(e => e.PublicacionId, "IX_EnviosPublicacion_PublicacionId");

            entity.Property(e => e.Exitoso).HasColumnType("bit(1)");
            entity.Property(e => e.FechaIntento)
                .HasMaxLength(6)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
            entity.Property(e => e.MensajeError).HasColumnType("text");

            entity.HasOne(d => d.Publicacion).WithMany(p => p.EnviosPublicacions)
                .HasForeignKey(d => d.PublicacionId)
                .HasConstraintName("FK_EnviosPublicacion_Publicaciones");
        });

        modelBuilder.Entity<Programacione>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => new { e.Estado, e.FechaProximaEjecucion }, "IX_Programaciones_Estado_Fecha");

            entity.HasIndex(e => e.PublicacionId, "UX_Programaciones_PublicacionId").IsUnique();

            entity.Property(e => e.Estado)
                .HasMaxLength(30)
                .HasDefaultValueSql("'Pendiente'");
            entity.Property(e => e.FechaCreacion)
                .HasMaxLength(6)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
            entity.Property(e => e.FechaProximaEjecucion).HasMaxLength(6);
            entity.Property(e => e.Tipo).HasMaxLength(30);
            entity.Property(e => e.UltimaEjecucion).HasMaxLength(6);

            entity.HasOne(d => d.Publicacion).WithOne(p => p.Programacione)
                .HasForeignKey<Programacione>(d => d.PublicacionId)
                .HasConstraintName("FK_Programaciones_Publicaciones");
        });

        modelBuilder.Entity<Publicacione>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.UsuarioId, "IX_Publicaciones_UsuarioId");

            entity.Property(e => e.Estado)
                .HasMaxLength(30)
                .HasDefaultValueSql("'Borrador'");
            entity.Property(e => e.FechaCreacion)
                .HasMaxLength(6)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
            entity.Property(e => e.FechaFinalizacion).HasMaxLength(6);
            entity.Property(e => e.Texto).HasColumnType("text");

            entity.HasOne(d => d.Usuario).WithMany(p => p.Publicaciones)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Publicaciones_Usuarios");
        });

        modelBuilder.Entity<SesionesConversacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("SesionesConversacion");

            entity.HasIndex(e => e.PublicacionActualId, "FK_SesionesConversacion_Publicaciones");

            entity.HasIndex(e => e.UsuarioId, "UX_SesionesConversacion_UsuarioId").IsUnique();

            entity.Property(e => e.DatosTemporales).HasMaxLength(1000);
            entity.Property(e => e.Estado)
                .HasMaxLength(40)
                .HasDefaultValueSql("'Inicio'");
            entity.Property(e => e.FechaActualizacion)
                .HasMaxLength(6)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");

            entity.HasOne(d => d.PublicacionActual).WithMany(p => p.SesionesConversacions)
                .HasForeignKey(d => d.PublicacionActualId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_SesionesConversacion_Publicaciones");

            entity.HasOne(d => d.Usuario).WithOne(p => p.SesionesConversacion)
                .HasForeignKey<SesionesConversacion>(d => d.UsuarioId)
                .HasConstraintName("FK_SesionesConversacion_Usuarios");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.TelegramUsuarioId, "UX_Usuarios_TelegramUsuarioId").IsUnique();

            entity.Property(e => e.FechaAlta)
                .HasMaxLength(6)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
            entity.Property(e => e.NombreCompleto).HasMaxLength(255);
            entity.Property(e => e.NombreUsuario).HasMaxLength(255);
            entity.Property(e => e.UltimaInteraccion)
                .HasMaxLength(6)
                .HasDefaultValueSql("'CURRENT_TIMESTAMP(6)'");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
