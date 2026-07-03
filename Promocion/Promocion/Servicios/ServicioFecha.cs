namespace Promocion.Servicios;

public static class ServicioFecha
{
    public static TimeZoneInfo ObtenerZonaHoraria(string zonaConfigurada)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zonaConfigurada);
        }
        catch (TimeZoneNotFoundException)
        {
            string alternativa = OperatingSystem.IsWindows() ? "Argentina Standard Time" : "America/Buenos_Aires";
            return TimeZoneInfo.FindSystemTimeZoneById(alternativa);
        }
    }

    public static DateTime ObtenerAhoraLocal(string zonaConfigurada) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ObtenerZonaHoraria(zonaConfigurada));
}
