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
| `GoogleMaps__BrowserApiKey` | Clave de navegador restringida por dominio para Maps JavaScript API, Places API (New) y Maps Embed API. Vacía: dirección y enlaces manuales, sin buscador ni mapa incrustado. |
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

La incorporación del superadmin no cambió el esquema: usa las tablas de roles de la migración inicial de Identity. La integración posterior de Google Maps sí requiere la migración indicada debajo.

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

## Actualizar para compartir eventos

La migración `20261016124750_AddEventCollaborators` crea la tabla de colaboradores sin alterar propietarios, eventos ni fotos existentes. Antes de publicar, detener el Application Pool y respaldar `App_Data`; conservar la base, los uploads y la configuración del servidor durante la publicación. Publicar la aplicación, arrancar con `Database__ApplyMigrations=true`, comprobar el inicio correcto y volver esa opción a `false`. No ejecutar la aplicación anterior durante la actualización.

Para usarlo: abrir un evento propio → **Colaboradores** → ingresar el correo de una cuenta registrada → **Agregar colaborador**. Esa persona verá el evento al recargar **Mis eventos**, con la marca «Compartido con vos». No se envía un correo ni se necesita aceptar una invitación. El creador puede quitar el acceso desde la misma sección. Ambos pueden editar y publicar; únicamente el creador administra accesos y la papelera. Si guardan versiones distintas del mismo evento, la segunda persona recibirá un aviso para recargar antes de guardar.

## Activar Google Maps

Cada módulo «Lugar y horario» permite buscar y seleccionar un lugar, mostrarlo en un mapa y abrir indicaciones en Google Maps. Se pueden agregar módulos independientes para ceremonia y fiesta. Los enlaces anteriores siguen funcionando. Sin clave, se puede ingresar la dirección y usar «Cómo llegar», pero el buscador y los mapas quedan pendientes de activación.

### 1. Preparar Google Cloud

1. Entrar a [Google Cloud Console](https://console.cloud.google.com/) y crear o elegir el proyecto SaveTheDate.
2. Asociar una cuenta de facturación para el uso en producción. Google puede cobrar las consultas según el uso; revisar sus tarifas antes de habilitarlo.
3. En **APIs y servicios → Biblioteca**, habilitar **Maps JavaScript API**, **Places API (New)** y **Maps Embed API**. No hacen falta Geocoding API, Routes API ni Places API antigua para esta integración.
4. En **APIs y servicios → Credenciales → Crear credenciales → Clave de API**, crear una clave exclusiva de SaveTheDate.
5. Editar la clave. En **Restricciones de aplicaciones**, elegir **Sitios web** y agregar ambos referentes:

   ```text
   https://savethedate.ar/*
   https://www.savethedate.ar/*
   ```

6. En **Restricciones de API**, seleccionar únicamente las tres APIs del paso 3. Guardar y esperar la propagación de la configuración.
7. Configurar cuotas y alertas de presupuesto acordes al uso esperado. Las alertas de presupuesto avisan; no detienen automáticamente el gasto.

Para probar en la Mac, usar otra clave de desarrollo restringida a `http://localhost:5105/*` y `http://127.0.0.1:5105/*`. No habilitar localhost en la clave de producción ni quitar las restricciones para solucionar errores.

Referencias: [configuración de Maps JavaScript API](https://developers.google.com/maps/documentation/javascript/get-api-key), [Places Autocomplete](https://developers.google.com/maps/documentation/javascript/place-autocomplete-new), [seguridad y restricciones](https://developers.google.com/maps/api-security-best-practices), [Maps Embed API](https://developers.google.com/maps/documentation/embed/get-api-key), [presupuestos de Cloud Billing](https://cloud.google.com/billing/docs/how-to/budgets).

### 2. Configurar IIS y actualizar la base

1. Detener el sitio y su Application Pool; respaldar `App_Data` y conservarlo durante la publicación.
2. Publicar la nueva versión desde Visual Studio. No reemplazar SQLite ni los uploads con los archivos locales.
3. En el **appsettings.json de la carpeta publicada del servidor**, completar estas secciones, conservando las demás:

   ```json
   "GoogleMaps": {
     "BrowserApiKey": "PEGAR_LA_CLAVE_RESTRINGIDA_AQUI"
   },
   "Database": {
     "ApplyMigrations": true
   }
   ```

   Es un fragmento: no reemplazar todo el archivo por él. No guardar la clave en el appsettings del repositorio ni compartir el archivo con credenciales. Alternativamente, definir `GoogleMaps__BrowserApiKey` y `Database__ApplyMigrations=true` como variables de entorno de la aplicación IIS. Si ya existen esas variables, prevalecen sobre JSON.

4. Iniciar el Application Pool y abrir el sitio. La migración `20261013045629_AddGoogleMapsLocations` agrega `GooglePlaceId` y `LocationAddress` con valores vacíos a los módulos existentes, sin borrar sus textos o enlaces. Verificar que arranque y que el editor abra correctamente.
5. Volver `Database:ApplyMigrations` a `false` (o la variable equivalente) y reiniciar el Application Pool. Preservar la configuración de Maps en publicaciones futuras.

La clave se utiliza en el navegador y puede verse allí: es el mecanismo normal de Google Maps JavaScript/Embed. La protección real son las restricciones de dominio/API y las cuotas; no debe ser una clave de servidor ni quedar sin restricciones. El repositorio contiene solo un valor vacío.

### 3. Comprobar el recorrido

1. Abrir un evento → **Diseño y módulos → Lugar y horario**.
2. Buscar un salón, iglesia o dirección. **Elegir una sugerencia**: escribir sin seleccionarla no cambia el lugar guardado.
3. Revisar el punto en el mapa. Escribir en Texto el nombre que se mostrará en la invitación, y en Detalles las indicaciones adicionales. La descripción devuelta por Google se muestra durante la selección; solo se persiste su identificador de lugar, no se guarda una copia del catálogo de Google.
4. Guardar cambios, volver a abrir el módulo y abrir Vista previa. Comprobar el mapa y **Cómo llegar**, también desde un celular.
5. Agregar otro módulo para una segunda ubicación si hace falta. Ocultar un módulo debe ocultar también su mapa y botón públicos.

Si Google rechaza una búsqueda, revisar facturación, las tres APIs y los referentes exactos (www y dominio sin www). Si se eligió un lugar y se quiere ingresar una dirección manual, usar **Quitar lugar**. Para retirar también una dirección manual, vaciarla; entonces vuelve a utilizarse el enlace opcional anterior, si lo había.

Los mapas cargan contenido externo de Google; el widget conserva su atribución y el iframe sus controles. Antes de activar el servicio en producción, incorporar el uso de Google Maps a los términos y la política de privacidad públicos del sitio según las [políticas de Maps JavaScript API](https://developers.google.com/maps/documentation/javascript/policies). La clave real, la facturación y la comprobación con Google quedan a cargo de la instalación; las pruebas locales no se presentan como verificación de una cuenta de Google Cloud.
