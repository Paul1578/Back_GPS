# Seguridad

## Controles implementados

- El registro público asigna `User` aunque el request solicite otro rol. La cuenta Admin de bootstrap sólo se crea con configuración explícita.
- JWT, base de datos, CORS y SMTP se configuran fuera de los valores versionados; CORS acepta una lista explícita de orígenes. El inicio exige conexión DB completa y JWT con clave de 32 bytes, emisor y audiencia.
- La contraseña local MySQL root y la clave JWT fueron rotadas; todos los refresh tokens anteriores se revocaron. Los usuarios locales deben iniciar sesión de nuevo. `.env` está ignorado y tiene permisos 0600. Docker Compose requiere `MYSQL_ROOT_PASSWORD` y `.env.example` contiene marcadores no utilizables.
- `OwnerUserId` se fija desde la claim firmada `owner_id`; filtros globales limitan Vehicles, Drivers, Routes y posiciones al tenant. La migración local asignó el vehículo huérfano al administrador raíz más antiguo, decisión confirmada por el usuario.
- Los controladores y comandos verifican rol, pertenencia al tenant y asignación Driver. El propietario raíz no se puede desactivar ni degradar desde el API de equipo.
- El cambio de rol se guarda en una unidad de trabajo. El estado de Driver no permite iniciar una ruta si el perfil está inactivo; la baja bloquea si hay ruta en curso y libera el vehículo.
- Los tokens de recuperación/activación se guardan con hash y no se escriben en logs. La invitación envía enlace de activación y no envía una contraseña temporal. Reset y activación revocan los refresh tokens existentes.
- Cada cuenta tiene un único rol (índice único en DB, cambio transaccional y emisión JWT rechaza estados inconsistentes). El borrado aislado de un rol se rechaza.
- CORS se aplica en todos los entornos con orígenes configurables. En producción se aplican HSTS y redirección HTTPS; el despliegue debe configurar proxy/certificado y `AllowedHosts` del dominio real.
- MySQL y phpMyAdmin de Docker sólo publican puertos en loopback (`127.0.0.1`), no en todas las interfaces del host.
- La asignación manual rechaza vehículos fuera de estado Available y los endpoints de rutas no usan el ID de cuenta como fallback de ID de ruta.
- El rate limit limpia claves vencidas, pero sigue siendo local al proceso; para múltiples réplicas se requiere un limitador distribuido.
- Las paradas validan estado/operación, evidencia JPEG/PNG/WebP y tamaño; el registro GPS valida coordenadas, velocidad, rumbo y consultas acotadas.
- Excepciones internas devuelven mensaje genérico y TraceId. Logs SMTP omiten destinatario, host, credenciales y cuerpo.
- `MySqlConnector` está fijado en 2.6.2 para incluir la corrección de [GHSA-473q-m89c-ghf8](https://github.com/advisories/GHSA-473q-m89c-ghf8). `dotnet list ... --vulnerable` no reporta vulnerabilidades.
- Los locks de asignación usan consultas parametrizadas y `OwnerUserId`; no aceptan nombres de tablas externos. Las transiciones también bloquean Route antes de Vehicle/Driver para serializar cambios sobre el mismo recurso.
- El provider SQLite de pruebas depende de SQLitePCLRaw.lib.e_sqlite3 2.1.14; NuGet audit confirma cero vulnerabilidades después de corregir CVE-2025-6965 presente en la resolución 2.1.10 inicial.
- Durante una inspección local se imprimió por error el valor anterior de MySQL root; fue rotado para los hosts `%` y `localhost` y `.env` se mantuvo en modo 0600. La nueva credencial no se copió al repositorio ni a los logs.

## Pendientes

- Rotar cualquier copia de estas credenciales que se haya reutilizado en entornos remotos; la rotación de esta sesión sólo afecta el entorno local.
- Configurar credenciales SMTP vigentes para entregar invitaciones y restablecimientos; la app muestra el fallo mientras no estén disponibles.
- Desplegar detrás de HTTPS y definir certificados/orígenes de producción.
- Migrar access tokens del `localStorage` a cookies HttpOnly con soporte del backend.
- Para alto volumen, mover evidencia desde `PointsJson` a almacenamiento de objetos y añadir rate limit distribuido.
- La migración local asignó el vehículo histórico huérfano al Admin raíz más antiguo por decisión explícita del usuario; validar propietarios antes de ejecutar esta migración en otra base.

## Errores de seguridad ya corregidos

Los hallazgos anteriores de roles públicos, logs con tokens, CORS abierto, recursos sin tenant y dependencia MySqlConnector vulnerable están resueltos y sus causas/cambios están en `ERROR_LOG.md`.

## Cobertura verificada — 2026-11-21

- EF InMemory ahora prueba el filtro de propietario en Vehicles, Drivers, Routes y RoutePositions; dos compañías no leen los registros de la otra.
- Regresiones de metadatos comprueban Admin/Manager para Vehicles y Drivers, y Admin para Users. El smoke HTTP/MySQL integrado en CI cubre aislamiento entre tenants para Vehicles, Drivers, Routes y RoutePositions; no reemplaza una matriz completa de permisos por rol.
- Consulta de dependencias al cierre: `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive` no encontró paquetes vulnerables.

La prueba HTTP/MySQL cross-tenant de cierre cubre Vehicles, Drivers, Routes y creación/lectura de RoutePositions. Un tenant ajeno recibe 404 para recursos individuales y no puede crear asociaciones cruzadas; GET de colección de posiciones devuelve `200 []` por el filtro global. El caso completo pasó dentro de Playwright 8/8 y el smoke de backend se integró al workflow. Sigue pendiente integrar Playwright autenticado frontend/API en CI remota. El smoke comprueba además que el rol `User` recibe 403 al leer rutas/vehículos y crear vehículos.

## Configuración por entorno y smoke CI

`Program.cs` carga `.env` con `Env.NoClobber().Load()`: las variables inyectadas por proceso/deploy tienen precedencia. El workflow backend inicia MySQL descartable, genera clave JWT/credenciales bootstrap sólo por ejecución y corre el smoke tenant con restricciones a loopback y nombre `fletflow_ci*`. La limpieza borra IDs de los fixtures, no todos los usuarios QA por prefijo.
