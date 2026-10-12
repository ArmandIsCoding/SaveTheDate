# Plan de producto e implementación

## Objetivo

Un portal donde cada persona pueda administrar sus celebraciones y construir invitaciones modulares, con una experiencia cuidada, cálida y accesible. El primer alcance es una aplicación local real y persistente, preparada para evolucionar; no incluye la contratación de hosting ni el lanzamiento público.

## Decisiones de partida

- Solución y proyecto principal: **SaveTheDate**.
- Blazor Web App con **Interactive Server**, no WebAssembly en esta etapa. Se ejecuta C# en el servidor y la interacción viaja por SignalR. Reduce duplicación y facilita trabajar con Identity, SQLite y archivos. Requiere conexión con el servidor.
- .NET 10; EF Core y SQLite con migraciones versionadas.
- Un monolito modular por carpetas, sin microservicios, CQRS ni capas de repositorios artificiales.
- Español rioplatense; zona horaria Argentina por defecto, configurable por evento. Fechas persistidas en UTC.
- Autenticación con Identity y cookies; cada operación restringida al propietario.
- Archivos privados locales detrás de un servicio de almacenamiento sustituible.
- Diseño propio inspirado en el tipo de contenido de la [referencia](https://www.agendalafecha.com/invitaciones/black/). Sin copiar sus fotografías, textos o código.

## Etapa 1 · Base funcional implementada

1. **Fundación:** solución, proyecto, configuración, navegación, estilos, persistencia y migración inicial.
2. **Identidad:** registro, inicio/cierre de sesión, validación, protección CSRF, bloqueo de acceso por intentos y revalidación de sesión.
3. **Eventos:** alta, consulta, cambios, papelera, recuperación, búsqueda y filtros. Borrador/publicado; enlace único y vista previa.
4. **Editor:** catálogo extensible, múltiples lugares, orden, activación, contenido y enlaces seguros. Portada y galería siempre existen; pueden ocultarse en la invitación.
5. **Galería:** ABM de imágenes por evento, carga múltiple por selección y arrastre, cámara nativa, validación, normalización, descripciones, orden y portada.
6. **Invitación:** diseño responsive, contenido de módulos, cuenta regresiva, galería, enlaces a mapas/regalos/música/redes y calendario.
7. **Asistencia:** formulario público y listado privado, cantidad de asistentes y notas. Una respuesta por correo/evento; cambios posteriores requieren contactar al organizador.
8. **Experiencia:** tres paletas pastel, textura de papel/acuarela, estados vacíos, ayuda, validación, avisos de guardado, navegación por teclado y diálogo con foco contenido.
9. **Verificación:** pruebas automatizadas con SQLite real; recorridos en navegador y revisión de anchos de escritorio/móvil. Alcance exacto en `VERIFICACION.md`.

### Criterios de aceptación de esta etapa

- Una cuenta nueva puede crear su evento y no ve eventos ajenos.
- Un evento conserva la configuración después de recargar o reiniciar el servidor.
- Cada evento tiene su galería y no permite modificar imágenes de otro evento.
- Un borrador no responde como invitación pública ni entrega imágenes a visitantes anónimos.
- Publicar habilita invitación, imágenes y calendario; volver a borrador los oculta.
- Agregar, quitar, ordenar y ocultar módulos cambia la invitación guardada.
- Las imágenes inválidas/sobredimensionadas se rechazan con un mensaje.
- Las respuestas llegan al organizador sin exponer datos de otros invitados.

## Etapa 2 · Preparación para usuarios reales

Prioridad antes de lanzamiento:

- Conectar proveedor de correo, confirmar cuentas, recuperación y cambio de contraseña, gestión del perfil y cierre de otras sesiones.
- Condiciones de uso, privacidad, consentimiento, eliminación de cuenta y política de retención de eventos/fotos/respuestas.
- Cuotas por cuenta, límites de eventos, moderación y protección adicional de RSVP frente a abuso. Decidir si las invitaciones son abiertas, con PIN o dirigidas a invitados identificados.
- Backups automáticos de DB/archivos/claves, restauración ensayada, logs estructurados, métricas, alertas y health checks.
- Hosting .NET con WebSockets, HTTPS, secretos y claves de Data Protection persistentes; balanceo/sesiones si se escala horizontalmente.
- Validación en Safari iOS y Chrome Android físicos: cámara, permisos, formato de fotos, reconexión, lector de pantalla y conexión móvil lenta.
- Publicación con separación entre borrador editable y versión publicada. En la versión inicial, guardar cambios en un evento publicado los hace visibles inmediatamente.
- Edición de respuestas por organizador y/o enlace firmado del invitado; exportación de invitados.

## Etapa 3 · Editor y experiencia premium

- Guardado automático con historial, restauración de versiones y vista previa fiel en vivo.
- Arrastre de módulos además de controles accesibles; campos específicos por tipo y múltiples galerías opcionales.
- Recorte visual de portada, punto focal, miniaturas, video y soporte HEIC mediante servicio compatible.
- Plantillas completas, selector de tipografías, personalización avanzada y duplicación de eventos.
- Música/sugerencias nativas, lista de regalos, hospedaje y traslados estructurados, agenda con varias actividades y horarios propios.
- Álbum colaborativo para invitados, enlaces firmados, moderación y cuotas.
- Invitaciones por grupo familiar, acompañantes y restricciones alimentarias con consentimiento adecuado.

## Etapa 4 · Escala y comercialización

- PostgreSQL según volumen/concurrencia; migración de datos con ensayo y rollback.
- Almacenamiento compatible con S3, CDN de archivos publicados y tareas de procesamiento de imágenes.
- Planes y pagos, dominios personalizados, organizaciones/colaboradores y permisos por evento.
- Estadísticas respetuosas de privacidad, búsqueda avanzada, internacionalización y soporte operativo.

## Estrategia de evolución

Cerrar una etapa con evidencia de aceptación antes de ampliar. Mantener reglas de negocio en servicios, vistas en componentes y autorizaciones en cada operación. Añadir interfaces cuando permitan sustituir dependencias reales (almacenamiento, correo, identidad), evitando fragmentar el proyecto antes de que exista una necesidad.
