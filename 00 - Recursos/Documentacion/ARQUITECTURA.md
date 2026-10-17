# Arquitectura

## Flujo general

```text
Navegador ── HTTP/SSR ── páginas públicas y autenticación
          └─ SignalR ── componentes Interactive Server del portal
                              ↓
                         servicios de aplicación
                              ↓
                  IDbContextFactory<AppDbContext>
                              ↓
                  Entity Framework Core / SQLite
```

`Domain` define Celebration, EventModule, EventGallery, GalleryImage, GuestResponse y ApplicationUser. `ModuleCatalog` concentra la información de los once tipos de módulos. `Components` renderiza y recoge interacciones. `EventService`, `GalleryService`, `InvitationService` y `AdminService` son la frontera de reglas y acceso. `IImageStorage` permite cambiar el disco local por almacenamiento externo.

## Persistencia

- Usuario 1:N Eventos; evento 1:N Módulos; evento 1:1 Galería; galería 1:N Imágenes; evento 1:N Respuestas.
- Slug único a nivel global y correo único por evento para RSVP.
- Identificadores UUID. Nombres de archivos generados por el servidor.
- El esquema cambia mediante migraciones de EF, no mediante `EnsureCreated`.
- Contexto por operación para evitar reutilizar una instancia de DbContext durante toda la conexión de Blazor.
- Token `Version` para detectar ediciones simultáneas de la configuración, publicación y papelera. Las fotos se guardan independientemente y no reemplazan módulos ni datos básicos.
- Eliminar un evento es baja lógica y lo despublica; restaurarlo lo deja como borrador. Eliminar una imagen es baja física.

## Identidad y autorización

Identity administra hashes de contraseñas y cookies. Registro/login/logout usan solicitudes HTTP normales con antiforgery; no intentan escribir cookies dentro de una conexión SignalR. El portal utiliza un AuthenticationStateProvider que revalida la marca de seguridad de Identity cada 20 minutos.

`ICurrentUser` obtiene el identificador de la sesión del servidor. `EventService`, `GalleryService` y `/media` permiten acceso al creador o a un colaborador del evento, consultando la relación en cada operación. Ocultar botones no es una barrera de seguridad. Las consultas públicas filtran por publicación y baja lógica y nunca incluyen respuestas de invitados ni colaboradores.

`EventCollaborator` relaciona evento y usuario registrado mediante una clave compuesta única y claves foráneas. `CollaboratorService` exige ser el creador para listar, agregar o revocar colaboradores; normaliza el correo con el normalizador de Identity. Agregar concede acceso inmediato, sin correo ni aceptación pendiente. El creador conserva la propiedad y la exclusividad sobre la papelera; los colaboradores pueden editar, publicar y consultar respuestas. Los eventos eliminados no aparecen para colaboradores. Restaurarlos conserva los accesos. Revocar bloquea las siguientes consultas, guardados y operaciones de galería; no borra cambios anteriores ni retira información ya recibida por un navegador. `Version` impide sobrescribir una edición más reciente de otro usuario. No hay sincronización de ediciones en vivo: hay que recargar para ver cambios ajenos.

La cuenta demo no tiene contraseña y se crea únicamente en Development. La ruta de acceso demo se registra únicamente en ese entorno.

## Superadministración

Las páginas `/administracion` usan SSR y la política `SuperAdministration`. `SuperAdminHandler` exige una identidad autenticada y consulta en SQLite el rol `SuperAdmin` y la marca de seguridad del usuario. `AdminService` repite esa autorización en cada operación; `AuthorizeView` solo controla la navegación visible. Retirar el rol invalida las siguientes consultas sin esperar a la renovación de cookies.

`AdminProvisioning` asigna o revoca el rol mediante un comando local sobre cuentas existentes; no hay asignación desde registro o una API. El modo comando sale antes de migrar, sembrar demos o iniciar el servidor. Las tablas de Identity ya existen, por lo que no se agrega una migración.

Las consultas usan proyecciones, recuentos en SQLite, paginación y `AsNoTracking`. No se exponen hashes, tokens ni marcas de seguridad. Los correos y mensajes de invitados están disponibles únicamente dentro del detalle autorizado. Las páginas y `/administracion/media/{id}` envían `Cache-Control: private, no-store, no-transform` y `X-Robots-Tag: noindex, nofollow`. Esa ruta de imágenes valida el permiso incluso cuando se abre directamente y no cambia los criterios de la ruta pública `/media`.

