# Regenerar las entidades con Entity Framework

El proyecto usa el enfoque **database-first**. La base se crea con `Promocion/BaseDatos/CrearBaseDatos.sql` y las entidades se generan dentro de `Promocion.dts`.

## Requisitos

- MySQL Server disponible en `localhost`.
- .NET SDK 10.
- Herramienta global `dotnet-ef`.
- Paquetes `MySql.EntityFrameworkCore` y `Microsoft.EntityFrameworkCore.Design`.

Si `dotnet-ef` no está instalado:

```powershell
dotnet tool install --global dotnet-ef
```

## Comando de scaffold

Ejecutar desde la carpeta raíz de la solución:

```powershell
dotnet ef dbcontext scaffold "server=localhost;database=telegram_bot;user=root;password=SU_CLAVE;SslMode=Disabled" MySQL.EntityFrameworkCore `
  --project Promocion.dts\Promocion.dts.csproj `
  --output-dir Entidades `
  --context-dir Datos `
  --context ContextoTelegram `
  --no-onconfiguring `
  --force
```

`--no-onconfiguring` evita escribir la contraseña en el código generado. `--force` reemplaza las entidades existentes, por lo que cualquier comportamiento propio debe residir fuera de los archivos generados o en clases parciales separadas.
