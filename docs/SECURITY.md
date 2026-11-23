# Seguridad

## Estado vigente de endurecimiento y riesgos de despliegue — 2026-11-23

- Revisión independiente read-only de Backend `391792e` y Front `6555858`: no encontró un bypass nuevo de tenant/rol, fallo JWT explotable ni vector XSS concreto en el alcance inspeccionado. Confirmó filtros globales, root-only y validación `TokenVersion` en JwtBearer `OnTokenValidated`. La revisión estática no sustituye smokes dinámicos.

- `POST /api/Auth/forgot-password` permite cinco solicitudes por diez minutos y por IP, incluso desde loopback en Development. El contador no usa correo/ID de cuenta, no bloquea cuentas y devuelve 429 con `Retry-After` al exceder el límite.
- El limitador y sus ventanas residen en memoria del proceso: reiniciar el backend reinicia los contadores; varias réplicas no comparten el límite. `UseForwardedHeaders` procesa como máximo un salto de `X-Forwarded-For`/`X-Forwarded-Proto`, con simetría obligatoria y sólo desde IPs explícitas en `ForwardedHeaders:KnownProxies`. La lista versionada está vacía por defecto. El despliegue debe configurar las IP reales de su ingress y considerar un backend distribuido para escalar.
- Los tokens de reset se generan con 32 bytes aleatorios (256 bits) y sólo se persiste su SHA-256. El TTL por defecto es 60 minutos; `Email:ResetTokenMinutes` sigue siendo configurable y se valida entre 1 y 1440 minutos.
- `Email:ResetPasswordUrl` y `Email:ActivationUrl` deben ser URLs absolutas HTTPS fuera de Development. En Development se permite HTTP sólo para hosts loopback. `ValidateOnStart` y la resolución explícita de opciones antes de sembrar DB fallan con nombres de configuración y sin imprimir valores de URL.
- SMTP conserva su comportamiento de error: el limitador no declara que el correo fue enviado. Outbox persistente y Data Protection siguen dependiendo de volúmenes, certificado y secretos de despliegue correctamente configurados.
- Action Backend `45127502896` en `63333bb`: Release, xUnit 136/136, aislamiento tenant, concurrencia y límite de login con proxies confiables por HTTP/MySQL/Staging. XFF falsificado desde origen no confiable se ignoró; dos proxies autorizados se agregaron a una IP cliente y otra IP autenticó. Esto no sustituye configuración de ingress real, réplicas, SMTP ni base de datos de producción.

## Precondiciones pendientes para migrar datos existentes — no inferir desde el código

- `AuthService` emite `owner_id = OwnerUserId ?? User.Id`, y `UserController` considera raíz cuando ese ID coincide con el usuario. En datos heredados, un Admin delegado con `OwnerUserId IS NULL` podría tener autoridad de raíz. No se debe asignar OwnerUserId automáticamente sin identificar raíces válidas; el modelo no ofrece suficiente información para distinguir todos los nulos.
- La migración `20261121062438_RouteLifecycleAndSingleRole` añade índice único en `UserRoles.UserId`; puede fallar si hay usuarios con más de un rol. Antes de aplicarla sobre una copia de datos existentes, revisar `SELECT UserId, COUNT(*) FROM UserRoles GROUP BY UserId HAVING COUNT(*) > 1;` y definir la regla de resolución.
- Revisar rutas sin propietario y relaciones incompatibles en una copia: `SELECT COUNT(*) FROM Routes WHERE OwnerUserId IS NULL;` y comparar propietario de `Routes.DriverId`/`VehicleId` con `Routes.OwnerUserId`. La migración tenant deja NULL cuando no puede asignar un propietario consistente; el filtro global las oculta para evitar fuga entre empresas, pero pueden quedar operaciones inaccesibles.
- Causa del acceso denegado local: `DB_PASSWORD` no coincidía con `MYSQL_ROOT_PASSWORD`. La variable actual de root en `.env` sí permitió conexión local; se sincronizó `DB_PASSWORD` sin imprimir ni versionar el secreto. El password declarado por el contenedor era antiguo. Las consultas de preflight fueron sólo lectura. Después, el arranque aplicó migraciones pendientes revisadas, incluido el backfill previsto de nombres desde Drivers vinculados.
- Preflight local: 2 Admin sin propietario (corresponden a las dos cuentas raíz que el usuario confirmó), 0 usuarios con varios roles, 0 rutas sin propietario y 0 asociaciones de ruta con propietario discordante. El vehículo sin conductor ya pertenece al Admin raíz más antiguo elegido por el usuario.
- API local aplicó cuatro migraciones pendientes revisadas: `TokenVersion`, nombres de usuario con backfill desde Driver asociado, `Routes.CreatedAtUtc` y tabla vacía `EmailOutbox`. Historia EF quedó 26/26. No restablecer credenciales ni recrear el volumen.

