# Operación local e IIS

## Desarrollo

`dotnet run --project "01 - Code/SaveTheDate" --launch-profile http` inicia en http://localhost:5105. `dotnet watch --project "01 - Code/SaveTheDate" run --launch-profile http` habilita recarga durante desarrollo.

El perfil HTTPS está disponible con `--launch-profile https` y requiere un certificado de desarrollo confiable. No exponer el perfil Development a Internet: habilita datos y acceso demo.

## Configuración

| Opción | Uso |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Development habilita migraciones y demo. Usar Production en despliegues reales. |
| `ConnectionStrings__DefaultConnection` | Cadena SQLite; por defecto `App_Data/savethedate.db` dentro del proyecto. |
| `Database__ApplyMigrations` | False fuera de Development. Activar explícitamente solo si se decide migrar al iniciar. |
| `ASPNETCORE_URLS` | Dirección/puerto del servidor; no sustituye la configuración de TLS/proxy. |

## Datos

- Respaldar base SQLite y uploads de forma consistente. Para el procedimiento local sencillo, detener la aplicación y copiar `App_Data` completa.
- No subir `App_Data` al repositorio. Tampoco compartir cookies, contraseñas o claves de Data Protection.
- La papelera conserva datos y fotos. Su purga y retención se definirán para producción.
- En producción conservar también las claves de Data Protection para que cookies/tokens sobrevivan a reinicios.

## Publicación

1. Completar los pendientes de identidad, privacidad y cuotas del plan.
2. Publicar: `dotnet publish "01 - Code/SaveTheDate" -c Release -o .artifacts/publish`.
3. Elegir hosting compatible con ASP.NET Core y conexiones WebSocket persistentes.
4. Configurar almacenamiento persistente, connection string, HTTPS, claves de Data Protection, permisos mínimos y host permitido.
5. Ejecutar migraciones de forma controlada con backup y rollback. No copiar la base demo a producción.
6. Configurar forwarded headers solo para proxies de confianza; revisar dirección real del cliente para rate limiting.
7. Probar registro, cookies, publicación, uploads y RSVP en el dominio final, además de restaurar un backup.

Los cambios de código se verifican localmente. La publicación en el servidor de IIS es un paso independiente.


## Superadministración en IIS

El portal `/administracion` permite consultar usuarios, eventos activos y papelera, módulos, galerías y respuestas de asistencia. También muestra métricas y uso del almacenamiento. Es un panel de consulta; no elimina usuarios ni cambia eventos de terceros. Los borradores, las fotos no publicadas y los datos de invitados siguen siendo privados.

1. Publicar la nueva versión conservando la base y los uploads de `App_Data`. Respaldar antes de actualizar. No publicar la base demo.
2. Abrir PowerShell en la carpeta **de salida publicada que usa IIS**, donde están `SaveTheDate.dll`, `appsettings.json` y `App_Data`. Utilizar una identidad de Windows con permisos de lectura/escritura sobre esa base.
3. Si IIS define `ConnectionStrings__DefaultConnection` en `web.config` o en su propia configuración, copiar esa misma configuración a la sesión de PowerShell. La consola no hereda las variables exclusivas de IIS. Por ejemplo, adaptando la ruta real:

   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Data Source=C:\ruta-real\App_Data\savethedate.db"
   ```

   Con la ubicación predeterminada y ejecutando desde la carpeta publicada no hace falta esta variable.

4. Dar el permiso a una cuenta **ya registrada en esa web**:

   ```powershell
   dotnet .\SaveTheDate.dll --grant-super-admin "tu-correo@ejemplo.com"
   ```

5. Abrir `/administracion` con esa cuenta. Aparece también el acceso «Administración» dentro del espacio personal. Una sesión anterior al permiso sirve al recargar; no hace falta cambiar la contraseña.

El comando termina después de actualizar el rol; no inicia otro servidor web. No necesita el SDK si ya está instalado el runtime de .NET que utiliza la aplicación. Con una publicación autocontenida también se puede usar `SaveTheDate.exe` con los mismos argumentos. No se debe ejecutarlo desde la copia de desarrollo si se pretende habilitar la cuenta de producción: son bases distintas.

Esta actualización no cambia el esquema. Usa las tablas de roles que ya existen en la migración inicial de Identity; no requiere activar `Database:ApplyMigrations` nuevamente.

Para retirar el acceso:

```powershell
dotnet .\SaveTheDate.dll --revoke-super-admin "tu-correo@ejemplo.com"
```

La política consulta el rol y la marca de seguridad actuales de la base en cada autorización. La revocación bloquea las siguientes solicitudes aun con cookies anteriores; no borra información ya vista o descargada. No existe un endpoint público de asignación ni un administrador automático. El comando rechaza la cuenta demo, y los registros comunes no pueden solicitar roles.

### Interpretar el dashboard

- Eventos activos excluye papelera. Publicados y borradores son subconjuntos de los activos.
- Asistentes confirmados suma el tamaño de los grupos que respondieron que sí en eventos activos; no es el número de formularios.
- La evolución mensual y las nuevas historias de los últimos 30 días cuentan todas las altas, incluso eventos luego enviados a papelera. Los meses se calculan en hora argentina.
- El total de fotos incluye papelera. Almacenamiento recorre los archivos referenciados por las galerías e informa referencias sin archivo; no busca archivos huérfanos ni mide la capacidad total del disco.
- El tamaño SQLite incluye los archivos WAL/SHM si existen. El cálculo es informativo y puede variar durante la actividad.
- Usuarios y eventos se paginan de a 20, igual que las respuestas de cada evento. El panel no inventa fecha de registro ni última conexión porque la base actual no guarda esos datos.
- No hay backups automáticos ni verificación de restauración desde este panel. Detener la aplicación y respaldar SQLite, uploads y las claves persistentes de Data Protection con el procedimiento de la instalación.


### Correos visibles detrás de Cloudflare

Las respuestas de `/administracion` envían `Cache-Control: private, no-store, no-transform`. Cloudflare respeta `no-transform` y no aplica Email Address Obfuscation a esas respuestas. Así los correos siguen visibles y seleccionables al navegar entre páginas con Blazor, sin depender de que se vuelva a ejecutar el script de decodificación del proxy. La cabecera se establece al enviar la respuesta para que antiforgery no la sobrescriba.

Si una versión anterior muestra `[email protected]` o abre `/cdn-cgi/l/email-protection`, publicar la versión corregida y recargar completamente el panel. No requiere cambiar roles, migrar SQLite ni desactivar globalmente la función de Cloudflare. El acceso de superadmin y `private, no-store` continúan siendo obligatorios.

Referencia: [Cloudflare · Email Address Obfuscation](https://developers.cloudflare.com/waf/tools/scrape-shield/email-address-obfuscation/#troubleshoot-email-obfuscation).
