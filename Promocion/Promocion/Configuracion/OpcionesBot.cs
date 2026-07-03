namespace Promocion.Configuracion;

public sealed class OpcionesBot
{
    public const string Seccion = "BotTelegram";

    public string TokenBot { get; set; } = string.Empty;
    public string IdCanal { get; set; } = string.Empty;
}