## Otros riesgos por validar en despliegue

- `SimpleRateLimitMiddleware` usa `RemoteIpAddress` y memoria estática. Los encabezados reenviados sólo se aceptan desde `ForwardedHeaders:KnownProxies`; configurar las IP explícitas del proxy en el entorno antes del despliegue. Varias réplicas no comparten contadores; usar almacenamiento distribuido antes de escalar.
- Registro público permite crear nuevos tenants; ahora también está limitado a cinco solicitudes por diez minutos/IP, además del límite general. La ventana vive en memoria y comparte las limitaciones de proxy/réplicas descritas arriba.
- Recuperación devuelve cuerpo uniforme y tiene un piso temporal, pero la rama de cuenta existente hace más escrituras que la inexistente. La diferencia temporal potencial no se midió bajo carga.

## Hallazgo corregido — revocación de refresh por cuenta — 2026-11-22

- `POST /api/Auth/logout` ahora vincula el refresh token solicitado con el ID autenticado (`NameIdentifier`). Un usuario que posea el refresh de otra cuenta no puede revocarlo; la respuesta continúa siendo idempotente y no revela la existencia del token.
- Pruebas: xUnit valida ambos casos; el smoke HTTP/MySQL real probó B intentando revocar el refresh de A, A rotándolo después y revocando su propio sucesor. Build Release limpio, **99/99** xUnit, smokes tenant y concurrencia exit 0 con cleanup.
- Action `45058536309` confirmó Release build/xUnit y los smokes HTTP/MySQL/concurrencia en la rama publicada.
- La política quedó definida: sólo la cuenta raíz puede crear/promover Admin. Action `45088129188` confirmó la denegación de Admin delegado en HTTP y la ausencia de cambios en MySQL.

## Controles implementados

- Forwarded Headers se ejecuta antes del rate limiter, con un único salto, simetría de XFF/XFP y allowlist explícita. `ForwardedHeaders:KnownProxies` está vacía por defecto. Ejemplo: `ForwardedHeaders__KnownProxies__0=10.0.0.10`; añadir únicamente las IP de origen de proxies administrados.

- El registro público crea una cuenta raíz `Admin` de un tenant nuevo. El backend fija ese rol e ignora `roleName` enviado por el cliente; el tenant queda limitado al `owner_id` del usuario recién creado. No otorga acceso a organizaciones existentes.
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

## Revisión de edición de perfil — 2026-11-22

- Auditoría estática independiente confirmó que el endpoint exige Admin, limita el miembro al tenant, bloquea editar cuenta raíz, valida campos a 100 caracteres y no expone hash/token.
- No se encontró IDOR directo por este endpoint. Como defensa en profundidad, `UpdateUserProfileCommand` ahora exige `CurrentOwnerUserId` y filtra tanto usuario como Driver asociado por propietario.
- xUnit verifica sync de nombres User/Driver y rechazo cross-tenant. Actions `45007491905` pasó 64/64 y smoke HTTP/MySQL valida máximos de username, FirstName y LastName; cleanup exitoso.
- Política definida por el usuario: sólo la cuenta raíz puede crear o promover miembros a Admin. La verificación compara el ID autenticado con el `owner_id` firmado; las rutas de creación, cambio de rol y alias responden 403 a Admin delegado.

