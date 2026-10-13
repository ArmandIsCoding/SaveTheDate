# Verificación del portal, la portada y la administración

Fecha de actualización: 13 de octubre de 2026. Entorno local: macOS ARM64, SDK .NET 10.0.401, runtime 10.0.12.

## Automatizada

**27 pruebas .NET correctas, 0 fallidas, 0 omitidas** mediante `dotnet test "01 - Code/SaveTheDate.Tests" -c Release --no-restore`. Las pruebas crean SQLite temporales y aplican las migraciones reales.

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

| Superadministración | Páginas y archivos denegados para visitantes/cuentas comunes; las operaciones de servicio rechazan incluso un claim de rol falsificado. |
| Permisos | Asignación a cuenta existente e idempotente, rechazo del demo, sesión anterior al alta válida y revocación/marca de seguridad efectiva de inmediato. |
| Consultas administrativas | Recuentos distinguen activos y papelera; filtros por usuario/estado/tipo; asistencia por tamaño de grupo y paginación de 23 respuestas en dos páginas. |
| Privacidad administrativa | Galería privada solo por la ruta autorizada, RSVP escapado, hashes/marcas no renderizados, cabeceras no-store/private/noindex. |
| Almacenamiento | Archivos existentes, tamaño y detección de referencias sin archivo con SQLite y archivos reales temporales. |

Compilación Release y generación del paquete con `dotnet publish` correctas. Base de datos y archivos privados excluidos de publicación. Consulta de paquetes NuGet del 12/10/2026: sin vulnerabilidades reportadas por las fuentes consultadas ese día; esta actualización no cambia dependencias.

### Ajuste de correos detrás de Cloudflare

El 13/10/2026 se reprodujo en la web publicada el reemplazo de direcciones por enlaces `[email protected]`. Se agregó `no-transform` a las respuestas administrativas, conservando `private, no-store` y la autorización. Se ejecutaron las **6 pruebas dirigidas** de superadministración y portada: todas correctas. Verifican la nueva cabecera en las cuatro páginas administrativas y su ausencia en la portada pública. La comprobación final a través de Cloudflare requiere republicar esta versión en IIS.

## Navegador

### Integración de Google Maps

- 8 casos .NET nuevos: persistencia y limpieza de ubicaciones independientes, rechazo de IDs malformados, parámetros escapados, prioridad del lugar elegido, compatibilidad de enlaces antiguos y mapas visibles solo en módulos públicos habilitados.
- 4 pruebas JavaScript sin dependencias externas: selección y error del proveedor, independencia entre módulos, selección lenta descartada al elegir otra y limpieza al desmontar. Ejecutar `node --test "01 - Code/SaveTheDate.Tests/JavaScript/google-maps.test.mjs"`.
- El adaptador de Google se prueba con un proveedor simulado. Los tests HTTP usan una clave ficticia solo para comprobar el HTML; no envían consultas ni simulan haber obtenido un mapa real.
- Navegador local: dirección manual ingresada con teclado nativo, botón de indicaciones generado, guardado confirmado, recarga y persistencia comprobadas. Vista de celular sin desbordamiento horizontal.
- Migración aplicada sobre la base demo existente después de un backup. Los valores anteriores siguen disponibles; las nuevas columnas comienzan vacías.
- Captura `ubicacion-google-maps-pendiente-clave.png`: estado real antes de configurar la clave, sin mostrar un mapa ficticio.
- **Pendiente de configuración externa:** sugerencias y mapa reales con Google, cuotas/facturación, restricciones de dominio y verificación en IIS. El usuario todavía no tiene proyecto ni clave de Google Cloud; la guía está en Operación.

### Portal e invitaciones

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

Superadministración revisada el 13/10 con una base de prueba separada y datos ficticios de Mili y Armando:

- Entrada desde el enlace protegido del espacio personal; panorama, usuarios, filtros de borradores por propietario, módulos desplegables, fotografías privadas y almacenamiento.
- Carga correcta de las tres fotos de un borrador sin habilitar su invitación pública.
- Dashboard y listado de usuarios a 390 × 844 y control de ancho a 320 px, con tablas que desplazan su contenido dentro de la tarjeta. Se corrigió el desbordamiento de una etiqueta accesible de la tabla.
- Comando real de asignación de rol ejecutado contra la base local de prueba, independiente de la instalación pública.

Las capturas de referencia están en `00 - Recursos/Capturas`.

## Límites de esta verificación

- La cámara física, los permisos de cámara y la conversión de fotos HEIC no se probaron en un teléfono real. Se implementó `capture="environment"` con el input nativo y se verificó la carga de archivos en el navegador.
- La selección múltiple se probó con archivos reales; el arrastre utiliza el mismo input nativo superpuesto a la zona de carga. No se simuló un arrastre desde el Finder.
- No se realizó una auditoría formal de accesibilidad ni una prueba de carga multiusuario.
- No se verificó un despliegue remoto, correo saliente ni restauración de backups de una instalación productiva.

Estos puntos quedan explícitos para la etapa de preparación para producción, sin presentarlos como capacidades ya validadas.
