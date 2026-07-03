using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Promocion.Configuracion;
using Promocion.Dominio;
using Promocion.dts.Datos;
using Promocion.dts.Entidades;

namespace Promocion.Servicios;

public sealed class ServicioProgramaciones : BackgroundService
{
    private readonly IDbContextFactory<ContextoTelegram> fabricaContexto;
    private readonly ServicioPublicador publicador;
    private readonly OpcionesPublicacion opciones;
    private readonly ILogger<ServicioProgramaciones> registro;

    public ServicioProgramaciones(
        IDbContextFactory<ContextoTelegram> fabricaContexto,
        ServicioPublicador publicador,
        IOptions<OpcionesPublicacion> opciones,
        ILogger<ServicioProgramaciones> registro)
    {
        this.fabricaContexto = fabricaContexto;
        this.publicador = publicador;
        this.opciones = opciones.Value;
        this.registro = registro;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        using PeriodicTimer temporizador = new(TimeSpan.FromSeconds(Math.Max(1, opciones.IntervaloControlSegundos)));
        do
        {
            await ProcesarPendientesAsync(cancelacion);
        }
        while (await temporizador.WaitForNextTickAsync(cancelacion));
    }

    private async Task ProcesarPendientesAsync(CancellationToken cancelacion)
    {
        try
        {
            DateTime ahora = ServicioFecha.ObtenerAhoraLocal(opciones.ZonaHoraria);
            await using ContextoTelegram contexto = await fabricaContexto.CreateDbContextAsync(cancelacion);
            List<Programacione> pendientes = await contexto.Programaciones
                .Where(elemento => (elemento.Estado == Estados.Pendiente || elemento.Estado == Estados.Activa)
                    && elemento.FechaProximaEjecucion <= ahora)
                .ToListAsync(cancelacion);

            foreach (Programacione programacion in pendientes)
            {
                bool enviado = await publicador.PublicarAsync(programacion.PublicacionId, cancelacion);
                programacion.UltimaEjecucion = ahora;

                if (enviado && programacion.Tipo == Estados.Recurrente)
                {
                    programacion.Estado = Estados.Activa;
                    programacion.FechaProximaEjecucion = ahora.AddMinutes(programacion.IntervaloMinutos!.Value);
                }
                else
                {
                    programacion.Estado = enviado ? Estados.Completada : Estados.Error;
                }
            }

            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (Exception excepcion)
        {
            registro.LogError(excepcion, "No fue posible procesar las publicaciones pendientes.");
        }
    }
}