## Cobertura verificada — 2026-11-21

- EF InMemory ahora prueba el filtro de propietario en Vehicles, Drivers, Routes y RoutePositions; dos compañías no leen los registros de la otra.
- Regresiones de metadatos comprueban Admin/Manager para Vehicles y Drivers, y Admin para Users. El smoke HTTP/MySQL integrado en CI cubre aislamiento entre tenants para Vehicles, Drivers, Routes y RoutePositions; no reemplaza una matriz completa de permisos por rol.
- Consulta de dependencias al cierre: `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive` no encontró paquetes vulnerables.

La prueba HTTP/MySQL cross-tenant de cierre cubre Vehicles, Drivers, Routes y creación/lectura de RoutePositions. Un tenant ajeno recibe 404 para recursos individuales y no puede crear asociaciones cruzadas; GET de colección de posiciones devuelve `200 []` por el filtro global. La integración autenticada frontend/API/MySQL está activa en GitHub Actions; los ciclos recientes de frontend pasaron 20/20.

## Configuración por entorno y smoke CI

`Program.cs` carga `.env` con `Env.NoClobber().Load()`: las variables inyectadas por proceso/deploy tienen precedencia. El workflow backend inicia MySQL descartable, genera clave JWT/credenciales bootstrap sólo por ejecución y corre el smoke tenant con restricciones a loopback y nombre `fletflow_ci*`. La limpieza borra IDs de los fixtures, no todos los usuarios QA por prefijo.

## Revocación por estado de cuenta — 2026-11-21

- Desactivar una cuenta revoca todos sus refresh tokens en el mismo `AppDbContext`/commit que persiste `IsActive=false`.
- `JwtBearerEvents.OnTokenValidated` comprueba en base de datos que el usuario del access token exista y continúe activo; un token ya emitido deja de autorizar al desactivar la cuenta. `/Auth/refresh` aplica la misma regla antes de rotar tokens.
- Verificado con HTTP real y MySQL descartable: después de la baja, `/Auth/me` y `/Auth/refresh` devuelven 401 para access/refresh emitidos antes de la operación.
- Esta validación hace una consulta de usuario por autenticación de bearer. Si se optimiza con caché en el futuro, la expiración/invalidation debe conservar el efecto inmediato de una baja.

## HTTP por rol en MySQL temporal — ampliación

El smoke HTTP ahora verifica Admin, Manager, Logistics y User: Manager lista Vehicles/Drivers/Routes; Logistics lista Routes y recibe 403 en Vehicles/Drivers; User recibe 403 en flota/rutas. Driver mantiene recorrido real/denegaciones verificados por Playwright autenticado. Esta cobertura ya se ejecutó contra MySQL local descartable; se repetirá en Actions al publicar.

Las mutaciones también se comprobaron con HTTP/MySQL: Manager puede crear Vehicle/Driver/Route y editar Route; Logistics puede editar/eliminar Route pero no crear Vehicle/Driver; User y Driver reciben 403 al crear Route.

El smoke anónimo solicitó rol Admin en el registro; la fila persistida conserva únicamente User y el token no puede consultar Vehicles. Logout-all se ejecutó con un refresh real: HTTP 200 y refresh posterior 401.

## Revocación de permisos al cambiar rol — 2026-11-21

- Cada request autenticado verifica en DB que la cuenta continúe activa y que su único rol actual coincida con el claim firmado del access token. Cambiar rol invalida inmediatamente access tokens previos, incluso antes de su expiración.
- `ChangeUserRoleCommand` revoca los refresh tokens del usuario en el mismo commit del cambio de rol.
- Si el usuario tiene Driver asociado, el comando toma el lock de Vehicle/Driver compartido con la transición de ruta y vuelve a leer el estado bajo lock. Se impide la carrera que podía dejar ruta InProgress con Driver desactivado.
- La consulta de usuario para esa relectura es `AsNoTracking`, así que no devuelve una entidad cargada antes de esperar por el lock.
- Pruebas HTTP/MySQL descartables comprobaron 401 para access/refresh viejos y estados válidos de la carrera inicio vs cambio Driver→Logistics. Queda pendiente repetir en Actions tras push.

