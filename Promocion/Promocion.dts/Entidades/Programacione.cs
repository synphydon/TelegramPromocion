using System;
using System.Collections.Generic;

namespace Promocion.dts.Entidades;

public partial class Programacione
{
    public long Id { get; set; }

    public long PublicacionId { get; set; }

    public string Tipo { get; set; } = null!;

    public DateTime FechaProximaEjecucion { get; set; }

    public int? IntervaloMinutos { get; set; }

    public int? CantidadPublicaciones { get; set; }

    public int PublicacionesRealizadas { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime? UltimaEjecucion { get; set; }

    public DateTime FechaCreacion { get; set; }

    public virtual Publicacione Publicacion { get; set; } = null!;
}
