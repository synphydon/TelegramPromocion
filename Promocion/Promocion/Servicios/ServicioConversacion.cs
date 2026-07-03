using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Promocion.Configuracion;
using Promocion.Dominio;
using Promocion.dts.Datos;
using Promocion.dts.Entidades;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Promocion.Servicios;

public sealed class ServicioConversacion
{
    private readonly ITelegramBotClient clienteBot;
    private readonly IDbContextFactory<ContextoTelegram> fabricaContexto;
    private readonly ServicioArchivos servicioArchivos;
    private readonly ServicioPublicador publicador;
    private readonly OpcionesPublicacion opciones;

    public ServicioConversacion(
        ITelegramBotClient clienteBot,
        IDbContextFactory<ContextoTelegram> fabricaContexto,
        ServicioArchivos servicioArchivos,
        ServicioPublicador publicador,
        IOptions<OpcionesPublicacion> opciones)
    {
        this.clienteBot = clienteBot;
        this.fabricaContexto = fabricaContexto;
        this.servicioArchivos = servicioArchivos;
        this.publicador = publicador;
        this.opciones = opciones.Value;
    }

    public async Task ProcesarAsync(Update actualizacion, CancellationToken cancelacion)
    {
        Message? mensaje = actualizacion.Message ?? actualizacion.ChannelPost;
        if (mensaje?.Text?.Trim().Equals("//Canal id", StringComparison.OrdinalIgnoreCase) == true)
        {
            await clienteBot.SendMessage(
                mensaje.Chat.Id,
                $"Chat.Id: {mensaje.Chat.Id}\nChat.Title: {mensaje.Chat.Title ?? "Sin título"}",
                cancellationToken: cancelacion);
            return;
        }

        if (actualizacion.CallbackQuery is not null)
        {
            await ProcesarBotonAsync(actualizacion.CallbackQuery, cancelacion);
            return;
        }

        if (mensaje?.From is null || mensaje.Chat.Type != ChatType.Private)
        {
            return;
        }

        if (mensaje.Text is "Hola" or "hola")
        {
            await clienteBot.SendMessage(mensaje.Chat.Id, "👋 Hola", cancellationToken: cancelacion);
            return;
        }

        if (EsComandoCancelarProgramados(mensaje.Text))
        {
            await using ContextoTelegram contextoProgramados = await fabricaContexto.CreateDbContextAsync(cancelacion);
            SesionesConversacion sesionProgramados = await ObtenerSesionAsync(contextoProgramados, mensaje.From, cancelacion);
            await CancelarProgramadosAsync(contextoProgramados, sesionProgramados.UsuarioId, mensaje.Chat.Id, cancelacion);
            return;
        }

        if (EsComandoCancelar(mensaje.Text))
        {
            await using ContextoTelegram contextoCancelacion = await fabricaContexto.CreateDbContextAsync(cancelacion);
            SesionesConversacion sesionCancelacion = await ObtenerSesionAsync(contextoCancelacion, mensaje.From, cancelacion);
            await CancelarPublicacionAsync(contextoCancelacion, sesionCancelacion, mensaje.Chat.Id, cancelacion);
            return;
        }

        if (EsComandoComenzar(mensaje.Text))
        {
            await using ContextoTelegram contextoComienzo = await fabricaContexto.CreateDbContextAsync(cancelacion);
            SesionesConversacion sesionComienzo = await ObtenerSesionAsync(contextoComienzo, mensaje.From, cancelacion);
            await ComenzarAsync(contextoComienzo, sesionComienzo, mensaje.Chat.Id, cancelacion);
            return;
        }

        await using ContextoTelegram contexto = await fabricaContexto.CreateDbContextAsync(cancelacion);
        SesionesConversacion sesion = await ObtenerSesionAsync(contexto, mensaje.From, cancelacion);

        switch (sesion.Estado)
        {
            case Estados.RecibiendoContenido:
                await IncorporarContenidoAsync(contexto, sesion, mensaje, cancelacion);
                break;
            case Estados.EsperandoFecha:
                await RecibirFechaAsync(contexto, sesion, mensaje, cancelacion);
                break;
            case Estados.EsperandoHora:
                await RecibirHoraAsync(contexto, sesion, mensaje, cancelacion);
                break;
            case Estados.EsperandoIntervalo:
                await RecibirIntervaloAsync(contexto, sesion, mensaje, cancelacion);
                break;
            case Estados.EsperandoCantidad:
                await RecibirCantidadAsync(contexto, sesion, mensaje, cancelacion);
                break;
            default:
                await MostrarInicioAsync(mensaje.Chat.Id, cancelacion);
                break;
        }
    }

