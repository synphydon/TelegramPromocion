# Bot de promociones para Telegram

Aplicación .NET 10 que recibe publicaciones por chat privado, guarda texto y multimedia, y las publica en un canal de inmediato, en una fecha y hora o con una frecuencia periódica.

## Configuración

Editar `Promocion/appsettings.json`:

- `ConnectionStrings:TelegramBot`: conexión MySQL.
- `BotTelegram:TokenBot`: token entregado por BotFather.
- `BotTelegram:IdCanal`: identificador numérico del canal, por ejemplo `-1001234567890`.
- `Publicaciones:DirectorioMultimedia`: directorio de imágenes y videos.
- `Publicaciones:IntervaloMinimoMinutos`: mínimo permitido para una recurrencia.
- `Publicaciones:IntervaloControlSegundos`: frecuencia de consulta de trabajos pendientes.

El identificador del canal se obtiene agregando el bot como administrador y publicando `//Canal id` dentro del canal.

## Ejecución

```powershell
dotnet run --project Promocion\Promocion.csproj
```

El bot debe ser administrador del canal y tener permiso para publicar mensajes.

Para producción conviene reemplazar secretos mediante variables de entorno, por ejemplo `BotTelegram__TokenBot`, `BotTelegram__IdCanal` y `ConnectionStrings__TelegramBot`.