## Revocación inmediata al cambiar contraseña — 2026-11-21

- Hallazgo: los comandos de cambio/restablecimiento revocaban refresh tokens, pero un access JWT ya emitido seguía aceptándose hasta expirar. La revocación de sesión no era inmediata.
- Se agregó `Users.TokenVersion` (migración aditiva con default 0). Cada cambio/reset/activación incrementa la versión y emite JWT con claim `token_version`; `OnTokenValidated` compara el claim con la versión actual en DB. Cambiar contraseña invalida access y refresh anteriores inmediatamente.
- La operación mantiene password hash, incremento y revocación de refresh dentro del mismo `UnitOfWork`/commit. La migración debe aplicarse al desplegar esta rama; los tokens anteriores al cambio, sin claim de versión, requerirán iniciar sesión nuevamente.
- xUnit: 54/54. Smoke HTTP de cambio de contraseña extendido para MySQL; validar en el nuevo run Actions antes de declarar verificado extremo a extremo.

- Resultado Actions `44983337062` (commit `cff33c6`): migración aplicada en MySQL disposable; smoke cambió contraseña, access/refresh viejos dieron 401, el login con password anterior falló y el nuevo fue aceptado. Workflow completo success.

## Matriz de autorización y operación Logistics — 2026-11-22

- Actions `44989454133` del frontend ejecutó contra backend `chore/stabilize-fleet-platform` la matriz negativa User/Driver/Logistics/Manager en MySQL efímero: Playwright 15/15, sin skips; xUnit 57/57. Las operaciones de flota/rutas/administración no permitidas devolvieron los 403 esperados.
- Smoke descartable amplió el camino positivo Logistics: crear/editar ruta, iniciar, completar parada PickupAndDelivery con evidencia, registrar GPS y cerrar. El backend devolvió el vehículo a `Available`; MySQL confirmó persistencia y cleanup. Actions `44990525889` pasó el ciclo completo y `44991032520` volvió a validar el código con fallback por nombre QA (cleanup `0:0:0`). El caso extremo de respuesta sin ID no se indujo.
- La cobertura de políticas sigue siendo parcial respecto a todas las acciones posibles; la autorización y el aislamiento deben permanecer en el servidor, no sólo en los controles UI.


## Auditoría NuGet de continuidad — 2026-11-22

- `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive` terminó con código 0 y reportó que `BackEnd_Gps` y `BackEnd_Gps.Tests` no tienen paquetes vulnerables.

## Hallazgos de sesiones — 2026-11-22

- Corregida la carrera de refresh concurrente mediante actualización condicional de una fila dentro de transacción y creación del sucesor antes del commit. HTTP/MySQL probó 1 ganador (200), 1 perdedor (401), token anterior revocado y un único sucesor activo.
- `logout-all` ahora incrementa `TokenVersion` e invalida access JWT ya emitidos, además de revocar refresh. La prueba HTTP comprobó 401 para ambos tokens anteriores y login nuevo 200.
- Forgot-password devuelve el mismo contenido público para correo conocido/desconocido y `ExpiresAt=null`; el error del proveedor SMTP se registra internamente y la respuesta no afirma envío exitoso.
- Prueba HTTP/MySQL ejecutada localmente en contenedor efímero `fletflow-ci-refresh-20261122`; smoke informó cleanup de 0 rutas, 0 posiciones GPS y 0 vehículos de Logistics/Manager. Repetir en Actions al publicar.

### Coordinación de logout-all con refresh — 2026-11-22

