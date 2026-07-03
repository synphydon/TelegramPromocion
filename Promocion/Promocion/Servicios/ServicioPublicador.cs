using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Promocion.Configuracion;
using Promocion.Dominio;
using Promocion.dts.Datos;
using Promocion.dts.Entidades;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Promocion.Servicios;

public sealed class ServicioPublicador
{
    private readonly ITelegramBotClient clienteBot;
    private readonly IDbContextFactory<ContextoTelegram> fabricaContexto;
    private readonly ServicioArchivos servicioArchivos;
    private readonly OpcionesBot opcionesBot;

    public ServicioPublicador(
        ITelegramBotClient clienteBot,
        IDbContextFactory<ContextoTelegram> fabricaContexto,
        ServicioArchivos servicioArchivos,
        IOptions<OpcionesBot> opcionesBot)
    {
        this.clienteBot = clienteBot;
        this.fabricaContexto = fabricaContexto;
        this.servicioArchivos = servicioArchivos;
        this.opcionesBot = opcionesBot.Value;
    }

    public async Task<bool> PublicarAsync(long publicacionId, CancellationToken cancelacion)
    {
        await using ContextoTelegram contexto = await fabricaContexto.CreateDbContextAsync(cancelacion);
        Publicacione? publicacion = await contexto.Publicaciones
            .Include(elemento => elemento.ArchivosPublicacions)
            .SingleOrDefaultAsync(elemento => elemento.Id == publicacionId, cancelacion);

        if (publicacion is null || string.IsNullOrWhiteSpace(opcionesBot.IdCanal))
        {
            return false;
        }

        EnviosPublicacion envio = new() { PublicacionId = publicacionId, FechaIntento = DateTime.Now };
        try
        {
            List<ArchivosPublicacion> archivos = publicacion.ArchivosPublicacions.OrderBy(elemento => elemento.Orden).ToList();
            Message[] mensajes = await EnviarContenidoAsync(publicacion.Texto, archivos, cancelacion);
            envio.Exitoso = 1;
            envio.TelegramMensajeId = mensajes.FirstOrDefault()?.Id;
        }
        catch (Exception excepcion)
        {
            envio.Exitoso = 0;
            envio.MensajeError = excepcion.Message;
        }

        contexto.EnviosPublicacions.Add(envio);
        await contexto.SaveChangesAsync(cancelacion);
        return envio.Exitoso == 1;
    }

    private async Task<Message[]> EnviarContenidoAsync(
        string? texto,
        IReadOnlyList<ArchivosPublicacion> archivos,
        CancellationToken cancelacion)
    {
        if (archivos.Count == 0)
        {
            Message mensaje = await clienteBot.SendMessage(opcionesBot.IdCanal, texto!, cancellationToken: cancelacion);
            return [mensaje];
        }

        string? leyenda = texto?.Length <= 1024 ? texto : null;
        Message[] mensajes;
        if (archivos.Count == 1)
        {
            ArchivosPublicacion archivo = archivos[0];
            Message mensaje;
            if (archivo.Tipo == "Imagen")
            {
                InputFile entrada = InputFile.FromFileId(archivo.TelegramFileId!);
                mensaje = await clienteBot.SendPhoto(opcionesBot.IdCanal, entrada, leyenda, cancellationToken: cancelacion);
            }
            else
            {
                await using FileStream video = File.OpenRead(servicioArchivos.ObtenerRutaCompleta(archivo.RutaRelativa));
                InputFile entrada = InputFile.FromStream(video, Path.GetFileName(archivo.RutaRelativa));
                mensaje = await clienteBot.SendVideo(opcionesBot.IdCanal, entrada, caption: leyenda, cancellationToken: cancelacion);
            }
            mensajes = [mensaje];
        }
        else
        {
            List<IAlbumInputMedia> medios = [];
            List<FileStream> videosAbiertos = [];
            try
            {
            for (int indice = 0; indice < archivos.Count; indice++)
            {
                ArchivosPublicacion archivo = archivos[indice];
                string? leyendaElemento = indice == 0 ? leyenda : null;
                if (archivo.Tipo == "Imagen")
                {
                    InputFile entrada = InputFile.FromFileId(archivo.TelegramFileId!);
                    medios.Add(new InputMediaPhoto(entrada) { Caption = leyendaElemento });
                }
                else
                {
                    FileStream video = File.OpenRead(servicioArchivos.ObtenerRutaCompleta(archivo.RutaRelativa));
                    videosAbiertos.Add(video);
                    InputFile entrada = InputFile.FromStream(video, Path.GetFileName(archivo.RutaRelativa));
                    medios.Add(new InputMediaVideo(entrada) { Caption = leyendaElemento });
                }
            }

            mensajes = await clienteBot.SendMediaGroup(opcionesBot.IdCanal, medios, cancellationToken: cancelacion);
            }
            finally
            {
                foreach (FileStream video in videosAbiertos)
                {
                    await video.DisposeAsync();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(texto) && leyenda is null)
        {
            Message mensajeTexto = await clienteBot.SendMessage(opcionesBot.IdCanal, texto, cancellationToken: cancelacion);
            return [.. mensajes, mensajeTexto];
        }

        return mensajes;
    }
}
