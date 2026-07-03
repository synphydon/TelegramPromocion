namespace Promocion.Configuracion;

public sealed class OpcionesPublicacion
{
    public const string Seccion = "Publicaciones";

    public string DirectorioMultimedia { get; set; } = "Multimedia";
    public int IntervaloMinimoMinutos { get; set; } = 10;
    public int IntervaloControlSegundos { get; set; } = 30;
    public string ZonaHoraria { get; set; } = "America/Buenos_Aires";
}