- Un auditor encontró que listar refresh activos antes de revocarlos podía omitir el sucesor que se insertara mientras tanto. La corrección hace que `logout-all` y `TryRotateAsync` tomen el mismo lock transaccional de la fila `Users` antes de cambiar sesiones. El incremento de `TokenVersion`, revocación y `SaveChanges` de logout-all se confirman juntos.
- El smoke ahora bloquea deliberadamente la fila del usuario desde MySQL y lanza refresh/logout-all en paralelo; exige HTTP 200 de logout, cero refresh activos al final y 401 al sucesor si refresh alcanzó a responder 200. Resultado local MySQL 8: aprobado.
- Un auditor indicó que no había validación de `TokenVersion` porque el middleware personalizado no la contiene. Revisión del pipeline completo confirmó que `Program.cs` registra `JwtBearerEvents.OnTokenValidated` y compara versión/estado/rol; smoke HTTP verificó que access anterior recibe 401. No se duplicó lógica.

### Limitación de enumeración por tiempo en recuperación

- Respuestas JSON y expiración son iguales para correo existente/no existente, y la excepción SMTP no se expone. Aun así, una cuenta existente ejecuta escritura y flujo SMTP mientras una desconocida no lo hace; existe diferencia temporal potencial. Requiere desacoplar el envío y reforzar respuesta uniforme; sigue pendiente y no se considera cerrado.

### Mitigación de timing en recuperación — 2026-11-22

- En la primera mitigación, SMTP dejó de ejecutarse dentro del request mediante una cola bounded de proceso. Ese mecanismo fue sustituido el 2026-11-22 por outbox cifrado y persistente descrito abajo.
- Tanto correo existente como desconocido esperan un mínimo de 250 ms; el smoke HTTP compara respuestas JSON/códigos y exige al menos 200 ms para ambos casos.
- MySQL 8 + API reales: ambas respuestas fueron HTTP 200 idénticas; el worker registró `SmtpHost no configurado` y no indicó éxito de envío. No se envió correo.
- Límite residual de timing: el piso temporal reduce la diferencia, pero no garantiza igualdad bajo DB lenta/sobrecarga; tratar timing como mitigado parcialmente, no imposible. La pérdida por reinicio/cola llena quedó corregida con el outbox descrito abajo.

## Creación concurrente de cuentas — 2026-11-22

- La unicidad del correo se aplica por índice global MySQL. Si dos altas cruzan el pre-check, el guardado perdedor identifica el duplicado confirmado por DB y responde 409; no convierte otras excepciones DB a conflicto ni expone detalles internos.
- `RegisterUserCommand` aplica límites de la entidad antes de persistir y el controller entrega 400 para campos fuera de contrato. MySQL smoke confirmó una sola cuenta/rol/token tras dos requests concurrentes al mismo email y ninguna persistencia para límites excedidos.
- `/Routes/assignment-options` rechaza token Driver (403); la autorización en backend sigue siendo la fuente de verdad.

## Outbox durable de recuperación — 2026-11-22

- El token de reset y el mensaje outbox se guardan en el mismo AppDbContext/commit. EmailDeliveryQueue es scoped y sólo agrega el mensaje al contexto; EmailOutboxWorker es hosted y resuelve DB/proveedor mediante scope.
- Email, enlace y token se serializan juntos y se protegen con ASP.NET Data Protection (FletFlow.EmailOutbox.PasswordReset.v1); no se guardan en claro. El UserId queda como metadato FK para lifecycle/diagnóstico y puede revelar que una cuenta solicitó recuperación.
- Configuración: FLETFLOW_DATA_PROTECTION_KEYS_PATH permite fijar el directorio del key ring. En producción debe montarse un volumen persistente, restringido al usuario del proceso, respaldado y compartido con el mismo ApplicationName=FletFlow entre réplicas. Perder o rotar/eliminar prematuramente una clave vuelve ilegibles sus mensajes; el worker reintenta hasta 12 intentos y luego marca dead-letter. Monitorear DeadLetteredAtUtc y LastFailureType.
- El claim usa actualización condicional atómica, lease de 2 minutos y LockToken para no sobrescribir una concesión posterior. El SMTP se llama fuera de transacción. La entrega es al menos una vez: si el proveedor acepta el email y la aplicación cae antes de marcar SentAtUtc, podría repetirse.
- Los fallos reintentan con backoff exponencial, limitado a seis horas; tras 12 intentos van a dead-letter. Mensajes enviados se purgan después de 30 días y dead-letter después de 90 días.
- Verificación HTTP/MySQL local: contenido persistido opaco, dos solicitudes concurrentes producen un token activo y ambos mensajes se guardan; SMTP sin credenciales no reporta éxito. Las credenciales SMTP válidas siguen siendo un requisito externo para la entrega real.

