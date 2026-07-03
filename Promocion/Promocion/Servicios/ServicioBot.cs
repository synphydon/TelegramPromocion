using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Promocion.Servicios;

public sealed class ServicioBot : BackgroundService
{
    private readonly ITelegramBotClient clienteBot;
    private readonly ServicioConversacion conversacion;
    private readonly ILogger<ServicioBot> registro;

    public ServicioBot(ITelegramBotClient clienteBot, ServicioConversacion conversacion, ILogger<ServicioBot> registro)
    {
        this.clienteBot = clienteBot;
        this.conversacion = conversacion;
        this.registro = registro;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        int desplazamiento = 0;
        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                Update[] actualizaciones = await clienteBot.GetUpdates(
                    offset: desplazamiento,
                    timeout: 30,
                    cancellationToken: cancelacion);

                foreach (Update actualizacion in actualizaciones)
                {
                    desplazamiento = actualizacion.Id + 1;
                    await conversacion.ProcesarAsync(actualizacion, cancelacion);
                }

            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excepcion)
            {
                registro.LogError(excepcion, "Ocurrió un error al recibir mensajes de Telegram.");
                await Task.Delay(TimeSpan.FromSeconds(5), cancelacion);
            }
        }
    }
}
