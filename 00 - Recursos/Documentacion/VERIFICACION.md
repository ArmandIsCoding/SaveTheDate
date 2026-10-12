# Verificación del portal y la portada pública

Fecha: 12 de octubre de 2026. Entorno local: macOS ARM64, SDK .NET 10.0.401, runtime 10.0.12.

## Automatizada

**14 pruebas correctas, 0 fallidas, 0 omitidas** mediante `dotnet test SaveTheDate.sln -c Release`. Las pruebas crean SQLite temporales y aplican la migración real.

| Área | Evidencia |
| --- | --- |
| Creación | Borrador persistido, slug único, galería individual y conversión Argentina → UTC. |
| Propiedad | Otra cuenta no puede listar, leer, editar, eliminar ni cargar fotos a un evento ajeno. |
| Módulos | Cambios, activación y orden sobreviven a guardados sucesivos. |
| Concurrencia | Una edición vieja no sobrescribe una versión nueva. |
| Validación | Se rechazan enlaces ejecutables y la eliminación de los módulos estructurales. |
| Publicación | Solo se leen invitaciones publicadas y activas; restaurar deja un borrador. |
| Galería | Carga real, conversión a WebP, edición, orden, portada, eliminación del archivo y normalización de posiciones. |
| Aislamiento de imágenes | Un ID de otro evento no se puede usar para portada o eliminación. SVG/código y archivos mayores de 10 MB se rechazan. |
| Asistencia | Datos validados, duplicados rechazados, respuestas privadas y módulo oculto sin recepción de nuevos RSVP. |
| Calendario | Fecha UTC, contenido escapado y protección frente a inyección de líneas. |
| Registro y sesión | Registro HTTP real, cookie, acceso al portal, logout y nuevo login. |
| Protección HTTP | Rechazo de POST sin CSRF, ausencia de acceso demo fuera de Development y 404 de recursos inexistentes. |
| Recorrido público | Publicar habilita imagen/calendario; RSVP persiste y muestra agradecimiento; despublicar vuelve a ocultar archivos. |
| Portada pública | Inicio accesible sin sesión; ingreso separado con CSRF; portal protegido. Los enlaces de la portada llevan al registro o a los eventos según la sesión. |

Compilación Release y generación del paquete con `dotnet publish` correctas. Base de datos y archivos privados excluidos de publicación. Consulta de paquetes NuGet: sin vulnerabilidades reportadas por las fuentes consultadas en esta fecha.

## Navegador

- Pantalla de ingreso y entrada al demo con cookie real.
- Portal con los tres eventos de ejemplo.
- Apertura de editor y confirmación visible de guardado.
- Alta de un evento de prueba usando controles reales.
- Carga múltiple de dos archivos mediante el selector nativo; confirmación visible y fotos renderizadas.
- Selección de portada y baja recuperable del evento de prueba. Se conserva en la papelera del demo.
- Portal e invitación revisados en escritorio y con viewport de 390 × 844.
- Banner móvil ajustado para evitar superposición de ilustración y texto.
- Invitación pública: portada, ancla interna, cuenta regresiva activa, fotografías, asistencia y footer.
- Navegación por anclas corregida para conservar el enlace de la invitación.
- Sin desbordamiento horizontal en las pantallas móviles inspeccionadas.
- Portada con tamaños de escritorio y celular: nombres legibles, paletas rosa/salvia/lavanda interactivas, navegación por anclas, preguntas desplegables y enlaces a registro e ingreso.
- Ejemplos y capturas actualizados con Mili y Armando. Los cambios en SQLite local se limitaron a los ejemplos originales de la cuenta demo, con un respaldo previo fuera del repositorio.

Las capturas de referencia están en `00 - Recursos/Capturas`.

## Límites de esta verificación

- La cámara física, los permisos de cámara y la conversión de fotos HEIC no se probaron en un teléfono real. Se implementó `capture="environment"` con el input nativo y se verificó la carga de archivos en el navegador.
- La selección múltiple se probó con archivos reales; el arrastre utiliza el mismo input nativo superpuesto a la zona de carga. No se simuló un arrastre desde el Finder.
- No se realizó una auditoría formal de accesibilidad ni una prueba de carga multiusuario.
- No se verificó un despliegue remoto, correo saliente ni restauración de backups de una instalación productiva.

Estos puntos quedan explícitos para la etapa de preparación para producción, sin presentarlos como capacidades ya validadas.