## Revalidación de autorización Driver y outbox — 2026-11-22

- Un Driver del mismo tenant no puede listar/leer ni cambiar estado, parada, evidencia o GPS de una ruta asignada a otro Driver. El smoke HTTP usa tokens reales contra API/MySQL; Admin confirma que no hay mutaciones. Actions `45029798323` pasó con cleanup verificado.
- `PUT /Routes/{id}/status` vuelve a autorizar al Driver después del lock de asignación y la recarga de la ruta, cerrando la ventana TOCTOU entre el controller y el command. La regresión simula una reasignación durante el lock y exige cero cambios persistidos.
- `EmailOutboxWorkerTests` ejecuta el BackgroundService real sobre SQLite: comprueba descifrado/entrega, marca `SentAtUtc` y libera lease; ante fallo en intento 12 verifica dead-letter y `LastFailureType`. Actions `45030208849` pasó Release build, xUnit **80/80**, smoke HTTP/MySQL y concurrencia; Actions `45030428079` volvió a pasar después del cambio documental.
- El envío de correo todavía no puede comprobarse con proveedor real mientras falten credenciales SMTP. La protección durable, el retry y el dead-letter sí quedaron probados.
- Pendientes de seguridad/plataforma: decidir si Admin delegado puede asignar Admin; habilitar SMTP y almacenamiento persistente/compartido de Data Protection keys en despliegue; mover evidencia JSON a object storage y rate limiting a almacenamiento distribuido antes de escalar.

## Correcciones de auditoría de seguridad — 2026-11-22

- `GET /api/Auth/test-email` sólo está disponible en Development. Incluso allí, el destinatario debe coincidir sin distinguir mayúsculas con el email autenticado; no se usa como relay de correo para terceros. El servicio de invitaciones y recuperación sigue intacto.
- `SimpleRateLimitMiddleware` incrementa el contador mediante `ConcurrentDictionary.AddOrUpdate`; entradas vencidas se remueven comparando el par exacto para no borrar una ventana recién renovada por una solicitud concurrente. El límite actual es 60/minuto por IP y proceso; para múltiples réplicas se necesita un rate limiter/almacenamiento distribuido.
- En Production el arranque exige `FLETFLOW_DATA_PROTECTION_KEYS_PATH` (volumen persistente) y `FLETFLOW_DATA_PROTECTION_CERT_PATH` (PFX que incluye clave privada); `FLETFLOW_DATA_PROTECTION_CERT_PASSWORD` suministra la contraseña si aplica. Data Protection cifra el key ring con el certificado. Nunca versionar el PFX ni su contraseña. En Development estas variables son opcionales.
- xUnit local: release **85/85**; build Release 0 warnings/0 errors; Actions integrado pendiente.

## Hallazgos de autorización aún abiertos

- La política de otorgamiento de Admin quedó definida: sólo cuenta raíz. El smoke HTTP/MySQL cubre creación/promoción permitidas desde raíz y las tres operaciones denegadas para Admin delegado, incluyendo ausencia de filas/cambios en SQL.
- La clave/contraseña PFX y el volumen persistente cifrado deben provisionarse por el entorno de despliegue; no hay material de producción disponible en el repositorio para validar su instalación.
- Verificación local fail-fast: con entorno `Production`, key ring configurado y certificado ausente, el proceso se negó a iniciar con mensaje explícito; no llegó a conectarse a DB ni creó core file en el repositorio.
- Validación remota `45037399332` sobre `94c3e90`: build limpio, 85/85 xUnit, aislamiento/roles y ciclo de flota HTTP/MySQL, smoke concurrente y cleanup correctos. La configuración productiva con certificado real continúa pendiente de secretos/infraestructura de despliegue.
- Nueva prueba con certificado autofirmado efímero confirma que el XML del key ring contiene `<encryptedSecret` y que un provider reiniciado puede descifrar payload con el mismo certificado. Suite Release actual: **86/86**, build limpio.

