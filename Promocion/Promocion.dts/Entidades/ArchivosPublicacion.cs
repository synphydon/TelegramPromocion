using System;
using System.Collections.Generic;

namespace Promocion.dts.Entidades;

public partial class ArchivosPublicacion
{
    public long Id { get; set; }

    public long PublicacionId { get; set; }

    public string Tipo { get; set; } = null!;

    public string RutaRelativa { get; set; } = null!;

    public string? NombreOriginal { get; set; }

    public string? TelegramFileId { get; set; }

    public int Orden { get; set; }

    public DateTime FechaCreacion { get; set; }

    public virtual Publicacione Publicacion { get; set; } = null!;
}
