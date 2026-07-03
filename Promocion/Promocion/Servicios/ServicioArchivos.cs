using Microsoft.Extensions.Options;
using Promocion.Configuracion;
using Telegram.Bot;

namespace Promocion.Servicios;

public sealed class ServicioArchivos
{
    private readonly ITelegramBotClient clienteBot;
    private readonly string directorioBase;

    public ServicioArchivos(ITelegramBotClient clienteBot, IOptions<OpcionesPublicacion> opciones)
    {
        this.clienteBot = clienteBot;
        directorioBase = Path.IsPathRooted(opciones.Value.DirectorioMultimedia)
            ? opciones.Value.DirectorioMultimedia
            : Path.Combine(AppContext.BaseDirectory, opciones.Value.DirectorioMultimedia);
        directorioBase = Path.GetFullPath(directorioBase);
        Directory.CreateDirectory(directorioBase);
    }

    public async Task<string> GuardarAsync(string identificadorTelegram, string extension, CancellationToken cancelacion)
    {
        var archivoTelegram = await clienteBot.GetFile(identificadorTelegram, cancelacion);
        string nombreUnico = $"{DateTime.Now:yyyyMMdd_HHmmss}_{DateTime.Now.Ticks % TimeSpan.TicksPerSecond:D7}_{Guid.NewGuid():N}{extension}";
        string rutaCompleta = Path.Combine(directorioBase, nombreUnico);

        await using FileStream destino = File.Create(rutaCompleta);
        await clienteBot.DownloadFile(archivoTelegram.FilePath!, destino, cancelacion);
        return Path.GetRelativePath(AppContext.BaseDirectory, rutaCompleta);
    }

    public string ObtenerRutaCompleta(string rutaRelativa) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rutaRelativa));
}
