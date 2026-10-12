# Operación local y futura

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

## Despliegue futuro

1. Completar los pendientes de identidad, privacidad y cuotas del plan.
2. Publicar: `dotnet publish "01 - Code/SaveTheDate" -c Release -o .artifacts/publish`.
3. Elegir hosting compatible con ASP.NET Core y conexiones WebSocket persistentes.
4. Configurar almacenamiento persistente, connection string, HTTPS, claves de Data Protection, permisos mínimos y host permitido.
5. Ejecutar migraciones de forma controlada con backup y rollback. No copiar la base demo a producción.
6. Configurar forwarded headers solo para proxies de confianza; revisar dirección real del cliente para rate limiting.
7. Probar registro, cookies, publicación, uploads y RSVP en el dominio final, además de restaurar un backup.

No se creó ningún recurso cloud ni se publicó un sitio externo durante esta implementación.