## Rotación de certificado Data Protection — 2026-11-22

- `FLETFLOW_DATA_PROTECTION_CERT_PATH` y su contraseña identifican el certificado actual; las claves nuevas se cifran con este PFX.
- Durante la transición, `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PATH` y `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PASSWORD` cargan el certificado anterior con clave privada para descifrar claves ya persistidas. Son opcionales cuando no hay rotación.
- No retirar ni destruir el certificado anterior mientras existan claves/payloads protegidos con él en el key ring. Los PFX y contraseñas se provisionan mediante secretos de despliegue y nunca se versionan. `.env.example` contiene únicamente nombres vacíos.
- La prueba de rotación conserva key ring y payload generados con el certificado anterior, abre un provider con actual + anterior, descifra el payload anterior y protege/descifra un payload nuevo.
- La regresión de rotación valida certificado anterior para lectura y certificado actual para claves nuevas; backend local Release **86/86** y build **0 warnings/0 errores**. Actions del nuevo commit pendiente.
- Actions `45040550853` validó Backend `b664b2c`: Release build, xUnit **86/86**, aislamiento HTTP/MySQL, concurrencia de asignación y cleanup success.


### Retiro del certificado Data Protection anterior

Si se conservó el mismo key ring cifrado con el certificado viejo, no quitar `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PATH` por tiempo transcurrido solamente: las entradas de claves viejas seguirán necesitando el PFX para poder leerse. La rotación de retiro requiere mantenimiento controlado:

1. Respaldar cifrados el key ring/DB y coordinar una ventana sin escritura de correo de recuperación.
2. Detener instancias/worker y eliminar/archivar los mensajes pendientes del outbox que no se enviarán; invalidar PasswordResetTokens activos para que ningún enlace antiguo se presente como válido. Los usuarios podrán solicitar uno nuevo.
3. Configurar un directorio persistente de key ring nuevo y el certificado nuevo como actual, sin variables `PREVIOUS_CERT`; iniciar una sola instancia y comprobar health + un flujo de recuperación de prueba de extremo a extremo.
4. Tras validar que ninguna instancia use el key ring viejo, retirar el PFX anterior del secreto activo. Si la política exige conservar la capacidad de restaurar el backup viejo, conservar PFX y backup cifrados bajo custodia separada; su destrucción vuelve irrecuperable ese key ring.

Este proceso invalida los emails/token de recuperación que estén pendientes o activos durante el cambio; debe coordinarse como operación de despliegue y confirmarse con el operador responsable antes de ejecutarse en producción. No hay credenciales PFX/infra productiva en local para ejecutar este runbook aquí.

## Hallazgo adicional — cuenta Driver enlazada y tokens de activación/reset

- Crear y editar rutas vuelven a bloquear y consultar la cuenta User enlazada después del lock de Vehicle/Driver/Route. Se evita asignar una ruta pendiente a una cuenta desactivada concurrentemente; la operación revierte con conflicto.
- Smokes HTTP/MySQL ejercitan activate-account y reset-password con tokens vigentes, consumo único, refresh previo revocado por reset y nuevo login. CI de esta tanda pendiente.

## Enumeración de cuentas en login — corregida localmente

- Auditoría confirmó que login distinguía correo inexistente (400/mensaje distinto) de contraseña incorrecta (401), y exponía estado pendiente de activación.
- Código ahora devuelve un mismo 401/mensaje para correo desconocido, password incorrecto, cuenta inactiva o pendiente. Para correo inexistente ejecuta BCrypt contra hash señuelo, con el objetivo de aproximar el coste criptográfico normal.
- xUnit y smoke HTTP/MySQL se añadieron; CI pendiente. La mitigación reduce diferencias observables pero no reemplaza una medición temporal bajo carga.
- Registro público también fue ajustado: email nuevo y duplicado devuelven el mismo 200/payload neutral. BCrypt corre antes de la consulta duplicada y el controlador aplica un piso de 500 ms a la respuesta. El frontend ya no auto-inicia sesión; solicita que el usuario ingrese sus credenciales en Login. Backend Action `45066082294` verificó respuestas iguales, piso >=450 ms en smoke y cleanup.
- La espera reduce enumeración temporal, pero la carga/latencia externa del almacenamiento y red no puede igualarse exactamente; no afirmar garantía de tiempo constante.
# Política para asignar Admin — 2026-11-23

