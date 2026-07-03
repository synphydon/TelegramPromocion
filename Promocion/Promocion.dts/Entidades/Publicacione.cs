using System;
using System.Collections.Generic;

namespace Promocion.dts.Entidades;

public partial class Publicacione
{
    public long Id { get; set; }

    public long UsuarioId { get; set; }

    public string? Texto { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaFinalizacion { get; set; }

    public virtual ICollection<ArchivosPublicacion> ArchivosPublicacions { get; set; } = new List<ArchivosPublicacion>();

    public virtual ICollection<EnviosPublicacion> EnviosPublicacions { get; set; } = new List<EnviosPublicacion>();

    public virtual Programacione? Programacione { get; set; }

    public virtual ICollection<SesionesConversacion> SesionesConversacions { get; set; } = new List<SesionesConversacion>();

    public virtual Usuario Usuario { get; set; } = null!;
}
