using System;
using System.Collections.Generic;

namespace Promocion.dts.Entidades;

public partial class Usuario
{
    public long Id { get; set; }

    public long TelegramUsuarioId { get; set; }

    public string? NombreUsuario { get; set; }

    public string? NombreCompleto { get; set; }

    public DateTime FechaAlta { get; set; }

    public DateTime UltimaInteraccion { get; set; }

    public virtual ICollection<Publicacione> Publicaciones { get; set; } = new List<Publicacione>();

    public virtual SesionesConversacion? SesionesConversacion { get; set; }
}