- Sólo la cuenta raíz del tenant puede crear o promover otro usuario a Admin. Admin delegado recibe 403 al crear usuarios con rol Admin, actualizar rol mediante PUT o usar el alias POST de roles.
- La raíz se identifica con la identidad autenticada comparada contra el `owner_id` firmado; no se acepta un identificador de propietario enviado por el cliente como autoridad.
- Action `45088129188` validó los casos permitidos/denegados mediante HTTP real y verificó en MySQL que las denegaciones no creen cuentas ni roles parciales; cleanup confirma que no quedaron fixtures.
- `GET /Auth/me` publica `isRootAccount` derivado de la entidad autenticada sólo para presentación; la autorización de operaciones sigue validada en backend.

## Auditoría adicional — 2026-11-23 (registro histórico; decisiones posteriores aplicadas)

- Revisión read-only de JWT, rotación/revocación refresh, reset token, aislamiento por propietario y permisos Admin raíz no encontró bypass nuevo en los flujos inspeccionados. La evidencia de CI actual incluye root-only, 113 xUnit y smoke tenant.
- Hallazgo histórico de mínimo de seis caracteres: resuelto posteriormente; las contraseñas nuevas requieren 12 caracteres, con pruebas y Action indicada en PROJECT_STATUS.
- Hallazgo histórico de ausencia de límite específico de login: resuelto posteriormente con 10 logins/minuto/IP. El límite para login y recuperación sigue siendo en memoria y no coordina réplicas; recuperación ahora tiene 5 solicitudes/10 minutos/IP, según la sección vigente al inicio de este documento.
- La aplicación de rate limit distribuido y almacenamiento persistente de Data Protection siguen siendo requisitos para múltiples réplicas/producción.

## Endurecimiento de credenciales y login — 2026-11-23

- Decisión confirmada por el usuario: toda contraseña nueva debe tener al menos 12 caracteres en registro, cambio, activación y recuperación. Las claves ya existentes continúan funcionando hasta que el titular decida cambiarlas; no se fuerza restablecimiento masivo.
- `POST /api/Auth/login` aplica 10 intentos/minuto por IP además del límite general de 60 solicitudes/minuto. Devuelve 429 con `Retry-After`; no bloquea cuentas ni revela si un email existe.
- El contador es en memoria por proceso. Para varias réplicas debe migrarse a un limitador distribuido y configurar correctamente la IP cliente detrás de proxies confiables; no confiar en `X-Forwarded-For` sin una lista de proxies permitidos.
- xUnit local y Action `45096399172`: 120/120 para el endurecimiento de contraseñas/login. Cambios posteriores de recuperación/token/URLs tienen validación local Release 133/133; no se han publicado en esta rama.

## Validación HTTP de login por IP — 2026-11-23

- El rate limit de login es 10 intentos/minuto por dirección IP y no bloquea cuentas. El nuevo smoke usa la API Release en Staging y verifica el pipeline HTTP completo: primera IP obtiene 10 rechazos de credenciales normales, exceso recibe 429 + `Retry-After`, segunda IP puede iniciar sesión en la misma cuenta.
- Smoke local y Action `45126308692` comprobados contra DB MySQL 8 descartable. En producción el contador sigue en memoria por proceso; antes de escalar se requiere store distribuido y configurar trusted proxies/forwarded headers con lista de proxies permitidos.

- Action [`45126308692`](https://github.com/Paul1578/Back_GPS/actions/runs/45126308692), backend `88f669d`, ejecutó y aprobó el flujo HTTP Staging completo de login rate limit sobre MySQL descartable. Contador distribuido/proxy confiable siguen pendientes de infraestructura de producción.
