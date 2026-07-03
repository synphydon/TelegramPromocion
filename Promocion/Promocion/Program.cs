using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Promocion.Configuracion;
using Promocion.Servicios;
using Promocion.dts.Datos;
using Telegram.Bot;

// La configuración se carga desde la carpeta del ejecutable, independientemente
// del directorio de trabajo utilizado por Visual Studio o la terminal.
HostApplicationBuilder constructor = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

constructor.Services.Configure<OpcionesBot>(constructor.Configuration.GetSection(OpcionesBot.Seccion));
constructor.Services.Configure<OpcionesPublicacion>(constructor.Configuration.GetSection(OpcionesPublicacion.Seccion));

string cadenaConexion = constructor.Configuration.GetConnectionString("TelegramBot")
    ?? throw new InvalidOperationException("No se configuró la conexión 'TelegramBot'.");
string tokenBot = constructor.Configuration[$"{OpcionesBot.Seccion}:TokenBot"]
    ?? throw new InvalidOperationException("No se configuró el token del bot.");

constructor.Services.AddPooledDbContextFactory<ContextoTelegram>(opciones => opciones.UseMySQL(cadenaConexion));
constructor.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(tokenBot));
constructor.Services.AddSingleton<ServicioArchivos>();
constructor.Services.AddSingleton<ServicioPublicador>();
constructor.Services.AddSingleton<ServicioConversacion>();
constructor.Services.AddHostedService<ServicioBot>();
constructor.Services.AddHostedService<ServicioProgramaciones>();

await constructor.Build().RunAsync();