    private async Task ProcesarBotonAsync(CallbackQuery consulta, CancellationToken cancelacion)
    {
        await clienteBot.AnswerCallbackQuery(consulta.Id, cancellationToken: cancelacion);
        if (consulta.Message is null || consulta.From.Id == 0)
        {
            return;
        }

        await using ContextoTelegram contexto = await fabricaContexto.CreateDbContextAsync(cancelacion);
        SesionesConversacion sesion = await ObtenerSesionAsync(contexto, consulta.From, cancelacion);

        switch (consulta.Data)
        {
            case "NuevaPublicacion":
                await CrearPublicacionAsync(contexto, sesion, consulta.Message.Chat.Id, cancelacion);
                break;
            case "Continuar":
                await clienteBot.SendMessage(consulta.Message.Chat.Id, "Puede enviar más texto, imágenes o videos.", cancellationToken: cancelacion);
                break;
            case "Finalizar":
                await FinalizarContenidoAsync(contexto, sesion, consulta.Message.Chat.Id, cancelacion);
                break;
            case "PublicarAhora":
                await ProgramarAhoraAsync(contexto, sesion, consulta.Message.Chat.Id, cancelacion);
                break;
            case "ElegirFecha":
                sesion.Estado = Estados.EsperandoFecha;
                await contexto.SaveChangesAsync(cancelacion);
                await PedirFechaAsync(consulta.Message.Chat.Id, cancelacion);
                break;
            case "FechaHoy":
                sesion.DatosTemporales = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria).ToString("yyyy-MM-dd");
                sesion.Estado = Estados.EsperandoHora;
                await contexto.SaveChangesAsync(cancelacion);
                await clienteBot.SendMessage(consulta.Message.Chat.Id, "Indique la hora en formato HH:mm.", cancellationToken: cancelacion);
                break;
            case "CadaMinutos":
                sesion.Estado = Estados.EsperandoIntervalo;
                await contexto.SaveChangesAsync(cancelacion);
                await clienteBot.SendMessage(
                    consulta.Message.Chat.Id,
                    $"Indique cada cuántos minutos debe publicarse. El mínimo es {opciones.IntervaloMinimoMinutos} minutos.",
                    cancellationToken: cancelacion);
                break;
        }
    }

    private async Task<SesionesConversacion> ObtenerSesionAsync(
        ContextoTelegram contexto,
        User usuarioTelegram,
        CancellationToken cancelacion)
    {
        Usuario? usuario = await contexto.Usuarios
            .Include(elemento => elemento.SesionesConversacion)
            .SingleOrDefaultAsync(elemento => elemento.TelegramUsuarioId == usuarioTelegram.Id, cancelacion);
        DateTime ahora = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                TelegramUsuarioId = usuarioTelegram.Id,
                NombreUsuario = usuarioTelegram.Username,
                NombreCompleto = $"{usuarioTelegram.FirstName} {usuarioTelegram.LastName}".Trim(),
                FechaAlta = ahora,
                UltimaInteraccion = ahora,
                SesionesConversacion = new SesionesConversacion { Estado = Estados.Inicio, FechaActualizacion = ahora }
            };
            contexto.Usuarios.Add(usuario);
        }
        else
        {
            usuario.NombreUsuario = usuarioTelegram.Username;
            usuario.NombreCompleto = $"{usuarioTelegram.FirstName} {usuarioTelegram.LastName}".Trim();
            usuario.UltimaInteraccion = ahora;
        }

        await contexto.SaveChangesAsync(cancelacion);
        return usuario.SesionesConversacion!;
    }

    private async Task CrearPublicacionAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        long chatId,
        CancellationToken cancelacion)
    {
        DateTime ahora = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        Publicacione publicacion = new() { UsuarioId = sesion.UsuarioId, Estado = Estados.Borrador, FechaCreacion = ahora };
        contexto.Publicaciones.Add(publicacion);
        await contexto.SaveChangesAsync(cancelacion);
        sesion.PublicacionActualId = publicacion.Id;
        sesion.Estado = Estados.RecibiendoContenido;
        sesion.DatosTemporales = null;
        sesion.FechaActualizacion = ahora;
        await contexto.SaveChangesAsync(cancelacion);

        await clienteBot.SendMessage(
            chatId,
            "Indique qué desea publicar. Puede enviar:\n1. Solo texto.\n2. Texto e imágenes.\n3. Texto, imágenes y videos.\n4. Solo imágenes.\n5. Solo video.\n6. Imágenes y videos.\n\nPuede enviar todo junto o agregar cada elemento por separado.",
            cancellationToken: cancelacion);
    }

    private async Task CancelarPublicacionAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        long chatId,
        CancellationToken cancelacion)
    {
        if (sesion.PublicacionActualId is not null)
        {
            Publicacione? publicacion = await contexto.Publicaciones.FindAsync([sesion.PublicacionActualId], cancelacion);
            if (publicacion is not null)
            {
                publicacion.Estado = Estados.Cancelada;
                publicacion.FechaFinalizacion = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
            }
        }

        sesion.Estado = Estados.Inicio;
        sesion.PublicacionActualId = null;
        sesion.DatosTemporales = null;
        sesion.FechaActualizacion = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        await contexto.SaveChangesAsync(cancelacion);

        await clienteBot.SendMessage(
            chatId,
            "El posteo fue cancelado. Puede generar uno nuevo cuando lo desee.",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Generar nuevo posteo", "NuevaPublicacion")),
            cancellationToken: cancelacion);
    }

    private async Task CancelarProgramadosAsync(
        ContextoTelegram contexto,
        long usuarioId,
        long chatId,
        CancellationToken cancelacion)
    {
        List<Programacione> programaciones = await contexto.Programaciones
            .Where(elemento => elemento.Publicacion.UsuarioId == usuarioId
                && elemento.Tipo == Estados.Recurrente
                && (elemento.Estado == Estados.Pendiente || elemento.Estado == Estados.Activa))
            .ToListAsync(cancelacion);

        foreach (Programacione programacion in programaciones)
        {
            programacion.Estado = Estados.Cancelada;
        }

        await contexto.SaveChangesAsync(cancelacion);
        string respuesta = programaciones.Count == 0
            ? "No tiene posteos recurrentes pendientes para cancelar."
            : $"Se cancelaron {programaciones.Count} {(programaciones.Count == 1 ? "posteo recurrente" : "posteos recurrentes")}. Los posteos con fecha y hora conservaron su programación.";
        await clienteBot.SendMessage(chatId, respuesta, cancellationToken: cancelacion);
    }

    private async Task ComenzarAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        long chatId,
        CancellationToken cancelacion)
    {
        bool posteoEnCurso = sesion.PublicacionActualId is not null || sesion.Estado != Estados.Inicio;
        if (posteoEnCurso)
        {
            await CancelarPublicacionAsync(contexto, sesion, chatId, cancelacion);
            return;
        }

        await clienteBot.SendMessage(
            chatId,
            "Puede generar un nuevo posteo cuando lo desee.",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Generar nuevo posteo", "NuevaPublicacion")),
            cancellationToken: cancelacion);
    }

    private static bool EsComandoCancelar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string comando = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return comando.Equals("/cancelar", StringComparison.OrdinalIgnoreCase)
            || comando.StartsWith("/cancelar@", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsComandoCancelarProgramados(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string comando = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return comando.Equals("/cancelarprogramados", StringComparison.OrdinalIgnoreCase)
            || comando.StartsWith("/cancelarprogramados@", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsComandoComenzar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string comando = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return comando.Equals("/comenzar", StringComparison.OrdinalIgnoreCase)
            || comando.StartsWith("/comenzar@", StringComparison.OrdinalIgnoreCase);
    }

    private async Task IncorporarContenidoAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        Message mensaje,
        CancellationToken cancelacion)
    {
        Publicacione? publicacion = await contexto.Publicaciones
            .Include(elemento => elemento.ArchivosPublicacions)
            .SingleOrDefaultAsync(elemento => elemento.Id == sesion.PublicacionActualId, cancelacion);
        if (publicacion is null)
        {
            sesion.Estado = Estados.Inicio;
            await contexto.SaveChangesAsync(cancelacion);
            await MostrarInicioAsync(mensaje.Chat.Id, cancelacion);
            return;
        }

        string? texto = mensaje.Text ?? mensaje.Caption;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            publicacion.Texto = string.IsNullOrWhiteSpace(publicacion.Texto)
                ? texto.Trim()
                : $"{publicacion.Texto}\n{texto.Trim()}";
        }

        if (mensaje.Photo?.Length > 0)
        {
            PhotoSize foto = mensaje.Photo[^1];
            string ruta = await servicioArchivos.GuardarAsync(foto.FileId, ".jpg", cancelacion);
            AgregarArchivo(publicacion, "Imagen", ruta, null, foto.FileId);
        }

        (string identificador, string? nombre, string extension)? videoRecibido = ObtenerVideo(mensaje);
        if (videoRecibido is not null)
        {
            var video = videoRecibido.Value;
            string ruta = await servicioArchivos.GuardarAsync(video.identificador, video.extension, cancelacion);
            AgregarArchivo(publicacion, "Video", ruta, video.nombre, video.identificador);
        }

        if (string.IsNullOrWhiteSpace(texto) && mensaje.Photo is null && videoRecibido is null)
        {
            await clienteBot.SendMessage(mensaje.Chat.Id, "El contenido debe ser texto, una imagen o un video.", cancellationToken: cancelacion);
            return;
        }

        sesion.FechaActualizacion = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        await contexto.SaveChangesAsync(cancelacion);
        await clienteBot.SendMessage(
            mensaje.Chat.Id,
            "Contenido incorporado correctamente. ¿Desea finalizar el posteo o continuar?",
            replyMarkup: CrearBotonesContenido(),
            cancellationToken: cancelacion);
    }

    private static void AgregarArchivo(Publicacione publicacion, string tipo, string ruta, string? original, string identificador)
    {
        int orden = publicacion.ArchivosPublicacions.Count == 0
            ? 1
            : publicacion.ArchivosPublicacions.Max(elemento => elemento.Orden) + 1;
        publicacion.ArchivosPublicacions.Add(new ArchivosPublicacion
        {
            Tipo = tipo,
            RutaRelativa = ruta,
            NombreOriginal = original,
            TelegramFileId = identificador,
            Orden = orden,
            FechaCreacion = DateTime.Now
        });
    }

    private static (string identificador, string? nombre, string extension)? ObtenerVideo(Message mensaje)
    {
        if (mensaje.Video is not null)
        {
            return (mensaje.Video.FileId, mensaje.Video.FileName, ObtenerExtension(mensaje.Video.FileName));
        }

        if (mensaje.Animation is not null)
        {
            return (mensaje.Animation.FileId, mensaje.Animation.FileName, ObtenerExtension(mensaje.Animation.FileName));
        }

        if (mensaje.VideoNote is not null)
        {
            return (mensaje.VideoNote.FileId, null, ".mp4");
        }

        if (EsDocumentoVideo(mensaje.Document))
        {
            return (mensaje.Document!.FileId, mensaje.Document.FileName, ObtenerExtension(mensaje.Document.FileName));
        }

        return null;
    }

    private static bool EsDocumentoVideo(Document? documento)
    {
        if (documento is null)
        {
            return false;
        }

        if (documento.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        string extension = Path.GetExtension(documento.FileName ?? string.Empty);
        return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".avi", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase);
    }

    private static string ObtenerExtension(string? nombreArchivo)
    {
        string extension = Path.GetExtension(nombreArchivo) ?? string.Empty;
        return string.IsNullOrWhiteSpace(extension) ? ".mp4" : extension;
    }

    private async Task FinalizarContenidoAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        long chatId,
        CancellationToken cancelacion)
    {
        Publicacione? publicacion = await contexto.Publicaciones
            .Include(elemento => elemento.ArchivosPublicacions)
            .SingleOrDefaultAsync(elemento => elemento.Id == sesion.PublicacionActualId, cancelacion);
        if (publicacion is null || (string.IsNullOrWhiteSpace(publicacion.Texto) && publicacion.ArchivosPublicacions.Count == 0))
        {
            await clienteBot.SendMessage(chatId, "Debe agregar al menos un texto, una imagen o un video.", cancellationToken: cancelacion);
            return;
        }

        publicacion.FechaFinalizacion = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        await contexto.SaveChangesAsync(cancelacion);
        await clienteBot.SendMessage(
            chatId,
            "¿Cuándo desea realizar el posteo?",
            replyMarkup: CrearBotonesProgramacion(),
            cancellationToken: cancelacion);
    }

    private async Task ProgramarAhoraAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        long chatId,
        CancellationToken cancelacion)
    {
        if (sesion.PublicacionActualId is null) return;
        long publicacionId = sesion.PublicacionActualId.Value;
        DateTime ahora = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        Programacione programacion = new()
        {
            PublicacionId = publicacionId,
            Tipo = Estados.Inmediata,
            FechaProximaEjecucion = ahora,
            Estado = Estados.Pendiente,
            FechaCreacion = ahora
        };
        contexto.Programaciones.Add(programacion);
        await contexto.SaveChangesAsync(cancelacion);

        bool enviado = await publicador.PublicarAsync(publicacionId, cancelacion);
        programacion.Estado = enviado ? Estados.Completada : Estados.Error;
        programacion.UltimaEjecucion = ahora;
        await CompletarSesionAsync(contexto, sesion, enviado, cancelacion);
        await clienteBot.SendMessage(chatId, enviado ? "✅ El posteo fue publicado correctamente." : "No fue posible publicar. Verifique el identificador del canal y los permisos del bot.", cancellationToken: cancelacion);
    }

    private async Task RecibirFechaAsync(ContextoTelegram contexto, SesionesConversacion sesion, Message mensaje, CancellationToken cancelacion)
    {
        DateTime hoy = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria).Date;
        bool valida = DateTime.TryParseExact(mensaje.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha);
        if (!valida || fecha.Date < hoy)
        {
            await clienteBot.SendMessage(mensaje.Chat.Id, "La fecha no es válida o es anterior a hoy. Use DD/MM/AAAA.", cancellationToken: cancelacion);
            return;
        }

        sesion.DatosTemporales = fecha.ToString("yyyy-MM-dd");
        sesion.Estado = Estados.EsperandoHora;
        await contexto.SaveChangesAsync(cancelacion);
        await clienteBot.SendMessage(mensaje.Chat.Id, "Indique la hora en formato HH:mm.", cancellationToken: cancelacion);
    }

    private async Task RecibirHoraAsync(ContextoTelegram contexto, SesionesConversacion sesion, Message mensaje, CancellationToken cancelacion)
    {
        bool fechaValida = DateTime.TryParseExact(sesion.DatosTemporales, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha);
        bool horaValida = TimeOnly.TryParseExact(mensaje.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly hora);
        DateTime fechaHora = fecha.Date.Add(hora.ToTimeSpan());
        DateTime ahora = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        if (!fechaValida || !horaValida || fechaHora <= ahora)
        {
            await clienteBot.SendMessage(mensaje.Chat.Id, "La fecha y hora deben ser posteriores al momento actual. Indique nuevamente la hora (HH:mm).", cancellationToken: cancelacion);
            return;
        }

        await CrearProgramacionAsync(contexto, sesion, Estados.FechaHora, fechaHora, null, null, cancelacion);
        await clienteBot.SendMessage(mensaje.Chat.Id, $"✅ Posteo programado para el {fechaHora:dd/MM/yyyy} a las {fechaHora:HH:mm}.", cancellationToken: cancelacion);
    }

    private async Task RecibirIntervaloAsync(ContextoTelegram contexto, SesionesConversacion sesion, Message mensaje, CancellationToken cancelacion)
    {
        if (!int.TryParse(mensaje.Text, out int minutos) || minutos < opciones.IntervaloMinimoMinutos)
        {
            await clienteBot.SendMessage(mensaje.Chat.Id, $"Ingrese un número igual o mayor que {opciones.IntervaloMinimoMinutos}.", cancellationToken: cancelacion);
            return;
        }

        sesion.DatosTemporales = minutos.ToString(CultureInfo.InvariantCulture);
        sesion.Estado = Estados.EsperandoCantidad;
        sesion.FechaActualizacion = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        await contexto.SaveChangesAsync(cancelacion);
        await clienteBot.SendMessage(
            mensaje.Chat.Id,
            "Indique cuántas veces desea que se publique el posteo.",
            cancellationToken: cancelacion);
    }

    private async Task RecibirCantidadAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        Message mensaje,
        CancellationToken cancelacion)
    {
        bool intervaloValido = int.TryParse(sesion.DatosTemporales, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutos);
        if (!intervaloValido || !int.TryParse(mensaje.Text, out int cantidad) || cantidad <= 0)
        {
            await clienteBot.SendMessage(
                mensaje.Chat.Id,
                "Ingrese una cantidad de publicaciones mayor que cero.",
                cancellationToken: cancelacion);
            return;
        }

        DateTime primeraEjecucion = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria).AddMinutes(minutos);
        await CrearProgramacionAsync(contexto, sesion, Estados.Recurrente, primeraEjecucion, minutos, cantidad, cancelacion);
        await clienteBot.SendMessage(
            mensaje.Chat.Id,
            $"✅ El posteo se publicará {cantidad} {(cantidad == 1 ? "vez" : "veces")}, cada {minutos} minutos.",
            cancellationToken: cancelacion);
    }

    private async Task CrearProgramacionAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        string tipo,
        DateTime proximaEjecucion,
        int? intervalo,
        int? cantidadPublicaciones,
        CancellationToken cancelacion)
    {
        contexto.Programaciones.Add(new Programacione
        {
            PublicacionId = sesion.PublicacionActualId!.Value,
            Tipo = tipo,
            FechaProximaEjecucion = proximaEjecucion,
            IntervaloMinutos = intervalo,
            CantidadPublicaciones = cantidadPublicaciones,
            PublicacionesRealizadas = 0,
            Estado = Estados.Pendiente,
            FechaCreacion = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria)
        });
        await CompletarSesionAsync(contexto, sesion, true, cancelacion);
    }

    private static async Task CompletarSesionAsync(
        ContextoTelegram contexto,
        SesionesConversacion sesion,
        bool programada,
        CancellationToken cancelacion)
    {
        Publicacione? publicacion = await contexto.Publicaciones.FindAsync([sesion.PublicacionActualId], cancelacion);
        if (publicacion is not null) publicacion.Estado = programada ? Estados.Programada : Estados.Error;
        sesion.Estado = Estados.Inicio;
        sesion.PublicacionActualId = null;
        sesion.DatosTemporales = null;
        await contexto.SaveChangesAsync(cancelacion);
    }

    private async Task MostrarInicioAsync(long chatId, CancellationToken cancelacion)
    {
        await clienteBot.SendMessage(
            chatId,
            "Bienvenido. Seleccione una opción para comenzar.",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Crear posteo", "NuevaPublicacion")),
            cancellationToken: cancelacion);
    }

    private async Task PedirFechaAsync(long chatId, CancellationToken cancelacion)
    {
        DateTime hoy = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
        await clienteBot.SendMessage(
            chatId,
            $"Indique la fecha en formato DD/MM/AAAA. La fecha predeterminada es {hoy:dd/MM/yyyy}.",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData($"Hoy ({hoy:dd/MM/yyyy})", "FechaHoy")),
            cancellationToken: cancelacion);
    }

    private static InlineKeyboardMarkup CrearBotonesContenido() => new(new[]
    {
        InlineKeyboardButton.WithCallbackData("Finalizar posteo", "Finalizar"),
        InlineKeyboardButton.WithCallbackData("Continuar", "Continuar")
    });

    private static InlineKeyboardMarkup CrearBotonesProgramacion() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("Publicar ahora", "PublicarAhora") },
        new[] { InlineKeyboardButton.WithCallbackData("Fecha y hora", "ElegirFecha") },
        new[] { InlineKeyboardButton.WithCallbackData("Cada X minutos", "CadaMinutos") }
    });
}
