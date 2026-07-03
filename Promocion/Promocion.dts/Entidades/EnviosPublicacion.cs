using System;
using System.Collections.Generic;

namespace Promocion.dts.Entidades;

public partial class EnviosPublicacion
{
    public long Id { get; set; }

    public long PublicacionId { get; set; }

    public DateTime FechaIntento { get; set; }

    public ulong Exitoso { get; set; }

    public string? MensajeError { get; set; }

    public int? TelegramMensajeId { get; set; }

    public virtual Publicacione Publicacion { get; set; } = null!;
}
