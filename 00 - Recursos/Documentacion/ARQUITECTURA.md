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

`Domain` define Celebration, EventModule, EventGallery, GalleryImage, GuestResponse y ApplicationUser. `ModuleCatalog` concentra la información de los once tipos de módulos. `Components` renderiza y recoge interacciones. `EventService`, `GalleryService` e `InvitationService` son la frontera de reglas y acceso. `IImageStorage` permite cambiar el disco local por almacenamiento externo.

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

`ICurrentUser` obtiene el identificador de la sesión del servidor. Los servicios filtran por OwnerId tanto al leer como al modificar; ocultar botones no es una barrera de seguridad. Las consultas públicas filtran por publicación y baja lógica y nunca incluyen respuestas de invitados.

La cuenta demo no tiene contraseña y se crea únicamente en Development. La ruta de acceso demo se registra únicamente en ese entorno.

## Imágenes

`InputFile` usa un input nativo superpuesto a la zona de arrastre. El botón de cámara es otro InputFile con `accept="image/*"` y `capture="environment"`. El navegador decide la experiencia de cámara/selector según el dispositivo.

`LocalImageStorage` valida peso y decodifica con SkiaSharp, permite JPEG/PNG/WebP, limita dimensiones, aplica orientación EXIF, reduce el lado máximo a 2400 px y reencodea WebP. El archivo nuevo no contiene EXIF/GPS. SVG y HEIC no se aceptan.

Los archivos están fuera de wwwroot y se entregan por `/media/{id}` solamente al dueño o cuando el evento está publicado. La respuesta no permite cache persistente. El tamaño bruto máximo es 10 MB por imagen. El procesamiento es síncrono dentro de la operación de carga; para alto volumen debe pasar a un servicio/cola con cuotas de memoria y concurrencia.

## Invitaciones

`InvitationView` renderiza los módulos según su orden y activación. La cuenta regresiva tiene su propio componente interactivo y libera el temporizador al desmontarse. Los demás contenidos públicos usan SSR. RSVP es un POST con antiforgery, límites por IP y validación del servidor. Los enlaces configurables solo aceptan HTTPS, sin esquemas ejecutables. Razor escapa el contenido del usuario; los únicos MarkupString son trazos SVG fijos del componente Icon.

El calendario serializa la fecha UTC y escapa/splitea líneas de ICS para evitar introducir eventos mediante el contenido.

## Límites deliberados

- Una base SQLite y un servidor son adecuados para esta etapa. No se afirma capacidad multiinstancia.
- Módulos con campos comunes; los bloques especiales (galería, cuenta regresiva, RSVP) tienen representación propia.
- No hay publicación por versiones: editar y guardar un evento publicado actualiza su página inmediatamente.
- RSVP abierto no equivale a identidad verificada; la dirección de correo sirve para evitar duplicados, no para autorizar modificaciones.
- No hay proveedor de correo configurado ni recuperación de cuenta. Es un pendiente previo a producción.