El panel es de consulta. Ser superadministrador no concede edición en los servicios normales: se requiere ser creador o colaborador del evento.

## Imágenes

`InputFile` usa un input nativo superpuesto a la zona de arrastre. El botón de cámara es otro InputFile con `accept="image/*"` y `capture="environment"`. El navegador decide la experiencia de cámara/selector según el dispositivo.

`LocalImageStorage` valida peso y decodifica con SkiaSharp, permite JPEG/PNG/WebP, limita dimensiones, aplica orientación EXIF, reduce el lado máximo a 2400 px y reencodea WebP. El archivo nuevo no contiene EXIF/GPS. SVG y HEIC no se aceptan.

Los archivos están fuera de wwwroot y se entregan por `/media/{id}` solamente al dueño o cuando el evento está publicado. La respuesta no permite cache persistente. El tamaño bruto máximo es 10 MB por imagen. El procesamiento es síncrono dentro de la operación de carga; para alto volumen debe pasar a un servicio/cola con cuotas de memoria y concurrencia.

## Invitaciones

`InvitationView` renderiza los módulos según su orden y activación. La cuenta regresiva tiene su propio componente interactivo y libera el temporizador al desmontarse. Los demás contenidos públicos usan SSR. RSVP es un POST con antiforgery, límites por IP y validación del servidor. Los enlaces configurables solo aceptan HTTPS, sin esquemas ejecutables. Razor escapa el contenido del usuario; los únicos MarkupString son trazos SVG fijos del componente Icon.

El calendario serializa la fecha UTC y escapa/splitea líneas de ICS para evitar introducir eventos mediante el contenido.

## Ubicaciones y Google Maps

`LocationEditor` encapsula la selección interactiva del lugar y notifica al editor para actualizar su estado de cambios pendientes. `google-maps.js` carga Places una sola vez, monta el widget oficial `PlaceAutocompleteElement` al expandir el módulo y libera sus listeners con un MutationObserver cuando Blazor lo retira. Una selección lenta anterior no reemplaza otra más reciente. Los errores permiten continuar con la dirección manual.

Cada módulo Location guarda su propio `GooglePlaceId` y una `LocationAddress` ingresada manualmente. El texto formateado que devuelve Google se muestra transitoriamente; no se copia a la base. Seleccionar un lugar limpia la dirección manual y tiene precedencia sobre un LinkUrl antiguo. `EventService` valida longitud/formato y persiste los campos únicamente para Location, conservando la autorización y concurrencia existentes. La migración AddGoogleMapsLocations agrega ambas columnas vacías, sin modificar contenido previo.

`GoogleMaps` construye URLs de destinos fijos de Google con parámetros escapados. `LocationMap` reutiliza el iframe oficial de Embed API en editor, invitación SSR y consulta administrativa. No se admite HTML de iframe ni se resuelven enlaces arbitrarios desde el servidor. Los mapas públicos son lazy y no cargan Places JavaScript. Los módulos ocultos no renderizan mapas; los enlaces anteriores siguen funcionando mientras no se seleccione una ubicación nueva.

`GoogleMaps:BrowserApiKey` se configura fuera de Git. Sin clave no se carga Google: permanece la dirección manual y el enlace externo de indicaciones (Maps URLs no necesita clave). La clave de navegador es visible por diseño y debe restringirse a los dominios y las tres APIs utilizadas. Operación detalla la activación en Google Cloud, la migración y la configuración de IIS.

## Límites deliberados

- Una base SQLite y un servidor son adecuados para esta etapa. No se afirma capacidad multiinstancia.
- Módulos con campos comunes; los bloques especiales (galería, cuenta regresiva, RSVP) tienen representación propia.
- No hay publicación por versiones: editar y guardar un evento publicado actualiza su página inmediatamente.
- RSVP abierto no equivale a identidad verificada; la dirección de correo sirve para evitar duplicados, no para autorizar modificaciones.
- No hay proveedor de correo configurado ni recuperación de cuenta. Es un pendiente previo a producción.
