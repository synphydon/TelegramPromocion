using System;
using System.Collections.Generic;

namespace Promocion.dts.Entidades;

public partial class SesionesConversacion
{
    public long Id { get; set; }

    public long UsuarioId { get; set; }

    public long? PublicacionActualId { get; set; }

    public string Estado { get; set; } = null!;

    public string? DatosTemporales { get; set; }

    public DateTime FechaActualizacion { get; set; }

    public virtual Publicacione? PublicacionActual { get; set; }

    public virtual Usuario Usuario { get; set; } = null!;
}
