# SaveTheDate

Portal de eventos e invitaciones digitales con una estética de papel, acuarelas y colores pastel. Interfaz en español, pensada para computadoras y celulares.

**Blazor Web App (.NET 10) · Interactive Server · ASP.NET Core Identity · Entity Framework Core · SQLite**

## Empezar

Requisito: SDK de .NET 10. El repositorio usa `global.json` con la versión 10.0.401 y permite versiones posteriores de la misma versión principal.

Desde la raíz del repositorio:

```sh
dotnet restore SaveTheDate.sln
dotnet run --project "01 - Code/SaveTheDate" --launch-profile http
```

Abrir **http://localhost:5105**. También se puede abrir la solución `SaveTheDate.sln` en Visual Studio o Rider. En macOS, `iniciar.command` permite iniciar el proyecto con doble clic.

En Development se aplican las migraciones y se crea automáticamente un espacio demo con tres eventos y fotografías. El botón **Explorar el espacio demo** permite entrar sin credenciales. Para probar el aislamiento entre cuentas, usar **Crear una cuenta**. El demo y su acceso directo no existen fuera de Development.

## Qué está implementado

- Registro, ingreso, cierre de sesión y cookies de Identity; bloqueo temporal por intentos fallidos.
- Alta, consulta y edición de eventos propios, búsqueda, filtros, papelera y restauración.
- Fecha/hora local, zona horaria explícita, nombre, tipo, lugar y enlace personalizable.
- Publicación y vuelta a borrador; invitación pública en `/i/{enlace}` y vista previa privada.
- Editor de módulos con activación, configuración y orden mediante controles accesibles.
- Once tipos: portada, cuenta regresiva, historia, lugar/horario, galería, dress code, regalos, asistencia, música, información y redes. Se pueden agregar varios lugares.
- Tres paletas: Rosa acuarela, Jardín de salvia y Sueño lavanda.
- Una galería propia por evento: múltiples fotos, arrastrar/soltar, selector de archivos, captura nativa de cámara, descripción, orden, portada y eliminación.
- JPG/PNG/WebP, hasta 10 MB por foto, 20 por carga y 100 por galería. Validación por decodificación, orientación EXIF, reducción a 2400 px, conversión a WebP y eliminación de metadatos. HEIC requiere conversión a JPG.
- RSVP público con validación y registro en SQLite; resumen y detalle de respuestas solo para el organizador. Una respuesta por correo/evento, sin sobrescritura anónima.
- Cuenta regresiva en vivo y descarga de calendario `.ics` en UTC.
- Fotografías de ejemplo, fuentes y elementos gráficos locales; no se necesitan CDNs para utilizar el sitio.

## Organización

```text
SaveTheDate.sln
01 - Code/
  SaveTheDate/
    Components/       Interfaz Blazor, páginas, layouts y componentes
    Domain/           Entidades y catálogo de módulos
    Services/         Operaciones, autorización, imágenes e invitaciones
    Data/             DbContext, migraciones y datos demo
    Endpoints/        Autenticación mediante solicitudes HTTP
    wwwroot/          Estilos, tipografías y recursos visuales
    App_Data/         SQLite y archivos privados (ignorado por Git)
  SaveTheDate.Tests/   Pruebas de integración con SQLite real y servidor de pruebas
00 - Recursos/
  Documentacion/      Plan, arquitectura, operación y estado de verificación
```

Un único proyecto de aplicación y un proyecto de pruebas. No hay una API separada ni capas de repositorios genéricos innecesarias. Cada operación usa un contexto de base de datos independiente.

## Pruebas

```sh
dotnet test SaveTheDate.sln
dotnet build SaveTheDate.sln -c Release
```

Las pruebas usan bases temporales, sin modificar el espacio demo. Cubren registro/cookies/CSRF, autorización entre propietarios, módulos, concurrencia, publicación, privacidad de imágenes, galerías, RSVP y calendario.

## SQLite y migraciones

Los datos locales están en `01 - Code/SaveTheDate/App_Data/savethedate.db` y las imágenes en `App_Data/uploads`. Para respaldar una instalación se necesitan **ambos**. Con el servicio detenido, copiar toda la carpeta `App_Data`.

```sh
dotnet tool restore
dotnet ef database update --project "01 - Code/SaveTheDate"
```

Para modificar el esquema:

```sh
dotnet ef migrations add NombreDelCambio --project "01 - Code/SaveTheDate" --output-dir Data/Migrations
```

Se puede configurar `ConnectionStrings__DefaultConnection` con otra ruta SQLite. Migrar a PostgreSQL/SQL Server requerirá cambiar el proveedor y crear/revisar migraciones específicas; la lógica de negocio ya está separada del proveedor.

## Alcance y siguiente etapa

Esta es una primera versión funcional para desarrollo local. No se desplegó a Internet. Antes de habilitar cuentas reales en producción, completar verificación y recuperación de correo, política de privacidad/retención, límites globales de almacenamiento, respaldos, observabilidad, claves de Data Protection persistentes, HTTPS y pruebas en dispositivos físicos. El RSVP no verifica la identidad del invitado. La carga desde la cámara usa el control nativo del navegador y depende del dispositivo.

El [plan completo](<00 - Recursos/Documentacion/PLAN.md>), las [decisiones de arquitectura](<00 - Recursos/Documentacion/ARQUITECTURA.md>) y la [guía de operación](<00 - Recursos/Documentacion/OPERACION.md>) detallan lo implementado y lo que sigue.

## Licencia y datos locales

El código y los recursos originales de SaveTheDate se distribuyen bajo la [licencia MIT](LICENSE). Las fuentes y las fotografías de demostración conservan sus propias licencias, detalladas en [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md); no se relicencian bajo MIT.

El repositorio incluye únicamente datos ficticios de demostración. La base SQLite, las fotos subidas, los respaldos, las claves, los archivos `.env` y los artefactos de compilación están excluidos por `.gitignore`. Mantener las credenciales en variables de entorno o en un gestor de secretos, nunca en los archivos versionados de configuración. Antes de publicar cambios, revisar también los archivos nuevos: `.gitignore` no reemplaza una revisión de secretos.
