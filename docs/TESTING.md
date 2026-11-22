# Regla de evidencia por parada — 2026-11-22

- xUnit Release local **110/110**: Completing/Skipping sin evidencia se rechaza y preserva estado/evidencia; Skipping con nota de evidencia se permite y luego la ruta completa.
- Smoke API + MySQL 8 descartables pasó completo: omitir, completar parada y cerrar ruta sin evidencia devuelven HTTP 400; después de cada request se comparan ruta, `EndedAt`, JSON completo `PointsJson` y status de vehículo con snapshot previo. Camino válido posterior registra evidencia, completa ruta y libera vehículo.
- Cleanup confirmó cero rutas/posiciones y cero fixtures de Vehicle/Driver/tenant. `bash -n`, diff-check correctos.
- Action backend `45070491409` para `f564aa3` terminó `success`: build, suite xUnit **110/110**, smoke HTTP/MySQL y concurrencia; servicios descartables detenidos y cleanup correcto.

# Vínculo manual User/Driver y validaciones negativas CRUD — 2026-11-22

- Smoke API + MySQL 8 descartables: crea User (`POST /Users`, `roleName=User`), confirma `driverId=null`, cambia rol a Driver, crea perfil con `userId` y confirma el vínculo con GET Driver, `GET /Users/my-team` y SQL (`UserId`, tenant, documento y cardinalidad 1).
- PUT Vehicle inválido por año 2101/placa de 21; POST Driver inválido por nombre requerido/documento/teléfono de 21; PUT Driver inválido por apellido requerido/documento/teléfono de 21. Cada request responde 400 y SQL confirma sin mutación/alta parcial.
- Ejecución completa `SMOKE_COMPLETO_OK`, cleanup verificó **0 Driver/0 User** del vínculo manual, cero fixtures de Manager y tenant A/B; contenedor/API locales detenidos. `bash -n`/diff-check pasan.
- Action `45069402482` pasó build Release, xUnit **107/107**, aislamiento HTTP/MySQL, concurrencia y cleanup. La salida verifica vínculo manual persistido, 400 sin mutaciones y limpieza explícita `0 Driver/0 User`.
- No hay endpoint HTTP de desvinculación conservando cuenta y perfil; definición funcional solicitada.

# CRUD Driver como Manager con aislamiento de fixtures — validado local — 2026-11-22

- `scripts/smoke-tenant-http.sh` crea un Driver y Vehicle dedicados bajo el token Manager. `PUT /Drivers/{id}` edita el Driver (HTTP 200); `PUT /status` baja/reactiva (204); `PUT /assign-vehicle` y `/unassign-vehicle` asignan/desasignan (204).
- Después de cada operación, `GET /Drivers/{id}` y `GET /Vehicles/{id}` verifican el estado de API; SQL comprueba los campos del Driver (`FirstName`, `LastName`, `DocumentNumber`, `PhoneNumber`, `IsActive`, `VehicleId`, `OwnerUserId`) y Vehicle (`Plate`, `IsActive`, `Status`, `OwnerUserId`).
- Cada paso compara además snapshot SQL/API del `manager_driver`, `manager_vehicle` y `manager_route` preexistentes para asegurar que la matriz aislada no los modifica. El fixture dedicado no se usa en rutas ni escenarios Logistics.
- Cleanup elimina Driver/Vehicle dedicados y comprueba conteo `0:0`; el smoke completo contra API local + MySQL 8 descartable terminó exit 0. Comandos: `/home/leonardo/.dotnet/dotnet build BackEnd_Gps.csproj -c Release --no-restore -m:1`, `bash -n scripts/smoke-tenant-http.sh`, `git diff --check`, `bash scripts/smoke-tenant-http.sh`.
- Durante el desarrollo, una lectura de colección con `onlyActive=false` no incluyó Driver activo porque el parámetro filtra exactamente inactivos; se cambió a consulta por ID. Otra expectativa SQL utilizó owner antes de inicializarlo; el test ahora lo consulta por la cuenta Manager. Detalles en `ERROR_LOG.md`.
- No se modificó código de producto. `/home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore` pasó **82/82**; Actions aún pendiente para este diff. Rama sin commit/push, pendiente de revisión.

# Aislamiento Users y asignaciones A/B por HTTP/MySQL — validado local — 2026-11-22

- `GET /api/Users` y `GET /api/Users/my-team` con JWT de B devuelven HTTP 200, incluyen el miembro B y excluyen todos los IDs que MySQL identifica como pertenecientes a A (`Id` raíz o `OwnerUserId` de A). Consultas SQL antes/después confirman `OwnerUserId` intacto.
- `GET /api/Routes/assignment-options` para B incluye exactamente su Driver y cero vehículos; no incluye Drivers A.
- B intenta asignar y desasignar el Driver A, asignar un vehículo A a su propio Driver y reactivar un vehículo A inactivo. Los cuatro endpoints responden HTTP 404. Después de cada request, SQL compara `Drivers.VehicleId`, `Vehicles.IsActive` y propietarios contra baseline; no hay mutaciones.
- Comandos ejecutados: `/home/leonardo/.dotnet/dotnet build BackEnd_Gps.csproj -c Release --no-restore -m:1` (0 warnings/0 errors), `/home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore` (**82/82**), `bash -n scripts/smoke-tenant-http.sh`, `git diff --check` y `bash scripts/smoke-tenant-http.sh` con API local y MySQL 8 en contenedor/DB descartables (`fletflow_ci_*`). El smoke terminó exit 0.
- Cleanup SQL confirmó cero Drivers, vehículos y miembros temporales A/B; además cero rutas, posiciones GPS y vehículos Logistics/Manager. El contenedor y API locales se detuvieron. Se observaron avisos del cliente MariaDB por alias `mysql` y TLS desactivado en conexión local de prueba; no afectaron el resultado.
- Actions aún pendiente para este diff; la rama sigue sin commit/push, pendiente de revisión e integración.

# Validación de logout vinculado al principal — 2026-11-22

- `/home/leonardo/.dotnet/dotnet build BackEnd_Gps.sln --configuration Release --no-restore -m:1`: 0 warnings/0 errores.
- `dotnet test` Release: **99/99**. Dos casos nuevos verifican no revocar refresh ajeno y revocar refresh propio.
- `bash -n` en `smoke-tenant-http.sh`, `smoke-route-concurrency.sh`, `run-e2e-local.sh`, y `git diff --check`: correctos.
- Smokes HTTP/API/MySQL 8 efímeros: ambos exit 0. Tenant smoke ejercitó logout cross-account y logout propio; concurrency smoke verificó agenda/asignaciones/roles. Cleanup corroboró cero fixtures.
- Front integrado cross-repo: Playwright **33/33**, lint/build/audit (0 vulnerabilidades).
- Action `45058536309` sobre `22c2fb6` pasó Release build/xUnit, smoke tenant HTTP/MySQL, concurrencia y cleanup.

# Smoke HTTP negativo de validación Vehicle/Driver — 2026-11-22

- `bash -n scripts/smoke-tenant-http.sh` y `git diff --check`: correctos.
- Smoke tenant completo en API local con MySQL 8 descartable: exit 0 (`SMOKE_COMPLETO_OK`); cleanup confirmó cero fixtures y se cerraron contenedor/API.
- `PUT /Vehicles/{id}`: año 2101 y placa de 21 caracteres devuelven 400; snapshot de placa, datos, activo y estado queda idéntico en MySQL.
- `POST /Drivers`: nombres requeridos vacíos, documento de 21 y teléfono de 21 caracteres devuelven 400 y no crean Driver.
- `PUT /Drivers/{id}`: apellido requerido vacío, documento largo y teléfono largo devuelven 400; fila completa, asignaciones y estado quedan idénticos.
- La primera tentativa de arranque falló con el host global incompleto; repetición con `/home/leonardo/.dotnet/dotnet` pasó. Detalle en `ERROR_LOG.md`. Action de CI pendiente.

# Pruebas backend

## Edición de perfil con scope de tenant — 2026-11-22

- Tests nuevos de `UpdateUserProfileCommand`: sincroniza username/nombres con Driver dentro del tenant; rechaza un usuario perteneciente a otro propietario. Release local **64/64** (SDK 8.0.428).
- `bash -n scripts/smoke-tenant-http.sh` y `git diff --check` pasan.
- Smoke HTTP amplía validación de máximos de 100 caracteres para nombre y apellido. Actions `45007491905`: Release build, **64/64** xUnit, smoke tenant HTTP/MySQL y concurrencia; cleanup de servicios descartables correcto.

## Suite automatizada

Proyecto: `BackEnd_Gps.Tests` (xUnit, .NET 8), agregado a `BackEnd_Gps.sln`.

Cobertura agregada:

- Registro público: el request solicita `Driver`, pero el controlador asigna la cuenta raíz `Admin`; el JWT debe usar su ID como `owner_id`, permitir consultar su flota vacía y negar acceso a un vehículo de otro tenant.
- `GET /api/Auth/test-email`: requiere el rol `Admin`.
- `RoutePoint`: preserva operaciones (ninguna, recogida, entrega y ambas), estados, coordenadas y nombre normalizado.
- `RoutePoint`: rechaza latitudes y longitudes fuera de límites.
- `RoutePosition`: valida coordenadas finitas, velocidad entre 0 y 300 km/h y rumbo entre 0 y menos de 360 grados.
- Contrato `RoutePointRequest`: serializa y recupera una parada de recogida y entrega.
- Dominio `RouteE`: cierre exige estados y evidencia; transiciones operativas válidas y formatos de evidencia permitidos.
- Ciclo de vida de rutas: inicio y cierre conservan tiempos reales, y asignación rechaza vehículo en mantenimiento.

La única costura de producción es `IUserRegistrationService`, usada para aislar el límite de registro en la prueba del controlador. `AuthService` la implementa y DI la resuelve al mismo servicio registrado. No se cambió la lógica de dominio para facilitar las pruebas.

## Comando y resultado

Repetición de cierre (2026-11-21):

```sh
DOTNET_ROOT=/home/leonardo/.dotnet /home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1
```

Resultado actualizado: `Passed: 47, Failed: 0, Skipped: 0`. Smoke local: Swagger HTTP 200; recurso protegido sin token HTTP 401. Frontend Playwright autenticado 5/5 (detalles en `Front_gpsR/docs/TESTING.md`). MySQL: 22 migraciones y conteos de fixtures E2E en cero. La verificación fallida inicial usó `IsRevoked`; el esquema real es `RevokedAt`, corregido y vuelto a verificar (registrado en `ERROR_LOG.md`).

Las dos pruebas de `SimpleRateLimitMiddleware` verifican que el límite de 60 por minuto sigue activo en Production y que Development no limita el tráfico loopback local.

Ejecutado desde la raíz:

```sh
/home/leonardo/.local/share/dotnet/dotnet test BackEnd_Gps.sln
```

Resultado histórico previo al aislamiento por tenant: `Passed: 22, Failed: 0, Skipped: 0`. La referencia EF Relational se alineó después a 9.0.10 y eliminó MSB3277.

## Verificación funcional HTTP y base local

- `docker compose ps`: MySQL 8 y phpMyAdmin en estado Up.
- `dotnet ef migrations list`: 21 migraciones enumeradas; `TenantFleetOwnership` aplicada.
- Arranque API corregida en puerto local 5048: EF Core informó “No migrations were applied. The database is already up to date.”
- Flujo real en MySQL/API: registro anónimo con intento de solicitar rol Admin respondió 200; login respondió 200; `/api/Auth/me` respondió 200 con rol `User`; `/api/Routes` respondió 403; `/api/Auth/test-email` respondió 403.
- El usuario de prueba identificado como `fletflow.qa.20261120.2154@example.test` fue eliminado al terminar; verificación SQL final dejó 0 filas.
- El CRUD admin y la prueba HTTP multiempresa se completaron en la verificación final descrita abajo.

## Errores encontrados y correcciones

1. **Las fuentes del test se compilaban dentro del proyecto web.** El proyecto SDK incluye por defecto archivos `*.cs` en subdirectorios. La primera ejecución reportó que `Fact`, `Theory` e `InlineData` no existían al compilar `BackEnd_Gps.csproj`. Se corrigió excluyendo `BackEnd_Gps.Tests/**/*.cs` de la compilación del proyecto web.
2. **Faltaba importar xUnit en el proyecto de pruebas.** Tras aislar el directorio, el proyecto de pruebas reportó los mismos atributos sin resolver. Se agregó `Usings.cs` con `global using Xunit;`.
3. **Conflicto de versión transitiva de EF Relational en la compilación de pruebas — resuelto.** Pomelo 9.0.0 permite EF Relational 9.0.x. Se fijó 9.0.10 en el proyecto de aplicación; la advertencia MSB3277 desapareció.
4. **Bloqueo temporal al copiar recursos del testhost.** Una ejecución anterior reportó reintentos `MSB3026` indicando archivos ocupados. La repetición posterior se ejecutó secuencialmente y sin esos reintentos; ver resultado final arriba.
5. **Mapeo de un rol ausente.** El mapper asignaba `null` a un rol declarado obligatorio y producía CS8601. Ahora falla con un error explícito si el repositorio no cargó la navegación requerida.

## Verificaciones manuales previas

- `/home/leonardo/.local/share/dotnet/dotnet restore`: correcto.
- `/home/leonardo/.local/share/dotnet/dotnet build --no-restore`: compilación correcta; la corrección de CS8601 recién aplicada requiere otra ejecución final.
- Swagger en `http://127.0.0.1:5046/swagger/index.html`: HTTP 200.
- Sin token, `/api/Routes`, `/api/Vehicles`, `/api/Auth/me` y `/api/Auth/test-email`: HTTP 401.

## Repetición final 2026-11-20

- `dotnet build BackEnd_Gps.csproj --no-restore -m:1`: correcto, 0 advertencias, 0 errores.
- `dotnet test BackEnd_Gps.Tests/BackEnd_Gps.Tests.csproj --no-restore -m:1`: 25 aprobadas, 0 fallidas, 0 omitidas, sin advertencias después de alinear EF Relational a 9.0.10 y añadir aislamiento por tenant.
- API Development `5048`: `/swagger/index.html` HTTP 200; `/api/Routes` sin token HTTP 401.
- La primera comprobación se hizo con entorno Production, en el que Swagger está deshabilitado por diseño y devolvió 404. Se corrigió la verificación reiniciando en Development; no era un fallo de API.
- Frontend independiente: build y lint correctos, dashboard HTTP 200, `npm audit` con 0 vulnerabilidades.
- Suite final: 25/25.
- Migración `TenantFleetOwnership`: aplicada a MySQL; `dotnet ef migrations list` la muestra aplicada. El vehículo histórico quedó con propietario y la base conserva sus filas.
- E2E MySQL/API: dos cuentas Admin raíz existentes. Empresa A vio 1 vehículo, empresa B 0; la lectura cruzada por ID devolvió 404.
- E2E MySQL/API con filas QA: crear vehículo, conductor y ruta de tres puntos (recogida y entrega), iniciar, registrar/leer GPS, completar y comprobar vehículo `Available`. Aprobado; se eliminaron los datos temporales y la verificación posterior dio 0 coincidencias.
- En el flujo HTTP, los JWT de test se firmaron localmente con la clave configurada y claims de Admin existentes; no se probó el login por contraseña de Admin en este flujo. El login público y restricción de rol se probaron por separado.
- Flujo pausa/reanudación HTTP/MySQL: iniciar puso vehículo en `OnRoute`, pausar lo devolvió a `Available`, reanudar fue aceptado y completar lo dejó `Available`. Filas de prueba borradas y conteo final cero.
- Crear cuenta Driver por `/api/Users` usó documento/teléfono de request, produjo un solo Driver, reportó `invitationSent=false` al fallar SMTP y rechazó documento duplicado. Filas de usuario/driver/token QA borradas; conteo final cero.
- Login Admin por contraseña: `/Auth/me` confirma Admin, `/Users/my-team` responde y clave incorrecta da 401. La cuenta temporal se habilitó en SQL porque no hay SMTP y después se borraron todos los datos/tokens.

- `dotnet test BackEnd_Gps.sln --no-restore`: 35/35 aprobadas, 0 omitidas.
- `npm run lint && npm run build`: correcto; ESLint sin diagnósticos, build Next 16.4.6 completo.
- E2E con API/EF/MySQL: creó vehículo, conductor y ruta de cuatro puntos; comprobó que el estado enviado por el cliente se reinicia, bloqueo de cierre sin evidencia, recogida/entrega, lectura posterior de notas persistidas, GPS y cierre exitoso.
- Se confirmó limpieza: cero rutas, conductores o vehículos con el identificador de la prueba.
- `/dashboard` HTTP 200; Swagger HTTP 200; proxy de endpoints protegidos responde 401 sin sesión.
- E2E real en Chrome: login Admin, completar pasos 1–4 del constructor, colocar origen/destino/parada con clicks del mapa, elegir `PickupAndDelivery`, asignar vehículo y conductor, revisar y crear. La ruta apareció en Gestión de Rutas; lectura autenticada de `/api/Routes` confirmó 3 puntos y operaciones `[None, PickupAndDelivery, None]` persistidas. Se limpió la ruta, los recursos y las cuentas QA; conteos finales en MySQL: cero.
- Vista del mapa en el constructor confirmada con marcadores A, B y 1, y selección de coordenadas al hacer clic; la ruta de navegador se creó de punta a punta.

## Errores de QA corregidos durante la validación final

- El script de lectura de configuración usó temporalmente el nombre de codec `utf-utf-8-sig`, que Python no reconoce; se corrigió a `utf-8-sig`.
- El primer dato QA usó una placa de más de 20 caracteres y MySQL rechazó la inserción; se reemplazó por una placa corta válida y se verificó limpieza de filas.
- El primer fixture E2E envió sólo las paradas intermedias pese a que el contrato de rutas recibe la lista completa con origen y destino; el fixture se ajustó al contrato y verificó los cuatro puntos.
- Al editar rutas, el frontend omitía la operación recogida/entrega en los puntos; se corrigió el payload para conservarla.
- El primer SQL de limpieza de la prueba de navegador usó la columna inexistente `Vehicles.PlateNumber` y se detuvo al llegar a esa sentencia. Se corrigió a `Vehicles.Plate`, se repitió la limpieza y se verificaron cero rutas, vehículos, conductores y usuarios temporales restantes.
- Chrome Admin CRUD: creó y editó vehículo, creó cuenta Driver con documento/teléfono, UI advirtió correctamente que SMTP no pudo enviar la invitación, y asignó/desasignó vehículo. La primera desasignación reveló HTTP 415; con la corrección del middleware se repitió y terminó mostrando `Sin vehículo`. Todas las filas QA fueron eliminadas; conteos finales en cero.

## Registro público como Admin raíz aislado — 2026-11-22

- El contrato previo de registro como `User` fue reemplazado: la UI promete el alta Gerente, y el servidor crea la cuenta raíz `Admin` fija de una empresa nueva, aunque el request incluya `roleName=Driver`.
- El smoke HTTP/MySQL comprueba rol único Admin, `OwnerUserId IS NULL`, claim JWT `owner_id` igual al ID recién creado, acceso 200 a flota vacía, listado vacío después de crear vehículo en otro tenant y 404 al consultar su ID directamente.
- El runner de dos tenants ya no modifica `UserRoles` manualmente. Backend Release local 61/61; `bash -n` y `git diff --check` correctos.
- Actions `44999697740` para `3620cc2` finalizó success: Release build, xUnit 61/61, smoke HTTP/MySQL de tenant/registro y smoke de concurrencia. El token y los recursos del job usan MySQL descartable; cleanup/stop containers concluyó success.

## Límites de usuario cross-tenant — 2026-11-22

- El smoke intenta desde tenant B desactivar y cambiar rol (PUT y alias POST) a un miembro de A; espera 404 y luego consulta MySQL para confirmar que sigue activo con rol User.
- Como propietario raíz, Admin A intenta cambiar su estado y rol mediante ambas rutas; espera 400 y MySQL debe conservar `IsActive=1` y rol Admin.
- Actions `45000893415` para `a5dbd4d` pasó con API/MySQL descartables: Release build, xUnit 61/61, tenant/roles/propietario, concurrencia y cleanup success.
- La suite ahora contiene 33 pruebas: las 30 anteriores más tres para cabeceras HTTP con y sin cuerpo.
- Role QA real MySQL/API: Logistics puede listar rutas (200) y su DELETE con ID inexistente llegó a dominio (404); User recibe 403. Se corrigieron el seeder que omitía Logistics y policies que nombraban Dispatcher. Datos QA borrados, cero usuarios temporales.
- Suite final: 35 pruebas (30 base + 3 middleware + autorización de rutas + seeding de roles idempotente).
- Actualizaciones posteriores: suite 40/40; incluye clave temporal, edición/desactivación de conductor, bloqueo de baja durante ruta activa y cambio transaccional de rol.
- `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive`: sin paquetes vulnerables tras actualizar MySqlConnector 2.6.2.
- Verificación final de esta fase: `dotnet test BackEnd_Gps.sln --configuration Release --no-restore` pasó 45/45; npm lint/build/audit pasaron con 0 vulnerabilidades; DB `fletflow` tiene 22 migraciones, 0 usuarios multirol, 0 refresh tokens vigentes y 0 filas QA de rutas/vehículos/conductores.
- Error de diagnóstico corregido: las primeras consultas usaron el nombre de base `BackEndGps` (no existe) y una columna `IsRevoked` que el esquema no tiene. Se consultó `fletflow` y se usó `RevokedAt IS NULL`; las consultas fallidas fueron de solo lectura.
- No se repitió E2E visual autenticado tras rotar JWT por falta de credenciales QA. Sí se completó API/MySQL autenticado con tenant temporal: registro ignoró rol Admin solicitado, login/perfil Admin 200, crear/listar vehículo 201/200, cross-tenant 404 con listado aislado y rol User 403. Usuarios, sesiones y vehículo se eliminaron al final.
- `dotnet build BackEnd_Gps.sln --configuration Release --no-restore`: correcto, 0 advertencias y 0 errores.
- Perfiles Driver en Chrome aún requieren E2E de edición, baja, filtro de inactivos y reactivación; reglas y endpoint ya compilan y tienen pruebas automatizadas.
- Driver E2E visual/API/MySQL: login, ruta propia, iniciar ruta desde UI, parada recogida+entrega, guardar y recargar evidencia, completar parada y ruta. Verificación API: ruta `Completed`, punto operativo `Completed` con evidencia y vehículo `Available`.
- Aislamiento entre conductores del mismo tenant: segundo Driver obtuvo lista vacía, 403 en detalle y 404 al cambiar estado de ruta ajena; el Driver asignado tiene acceso.
- Driver móvil 390 px: placa/modelo del vehículo aparecen desde el resumen de ruta, el mapa informa `Sin ubicación GPS`, y document/body scroll width = viewport. El resumen usa `GetByIdsAsync` sobre IDs distintos, sin consulta por ruta.

Las pruebas tenant automatizadas usan EF Core InMemory; verificación HTTP/MySQL comprueba IDOR. Chrome cubrió flujos representativos Admin/Driver/User/Logistics y CRUD de vehículo; faltan otros casos de borde de CRUD y roles.

## Smoke HTTP repetible

Repetición de cierre 2026-11-21: `dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1` aprobó 45/45 (0 fallidas, 0 omitidas). `git diff --check` limpio; frontend hermano volvió a pasar lint/build/audit y reportó 0 vulnerabilidades.

Chrome/MySQL de cierre: frontend Playwright pasó 2/2 (login negativo y CRUD Admin de vehículo en 390×844); el temporal Admin y su vehículo se eliminaron físicamente. Swagger se verificó luego con API Development sin bootstrap (200); base mantiene 22 migraciones.

Prueba UI adicional de ruta/roles: creó vehículo y Driver de equipo enlazado mediante API, luego la ruta E2EQA; editó en Gestión de Rutas y confirmó por API que `PickupAndDelivery` se conservó y que el nombre del Driver aparece en revisión. El runner provisionó User temporal; Chrome comprobó controles ocultos y 403 en Vehicles/Routes. Suite autenticada Playwright 4/4. `scripts/run-e2e-local.sh` limita web/API a localhost y limpia Admin/User/Driver/ruta/conductor/vehículo; verificación final: cero filas QA y cero refresh tokens activos.

Verificación posterior al endurecimiento del runner: `bash -n scripts/run-e2e-local.sh` pasa; sin `FLETFLOW_E2E_ALLOW_MUTATIONS=true` termina con código 2 antes de tocar la base. El suite directo sin credenciales ejecuta el login negativo (1 aprobado) y omite los 3 flujos con mutaciones; runner autenticado ejecutó 4/4 contra MySQL real.

`scripts/smoke-http.sh` prueba login Admin y CRUD de vehículo sobre la API/MySQL activa. Requiere `curl`, `jq`, `FLETFLOW_ADMIN_EMAIL` y `FLETFLOW_ADMIN_PASSWORD`; crea una placa única y limpia el vehículo mediante `trap`.

Ejecución real en esta sesión: login Admin temporal, crear/leer/editar vehículo, cambiar estado y eliminar; todas las respuestas esperadas y cleanup SQL comprobado en cero. La cuenta QA temporal y el vehículo se borraron. La contraseña se generó en memoria y no se imprimió.

También se probó directamente contra API/MySQL el ciclo de Driver: crear, leer, actualizar, dar de baja y reactivar. Se borraron físicamente el Driver y el usuario QA; conteo final cero.

Chrome E2E 2026-11-21: login Admin, edición de conductor y persistencia consultada por API, baja, filtro de inactivos, reactivación persistida y viewport 390×844 sin overflow. User: dashboard explica permisos ausentes, oculta Equipo/Rutas y API Vehicles/Routes devuelve 403.

Chrome E2E Logistics: un Manager del tenant cambió a Logistics; el usuario pudo abrir Rutas, API Routes devolvió 200, Equipo/Vehículos no aparecen y Vehicles devuelve 403. La invitación temporal se activó sólo para el test por SMTP no disponible; fixture eliminado.

Chrome E2E Vehicle: crear, buscar, estado vacío, editar y verificar API; eliminación lógica devuelve 200 con `isActive=false`, el listado `onlyActive=true` ya no contiene el vehículo. Fixture eliminado.

La invitación envía sólo el enlace de activación; el usuario fija su contraseña allí y el correo no transmite clave temporal.

## Verificación final 2026-11-21

- `DOTNET_ROOT=/home/leonardo/.dotnet dotnet test BackEnd_Gps.sln --no-restore`: 43/43 aprobadas.
- `DOTNET_ROOT=/home/leonardo/.dotnet dotnet build BackEnd_Gps.sln -c Release --no-restore`: 0 warnings, 0 errors.
- La prueba `CreateRouteCommandTests` confirma que ruta y vehículo del conductor quedan persistidos por la misma unidad de trabajo.
- Frontend: `npm run lint`, `npm run build`, `npm audit --audit-level=low` (0 vulnerabilidades).
- API Development Swagger 200, `/api/Routes` sin JWT 401; web `/dashboard` 200.
- Compose recreó MySQL conservando volumen; 21 migraciones presentes y cero filas QA en Vehicles, Drivers y Routes.
- `scripts/smoke-http.sh` pasa `bash -n`; no se repitió ese script porque requiere credenciales Admin QA persistentes. Se hizo un smoke equivalente con cuentas efímeras y cleanup documentado arriba.

## Cobertura añadida — 2026-11-21

- `dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1`: **48/48**, 0 fallidas, 0 omitidas.
- EF InMemory verifica aislamiento de Vehicles, Drivers, Routes y RoutePositions entre dos propietarios y relaciones de ruta/posición.
- `RouteAuthorizationTests` comprueba roles de controllers Vehicles y Drivers (Admin/Manager) y Users (Admin); Routes mantiene verificaciones de roles a nivel de acción.
- Auditoría independiente: falta matriz completa de HTTP real por rol y pruebas de mutación cross-tenant para rutas, conductores y posiciones; la suite de roles añadida valida metadatos de autorización, no sustituye E2E API.
- Frontend hermano confirmó Chrome/API/MySQL autenticado 7/7 y limpieza de fixtures.

## E2E HTTP/MySQL cross-tenant — 2026-11-21

- Dos Admin raíz con propietarios independientes: B no encuentra en listados vehículos, conductores o rutas de A.
- Lectura/actualización/baja de vehículo, lectura/actualización/estado de Driver y lectura/edición/estado/baja de Route ajenos responden 404.
- B no puede crear ruta con vehículo/Driver de A. Para GPS, la ruta A se inició y se persistió una posición: A recibe una fila, B obtiene `200 []` por filtro y el POST de posición por B devuelve 404.
- Playwright autenticado pasó 8/8. Cleanup eliminó ambos Admin temporales, los recursos y refresh tokens.
- El GET de colección GPS responde 200 con cero elementos cuando la ruta es de otro tenant; no expone el ID ni las posiciones. La prueba espera el contrato de colección vacía, no 404.

## API/tenant en CI con MySQL — 2026-11-21

- El workflow backend ahora levanta MySQL 8.0 como service, crea credenciales aleatorias por ejecución, inicia API con bootstrap Admin en DB `fletflow_ci` y corre `scripts/smoke-tenant-http.sh` después de xUnit.
- El smoke valida aislamiento real de Vehicles, Drivers, Routes y RoutePositions mediante dos Admin raíz: listados, lecturas/escrituras cruzadas, asociaciones de ruta y GPS. Sólo acepta API loopback y DB `fletflow_ci*`; limpieza usa IDs/valores de fixture, no prefijos globales.
- Validación local equivalente: API compilada, MySQL y DB nueva `fletflow_ci_validation_02`; aplicó 22 migraciones, smoke pasó, SQL confirmó cero fixtures, DB temporal descartada.
- Se corrigió precedencia de configuración: `Env.NoClobber().Load()` preserva valores de CI sobre `.env`. El primer intento conectó a `fletflow` y creó sólo un Admin QA sin sesiones/recursos; se borró por email exacto y verificó cero.
- El workflow remoto no se ejecutó desde este checkout; las ramas feature locales no existen aún en `origin`. El job y el smoke sí pasaron sus verificaciones locales equivalentes.
- El smoke de backend también crea un usuario temporal con rol `User`: HTTP devuelve 403 en `GET /Vehicles`, `POST /Vehicles` y `GET /Routes`. Validado contra otra DB MySQL descartable; cleanup automático completado.

## E2E frontend integrado en Actions — 2026-11-21

- `Front_gpsR/.github/workflows/ci.yml` provisiona MySQL y checkout público de este backend en `chore/stabilize-fleet-platform`; compila/prueba backend y frontend y ejecuta los ocho casos Playwright autenticados con fixtures desechables.
- `scripts/run-e2e-local.sh` se hizo portable a GitHub Actions: cliente MySQL del host, credenciales por entorno y validación de DB `fletflow_ci*` en modo CI.
- Repetición local autenticada en Chrome/API/MySQL: **8/8**, cleanup completo y base temporal descartada. Actions remoto no se ha ejecutado aún; requiere PR/branch publicados.
- Actualización remota: backend PR #5 pasó todas las fases (48 tests y smoke HTTP/MySQL). Frontend PR #2 pasó lint/build/audit y responsive no mutante, pero el primer E2E abortó porque el runner exigió Next antes de que Playwright iniciara `webServer`; corrección ya publicada, falta nuevo resultado.

## Protección de asignaciones concurrentes — 2026-11-21

- `dotnet test BackEnd_Gps.sln -c Release --no-restore -m:1`: **50/50**, 0 fallidas, 0 omitidas.
- `dotnet build BackEnd_Gps.sln -c Release --no-restore -m:1`: 0 warnings, 0 errores.
- `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive`: sin paquetes vulnerables; `SQLitePCLRaw.lib.e_sqlite3` resuelve a 2.1.14.
- Sobre MySQL/API local con DB temporal `fletflow_ci_validation_14`, smoke tenant pasó. El smoke de concurrencia comprobó Create con vehículo repetido, Create con Driver repetido, Update con ambos recursos repetidos, dos inicios del mismo vehículo y carrera inicio/desactivación; cada par devolvió un éxito y un HTTP 409. Los asserts MySQL verifican las rutas/vehículos persistidos y el estado Driver/vehículo coherente.
- Cleanup detuvo la API y descartó el esquema de validación. Ambos scripts pasan `bash -n`; `git diff --check` limpio.
- Al corte de la verificación local, Actions debía ejecutar la rama actualizada; el resultado remoto siguiente quedó registrado abajo.
- Resultado remoto posterior: Actions backend run `44976384961`, commit `90da59a`, pasó todas las fases, incluyendo 50 xUnit y los smokes MySQL tenant/concurrencia.

## Smoke de sesiones, bajas operativas y concurrencia de paradas — 2026-11-21

- `dotnet test BackEnd_Gps.sln -c Release --no-restore -m:1`: **50/50**; Release build: 0 warnings, 0 errores.
- `smoke-tenant-http.sh`: aislamiento de recursos, alta/login temporal, desactivación de Manager; access viejo y refresh viejo responden HTTP 401.
- `smoke-route-concurrency.sh`: parejas concurrentes de creación/edición/inicio producen éxitos/conflictos esperados; confirma SQL de estados. Rechaza inicio con Vehicle inactivo, baja Route en progreso y desasignación Driver activo; acepta operaciones tras cancelar. Ciclo de baja/reactivación mantiene estado visible por API. Dos actualizaciones paralelas de estados de paradas quedan persistidas simultáneamente.
- La ruta valida además exclusividad de asignación Vehicle/Driver tras adquirir los locks. xUnit SQLite prueba conflicto de creación y edición con persistencia intacta; el smoke HTTP/MySQL verifica ambos 409 y conserva la relación previa (Actions por ejecutar).
- Actions `45032172895` validó la exclusividad en backend `8e76f80`: Release build, xUnit **82/82**, smoke de tenant y smoke de concurrencia HTTP/MySQL correctos; cleanup completo.
- Ejecución local final con DB descartable: ambos scripts pasaron y la trampa eliminó el schema temporal. `bash -n` y `git diff --check` correctos.

- Ampliación de autorización real del smoke: Manager HTTP 200 en listados Vehicle/Driver/Route; Logistics HTTP 200 en Routes y HTTP 403 en Vehicles/Drivers; User 403 en flota/rutas; Driver se verifica en E2E autenticado. `smoke-tenant-http.sh` y `smoke-route-concurrency.sh` volvieron a pasar juntos en `fletflow_ci_validation_roles20261121`, DB descartada. Actions remoto para este lote aún pendiente.

## Cambio de rol, sesiones y estados concurrentes — 2026-11-21

- `dotnet test BackEnd_Gps.sln --configuration Release --no-restore`: **50/50**; `dotnet list ... package --vulnerable --include-transitive`: ningún paquete vulnerable.
- `smoke-tenant-http.sh`: User/Manager/Logistics, aislamiento de tenant y cambio Logistics→User; access/refresh anteriores devuelven 401.
- `smoke-route-concurrency.sh`: carreras de inicio/cambio Driver→Logistics y de estados de paradas; MySQL comprobó los estados que quedaron persistidos. Reactivación con ruta activa devuelve 409.
- Ambas pruebas pasaron con API/MySQL de esquema temporal. Frontend autenticado Playwright **14/14** en Chrome/API/MySQL real. Limpieza y descarte de schema confirmados.
- Build Release final 0 warnings/0 errors y audit NuGet sin vulnerables. Nuevos runs GitHub Actions pendientes; CI verde previo corresponde a commits anteriores.
- Nuevo smoke de escritura MySQL aprobado: Manager crea Vehicle/Driver/Route y edita Route; Logistics edita/elimina Route y recibe 403 en Vehicle/Driver; User y Driver reciben 403 al crear Route. Esquema temporal y fixtures eliminados.
- Verificación histórica anterior al cambio de onboarding: registro anónimo que pide Admin terminaba como User y recibía 403 en Vehicles. En la validación actual ese contrato se reemplaza por Admin raíz de tenant; `logout-all` sigue respondiendo 200 y el refresh invalidado devuelve 401.

## Reset y activación — 2026-11-21

- `PasswordRecoveryCommandTests`: cuatro pruebas con BCrypt real y repositorios controlados; comprueba cambio de hash, token de un uso, revocación de sesiones, limpieza de `MustChangePassword` y rechazos de token usado/confirmación distinta.
- Suite completa `dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1`: **54/54**, sin fallos ni omisiones. No se envió SMTP.
- En este entorno usar `/home/leonardo/.dotnet/dotnet`.

## Cambio de contraseña revoca JWT antiguos — 2026-11-21

- Smoke HTTP agrega cambio de contraseña autenticado: access token y refresh previos deben dar 401, contraseña anterior debe fallar y la nueva debe iniciar sesión.
- `PasswordRecoveryCommandTests` comprueba que reset/activación incrementan `TokenVersion` junto con hash, uso de token y revocación refresh.
- Suite local Release: 54/54; build 0 warnings/0 errores; `bash -n scripts/smoke-tenant-http.sh` correcto. Integración contra MySQL queda en Actions después del push.

- Resultado remoto: Actions `44983337062` success. MySQL descartable aplicó la migración; smoke HTTP probó cambio de contraseña, revocación inmediata de access y refresh, rechazo de la clave anterior y aceptación de la nueva.

- Ampliación xUnit: `ChangePassword_ChangesHashAndInvalidatesExistingSessions` comprueba BCrypt, versión, revocación refresh y commit; `ChangePassword_RejectsWrongCurrentPasswordWithoutChangingState` comprueba que un current password incorrecto no modifica hash/versión/sesiones.
- Suite actual Release: **56/56**. La ampliación de tests está pendiente de Actions; smoke HTTP/MySQL anterior del flujo completo pasó en `44983337062`.

- Resultado remoto final de esta iteración: run `44983763612` success; xUnit **56/56**, smoke HTTP tenant/autorización/sesiones y smoke concurrente vía API/MySQL pasaron. DB disposable y contenedores se detuvieron al finalizar.

## Invalidación por cambio de rol — 2026-11-21

- `dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1`: **57/57** aprobadas.
- `dotnet build BackEnd_Gps.sln --configuration Release --no-restore -m:1`: 0 warnings, 0 errores.
- `bash -n scripts/smoke-tenant-http.sh` y `git diff --check`: correctos.
- `smoke-tenant-http.sh` con API local y MySQL 8 en contenedor temporal, DB `fletflow_ci_*`: aprobado. Logistics→User→Logistics mantuvo 401 para access/refresh originales. El contenedor se eliminó al terminar.
- El primer intento no completó porque faltaba soporte para puerto MySQL configurable; la conexión al MySQL local falló por credenciales antes de ejecutar SQL. Se documentó el incidente y se añadió `MYSQL_PORT`; la repetición aislada pasó.

## Revocación de JWT tras cambio y restauración de rol — CI 2026-11-22

- Actions `44986173700` para `3f3b004`: Release 57/57, build sin warnings y smokes reales HTTP/MySQL de tenant/roles y concurrencia en verde. El ciclo Logistics→User→Logistics mantuvo access/refresh originales en 401.
- La base y el contenedor temporales se eliminaron al completar la verificación.

## Creación de rutas por Logistics — 2026-11-22

- `bash -n scripts/smoke-tenant-http.sh` y `git diff --check`: correctos.
- Sólo se ejecutó `smoke-tenant-http.sh` contra API local temporal + MySQL 8 en contenedor efímero, con esquema `fletflow_ci_*`. Manager creó/editó y dio de baja su ruta antes de liberar la agenda compartida; Logistics creó otra ruta con los mismos fixtures (HTTP 201), GET confirmó ID/nombre/vehículo/conductor/paradas persistidos, PUT devolvió 200 y un segundo GET confirmó el nombre actualizado, DELETE devolvió 204.
- Primer intento: el flujo HTTP pasó, pero falló cleanup porque el ID de la ruta Logistics sobrescribió el de Manager, cuya baja es lógica. Se corrigió guardando ambos IDs y limpiando las dos rutas. La repetición terminó con código 0 y cleanup sin errores; contenedor y DB desechables eliminados. No se usó la DB local.
- CI confirmado: Actions `44988756481` para `43ca30c` terminó success. Release build, xUnit y los smokes tenant/roles, rutas por Logistics y concurrencia HTTP/MySQL pasaron; el job detuvo los contenedores al finalizar.

## Ciclo operativo de ruta como Logistics — 2026-11-22

- Validación local completa con `bash scripts/smoke-tenant-http.sh`, `bash -n scripts/smoke-tenant-http.sh`, `git diff --check` y Release build (0 warnings/0 errores). API `127.0.0.1:5058`, MySQL 8 en Docker/13306 y esquema descartable `fletflow_ci_lifecycle_*`; no se usó DB local.
- Como Logistics: crear ruta HTTP 201; leer/editar 200; iniciar 204; iniciar/cerrar parada PickupAndDelivery, agregar evidencia y escribir/leer posición GPS 200; cerrar ruta 204. GET confirmó `Completed`, tiempos `StartedAt`/`EndedAt`, operación/evidencia/GPS persistidos y vehículo de vuelta a `Available`.
- Cleanup confirmó 0 rutas, 0 posiciones GPS y 0 vehículos QA; contenedor y API temporal detenidos.
- Primer intento: las operaciones pasaron, pero el assert de cleanup incluyó un warning MySQL en stdout y dio un falso conteo. Se redirigió stderr por separado y la repetición pasó; detalle en `ERROR_LOG.md`.
- Actions `44990525889` para `d58c5ab` pasó el smoke ampliado y xUnit **57/57**; el log confirma operación Logistics y cleanup `0:0:0`.
- Tras la revisión se añadió fallback de cleanup por nombre QA. La repetición local posterior, con Release build 0 warnings y MySQL 8 desechable, pasó; el código actualizado aún necesita su propia ejecución de Actions.
- CI del código actualizado: Actions `44991032520` para `da53081` success; Release build, xUnit 57/57, smoke tenant/roles/Logistics y concurrencia HTTP/MySQL. El smoke de Logistics volvió a confirmar `0:0:0`; el caso excepcional de ID ausente no se indujo.

## Conflicto de placa duplicada — 2026-11-22

- `DOTNET_ROOT=/home/leonardo/.dotnet PATH=/home/leonardo/.dotnet:$PATH /home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore`: **59/59**, 0 fallidas/omitidas.
- Dos pruebas unitarias verifican que crear o editar con placa ya utilizada devuelve `BusinessConflictException` y no persiste cambios.
- Frontend lint/build/discovery pasó con 16 E2E; la ejecución navegador/API/MySQL del nuevo rechazo visual queda pendiente del Actions cross-branch.
- Confirmación backend: Actions `44995681296` en `6ec5315` terminó success con build, xUnit y smokes de aislamiento tenant y concurrencia sobre MySQL temporal.

## Documento de Driver duplicado — 2026-11-22

- Release local con SDK 8.0.428: `dotnet test BackEnd_Gps.sln --configuration Release --no-restore`, **61/61**, 0 fallidas/omitidas.
- Unit tests validan que CreateDriver y UpdateDriver generan conflicto sin insertar, mutar ni hacer commit cuando el documento ya pertenece a otro Driver.
- E2E frontend local lint/build/discovery 16 pasan; Actions integrado pendiente.
- CI integrado: Actions `44997043103` en `a7f7c4e` pasó build, **61/61 xUnit** y smokes MySQL de tenant/concurrencia.

## Front cross-branch — documento de Driver repetido

- Actions `44997054375` hizo checkout exacto de backend `a7f7c4e`, pasó xUnit 61/61, responsive 1/1 y Playwright 16/16. El caso crea Driver, repite documento con correo distinto y confirma 409 y ausencia del nuevo correo en My Team.
- Audit/lint/build y cleanup del workflow terminaron success.

## Alta Driver concurrente — pendiente Actions

- Dos solicitudes HTTP simultáneas a `POST /Users` usan correos distintos y el mismo documento en el tenant. La API debe producir un 201 y un 409 independientemente de cuál gane.
- MySQL debe conservar exactamente una cuenta, un Driver y un token de activación; el cleanup elimina ambos correos y el documento de prueba.
- Esta prueba de integración detecta persistencia parcial ante violación concurrente del unique index; `bash -n`, diff-check y xUnit Release 61/61 pasan localmente.

## Perfil de miembros y validación MySQL — 2026-11-22

- Backend Release build: 0 warnings, 0 errores. xUnit: 62/62.
- En MySQL 8 efímero: smoke-tenant-http.sh pasó, incluyendo tenant ajeno 404, raíz 400, operaciones de ruta y cleanup verificado con cero fixtures.
- smoke-route-concurrency.sh pasó con verificación de conflictos y consistencia concurrente.
- La migración AddUserProfileNames se aplicó desde esquema vacío y ejecutó el SQL de backfill.
- El runner run-e2e-local.sh pasó Playwright autenticado 18/18 en Chrome contra API/MySQL reales; el contenedor/esquema fueron descartados al salir.
- Los smoke scripts respetan MYSQL_PORT; valor predeterminado 3306.
- Actions backend 45005400763 pasó build, xUnit, smoke HTTP/MySQL de tenant y smoke de concurrencia.


## Auditoría de dependencias — 2026-11-22

- Ejecutado con SDK 8.0.428: `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive` — código 0; sin vulnerabilidades en `BackEnd_Gps` ni `BackEnd_Gps.Tests`.


## Cross-tenant de paradas y ciclo Driver por HTTP/MySQL — 2026-11-22

- En MySQL 8 efímero, el token de tenant B intentó `PUT /Routes/{id}/points/0/status` y `POST /Routes/{id}/points/0/evidence` para la ruta de A; ambas devolvieron 404. La lectura posterior de A confirmó que status quedó 0 y evidence vacío.
- CRUD Driver HTTP: POST/GET, PUT y GET de verificación; status false, GET `onlyActive=false`, status true y GET; asignación/desasignación del vehículo con GET confirmatorio.
- `bash -n scripts/smoke-tenant-http.sh`, `dotnet test BackEnd_Gps.sln -c Release --no-restore -m:1` (**64/64**) y smoke completo pasaron. La base `fletflow_ci_*` y contenedor MySQL se descartaron; cleanup confirmó cero rutas, posiciones y vehículos QA.
- Actions `45008868471` valida la actualización documental previa. La Action que incluirá estos nuevos escenarios todavía debe ejecutarse tras publicar el cambio.


- Confirmación remota del lote Driver/cross-tenant: Actions backend `45009348293` success para `c6f74f8`; xUnit **64/64**, Release build, smoke HTTP/MySQL y concurrencia pasaron. Cleanup de servicios efímeros completo.


## Filtros temporales de tracking — 2026-11-22

- Smoke HTTP/MySQL fijó una hora RecordedAt y verificó coincidencia en los extremos inclusivos (`from=to`), filtros sólo `from`/sólo `to`, consulta fuera del intervalo y rango invertido. La API devuelve 200 con una posición en el rango inclusivo y 200 `[]` para los otros cuatro casos.
- Release xUnit **64/64**, `bash -n`, `git diff --check` y el smoke completo en MySQL 8 temporal pasaron; cleanup verificó cero rutas, posiciones y vehículos QA. Actions del SHA siguiente pendiente.


- Actions backend `45009987648` para `729f326` pasó: build, xUnit **64/64**, smoke HTTP/MySQL (incluye los filtros GPS y aislamiento de paradas) y concurrencia; DB/servicios temporales limpiados.

## Estado de cuenta y catálogo global de roles — 2026-11-22

- `UpdateUserCommandTests` comprueba incremento persistido de `TokenVersion` al desactivar/reactivar y que repetir el estado no rota sesiones.
- `smoke-tenant-http.sh` conserva el mismo JWT: `/Auth/me` devuelve 401 durante la baja y tras reactivar; una sesión nueva devuelve 200. Comprueba además GET `/Roles` 200 y POST/DELETE 403 para Admin de tenant.
- Release xUnit local **74/74**, smoke contra MySQL 8/API efímeros aprobado, cleanup verificado. Actions `45021088919` también pasó build, 74/74, smoke HTTP/MySQL y concurrencia.

## Sesiones y refresh concurrente — 2026-11-22

- xUnit Release: 76/76 tras agregar casos de logout-all y recuperación conocida/desconocida con fallo simulado de correo (la corrida final con `DOTNET_ROOT` se encuentra en ejecución al actualizar este documento; registrar su resultado exacto en el siguiente handoff).
- Smoke `scripts/smoke-tenant-http.sh` contra API local y MySQL 8 desechable: aprobado. Incluye refresh duplicado concurrente (exactamente 200/401), persistencia del token ganador, rechazo de reuso, invalidación de access/refresh en logout-all, y re-login correcto.
- Cleanup del smoke: 0 rutas, 0 posiciones GPS y 0 vehículos QA.
- La prueba de SMTP usa un emisor controlado que falla; no envía mensajes reales ni valida credenciales externas.

- Resultado final de la repetición local: Release xUnit **76/76**, 0 fallidas y 0 omitidas (22-11-2026), usando `/home/leonardo/.dotnet/dotnet` y runtime local.
- Release build final: **0 warnings / 0 errores**; `bash -n` de ambos scripts y `git diff --check` pasaron. El smoke de concurrencia de rutas está corriendo para esta iteración.
- El smoke adicional `scripts/smoke-route-concurrency.sh` también terminó exit code 0 en esta iteración; comprobó concurrencia de agenda/asignaciones, roles, rutas y paradas sin dejar asignaciones contradictorias.

## Refresh/logout-all concurrentes — 2026-11-22

- xUnit Release después del lock compartido: **76/76**.
- Smoke HTTP/MySQL ejecutado con API local y MySQL 8 efímero. El test bloquea deliberadamente la fila de Users, lanza `logout-all` y refresh en paralelo, y valida HTTP 200 al cierre global, cero refresh activos al final y 401 para cualquier sucesor devuelto antes del cierre. Resultado: exit code 0.
- La configuración JWT real también denegó (`401`) un access token anterior al cierre. Cleanup al final: 0 rutas, 0 posiciones GPS, 0 vehículos de Logistics/Manager.
- Riesgo pendiente: la duración de recuperación puede diferenciar cuentas conocidas/desconocidas; no se cuenta como mitigado por la igualdad del cuerpo HTTP.

## Recuperación uniforme y worker SMTP — 2026-11-22

- xUnit Release: 76/76, incluyendo respuesta genérica, token sólo para cuenta conocida, cola de entrega y duración mínima >=200 ms para conocidas/desconocidas.
- Smoke HTTP/MySQL real comparó JSON ordenado y HTTP 200 para ambas cuentas; ambas duraciones fueron >=200 ms. La cola ejecutó el worker y registró `SmtpHost no configurado`; no se envió email ni se comunicó como enviado.
- Estado histórico previo al lote de outbox: `EmailDeliveryQueue` era volátil y bounded (256). Ya se reemplazó por outbox durable; sigue pendiente probar la entrega SMTP con credenciales vigentes.
- Actions Backend `45024262135` para SHA `35ad3934daadafd16d553c11b999011d44ea2753`: `success`; build 0 warnings/0 errors, xUnit 76/76, smoke HTTP/MySQL tenant/concurrencia y cleanup cero.

## Alta de miembros y opciones de asignación por Driver — 2026-11-22

- Smoke API/MySQL 8: username/nombres 100 y email 200 aceptados. Username 101, email 201, firstName 101 y lastName 101 devuelven HTTP 400; consulta SQL confirma cero filas para los correos inválidos.
- Dos POST `/Users` concurrentes con correo igual: exactamente HTTP 200 y 409; MySQL confirma exactamente `1 User : 1 UserRole : 1 PasswordResetToken` y cleanup borra la cuenta.
- Driver autenticado probó GET `/Routes/assignment-options` y recibió 403; smoke también valida User 403 y acceso Logistics 200 sin filtrar datos cross-tenant.
- Corrida completa contra API real + MySQL 8 desechable terminó exit code 0; cleanup verificó 0 rutas, posiciones y vehículos y retiró usuarios QA.

## Outbox durable de recuperación — 2026-11-22

- Agregada prueba xUnit EmailDeliveryQueueTests: crea un mensaje outbox y comprueba que email, URL y token no aparecen en el payload persistido.
- AuthServiceSecurityTests comprueba una sola cola/commit y lock del usuario durante recuperación. Suite Release actual: 77/77, sin fallos ni skips.
- API .NET local + MySQL 8 descartable fletflow_ci_outbox_20261122: startup aplicó AddEmailOutbox; smoke-tenant-http.sh pasó. SQL confirmó payload cifrado (1:0 en correo conocido), dos solicitudes de reset concurrentes HTTP 200, exactamente un token activo y tres filas outbox.
- El smoke completo verificó aislamiento/roles, operación logística con pickup+delivery, evidencia, GPS, estados y limpieza 0 rutas / 0 posiciones / 0 vehículos; el contenedor DB se detuvo y eliminó.
- bash -n scripts/smoke-tenant-http.sh y git diff --check pasan. Repetir build, xUnit y Actions después de publicar ambos heads.
- SMTP no configurado durante la ejecución: el worker registró fallo y no envió ni simuló correo exitoso.

- Reinicio con key ring persistente en ruta temporal: se encoló una recuperación, se capturó el baseline, se detuvo la API, se hizo elegible la fila y se reinició API con el mismo key ring. MySQL confirmó que aumentó el intento y LastFailureType=InvalidOperationException (SMTP ausente, payload descifrado y sender invocado).
- El primer assert del restart asumió un conteo fijo y falló; se corrigió a comparar con el baseline. Causa/solución en ERROR_LOG.
- Smoke independiente smoke-route-concurrency.sh también pasó contra API + MySQL 8 descartables tras la migración.

## Actions outbox — 2026-11-22

- Backend commit 5049dea: Actions 45027017954 success. Release build 0 warnings; xUnit 77/77; smoke HTTP/MySQL y concurrencia rutas success; cleanup completo.
- El reinicio con key ring persistente se comprobó localmente en DB/API descartables y quedó documentado arriba; SMTP sin credenciales sólo produjo el fallo esperado.
# Regresión de autorización por reasignación concurrente — 2026-11-22

- `ChangeRouteStatusAuthorizationTests.Execute_DriverReassignedAfterInitialRead_CannotChangeRouteStatus`: reemplaza la asignación cuando se adquiere el lock, como ocurriría si el Driver se reasigna después del chequeo inicial del controller.
- Verifica `KeyNotFoundException`, una sola adquisición del lock, cero actualizaciones del repositorio y estado Pending conservado.
- Resultado Release local: prueba enfocada 1/1; suite completa **78/78**, cero skips; build Release 0 warnings/0 errors. Validación remota pendiente.

## Worker de EmailOutbox — tests SQLite 2026-11-22

- `Worker_DecryptsAndMarksOutboxMessageSent`: ejecuta el worker real y verifica payload recuperado, llamada al sender, timestamp enviado, attempts y liberación de lock.
- `Worker_DeadLettersMessageAfterTwelfthFailure`: fuerza fallo del sender desde intento 11 y confirma intento 12, `DeadLetteredAtUtc`, tipo de falla y lease libre.
- Validación local: suite Release **80/80**, 0 skips; build Release 0 warnings/0 errors. Actions `45030208849` también pasó build/xUnit 80/80, smoke HTTP/MySQL, concurrencia y cleanup.
# Incidente CI — precisión de `CreatedAtUtc`

- Actions `45029094282`: build/tests pasaron, smoke HTTP falló en assert API/MySQL luego de editar ruta.
- Ajuste de prueba: helper compara timestamp con seis decimales, completando fracciones cortas y eliminando exceso; el mensaje ahora imprime API antes/después y DB antes/después. El normalizador pasó casos locales sin fracción, `.1234` y `.123456789`.
- Actions `45029374847` confirmó smoke y cleanup completos; xUnit **78/78**.
# Cobertura Driver same-tenant en smoke HTTP — pendiente CI

- Con un JWT Driver real del tenant propietario, el smoke prueba `GET /Routes?driverId=...`, detalle, cambio de estado, estado de parada, evidencia y GET/POST posiciones contra una ruta de otro Driver.
- Expected status: listado 200 vacío; detalle/GPS 403; escrituras/estado/parada/evidencia 404. Admin verifica que la ruta permanezca InProgress, sin evidencia nueva, y que GPS siga en una fila.
- El fixture es el Driver ganador de la carrera HTTP de unicidad ya existente; sus filas tienen cleanup por email/documento. `bash -n` y diff-check locales pasan.
- Actions `45029798323` pasó con API/MySQL reales: el step de isolation, el smoke concurrente, los 78 xUnit y cleanup completo finalizaron success.

## Auditoría de seguridad: rate limit, correo diagnóstico y Data Protection

- `SimpleRateLimitMiddlewareTests.ConcurrentRequests_CannotExceedSixtyAcceptedRequestsPerWindow`: ráfaga paralela de 200 requests desde la misma IP; resultado 60 aceptadas y 140 HTTP 429.
- `AuthControllerTests` verifica que el endpoint de email diagnóstico da 404 fuera de Development; en Development rechaza destinatario distinto del email autenticado y sólo invoca el sender para la cuenta propia.
- `dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1`: 85/85; build Release: 0 warnings/0 errors. Actions integrado pendiente para la rama actualizada.
- Producción requiere configuración de key ring persistente y certificado de cifrado; su disponibilidad depende de secretos/infraestructura externa y no se simuló con credenciales productivas.
- Arranque de configuración Production: sin certificado y con ruta de key ring especificada, la API rechazó el inicio con `Producción requiere FLETFLOW_DATA_PROTECTION_CERT_PATH`; la comprobación enfocada posterior pasó. No se creó core file en el repositorio.
- Actions [`45037399332`](https://github.com/Paul1578/Back_GPS/actions/runs/45037399332) validó Backend `94c3e90`: Release build 0 warnings/0 errors; xUnit **85/85**; smoke HTTP/MySQL tenant/roles/flota y smoke de concurrencia de asignación; cleanup success.

## Data Protection: cifrado, persistencia y rotación del key ring

- `DataProtectionKeyRingTests.CertificateRotation_PreviousCertificateCanDecryptExistingKeyRingAndPayload` crea key ring/payload con certificado anterior; un provider configurado con certificado actual + anterior descifra el payload legado, crea una clave nueva y valida el round-trip nuevo. Comprueba `<encryptedSecret` en ambos archivos y limpia el directorio temporal en `finally`.
- La prueba enfocada de rotación pasó 1/1; pruebas de rotación y rate limit concurrente 2/2; suite Release **86/86**; build Release **0 warnings/0 errors**.
- Suite local: `/home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore`; build: `/home/leonardo/.dotnet/dotnet build BackEnd_Gps.sln --configuration Release --no-restore -m:1`.
- Front Actions `45039605708` usó Backend `b625c28`: xUnit 86/86, E2E API/MySQL Front 23/23, responsive/audit/lint/build y cleanup success.
- Backend `b664b2c`, Actions `45040550853`: build Release, xUnit **86/86**, aislamiento HTTP/MySQL, concurrencia de rutas y cleanup success. Esta corrida valida la prueba de rotación/concurrencia y el wiring de configuración del backend.

## Regresiones de concurrencia y rotación de certificados — 2026-11-22

- `SimpleRateLimitMiddlewareTests.ConcurrentRequests_CannotExceedSixtyAcceptedRequestsPerWindow` prepara 200 solicitudes en una barrera async, las libera juntas desde la misma IP y confirma ejecución solapada, 60 aceptadas y 140 HTTP 429.
- `DataProtectionKeyRingTests.CertificateRotation_PreviousCertificateCanDecryptExistingKeyRingAndPayload` crea key ring/payload con certificado anterior y verifica descifrado y uso posterior con provider configurado con certificado actual + anterior.
- Pruebas enfocadas: `/home/leonardo/.dotnet/dotnet test BackEnd_Gps.Tests/BackEnd_Gps.Tests.csproj --configuration Release --no-restore -m:1 --filter "FullyQualifiedName~SimpleRateLimitMiddlewareTests.ConcurrentRequests|FullyQualifiedName~DataProtectionKeyRingTests.CertificateRotation"`.
- Suite completa: `/home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1`.
- Resultado: tests enfocados 2/2, suite completa 86/86, build Release 0 warnings/0 errores.


## Seguridad de sesión, reset y GPS — validación local 2026-11-22

- `/tmp/fletflow-dotnet/dotnet test BackEnd_Gps.sln --configuration Release --no-restore`: **91/91**, 0 omitidas. `dotnet build BackEnd_Gps.csproj --configuration Release --no-restore`: 0 warnings/0 errores. El path `/tmp` responde al entorno de esta sesión donde `/usr/bin/dotnet` no encuentra runtime; no es una ruta requerida para otros desarrolladores.
- Unit tests: bloqueo de cambio de contraseña antes de revocar; reset/activación consumen token sólo una vez; repositorio SQLite devuelve true en primer consumo y false en el segundo; GPS Driver reasignado en lock no registra posición.
- `bash -n scripts/smoke-tenant-http.sh` y `git diff --check`: correctos. Próxima Actions debe validar en MySQL el smoke de mantenimiento y el workflow backend completo.

- `EmailOutboxWorkerTests` cubre supresión de payload cuando ResetToken ya se usó/invalidó; `dotnet test ... --filter FullyQualifiedName~EmailOutboxWorkerTests`: **3/3**. Suite Release actual: **92/92**.

## Reasignación Driver/ruta pendiente — 2026-11-22

- `dotnet test BackEnd_Gps.Tests/BackEnd_Gps.Tests.csproj -c Release --no-restore` con SDK aislado .NET 8.0.428: **94/94**.
- Incluye pruebas SQLite para rechazar reasignación con ruta Pending y para defender el inicio ante un vínculo Driver/Vehicle inconsistente, comprobando que no se muten los estados.
- `bash -n scripts/smoke-route-concurrency.sh scripts/run-e2e-local.sh` y `git diff --check`: correctos. Smoke HTTP/MySQL real de este cambio y CI remoto pendientes.
- Action backend [45048795456](https://github.com/Paul1578/Back_GPS/actions/runs/45048795456), commit `27bf75a`: build Release exitoso; xUnit **94/94**; smoke `smoke-tenant-http.sh` y `smoke-route-concurrency.sh` pasaron contra MySQL 8 descartable. El smoke de concurrencia incluye el HTTP 409 al reasignar Driver con ruta Pending y consulta SQL que comprueba vehículo, ruta y asignación sin mutaciones. Cleanup: cero recursos QA.

## Revocación concurrente y asignación operativa Pending — 2026-11-22

- Suite Release local tras ampliar tests: **97/97**. Casos nuevos comprueban que cambiar a Logistics con ruta Pending no desactive ni desasigne el Driver ni cambie tokens; baja/desasignación también se rechazan sin persistencia.
- Build Release: 0 warnings/0 errores. `bash -n scripts/smoke-tenant-http.sh scripts/smoke-route-concurrency.sh` y `git diff --check`: correctos.
- Smokes actualizados: carrera MySQL real refresh/cambio de rol con row lock; HTTP 409 y SQL intacta para reasignación, desasignación y baja con ruta Pending. Ejecución Action pendiente.
- Cierre CI [45050058627](https://github.com/Paul1578/Back_GPS/actions/runs/45050058627), `2045b18`: build Release; xUnit **97/97**; smoke tenant HTTP/MySQL y route concurrency aprobados, incluida carrera de cambio de rol/refresh y HTTP 409 con SQL sin cambios para operaciones de Driver con ruta Pending. Cleanup: cero fixtures.

## Validación de cuenta Driver y recuperación — 2026-11-22

- SQLite: edición de ruta con cuenta Driver inactiva rechazada sin cambios de ruta/vehículo; prueba focal CreateRouteCommandTests 10/10.
- Smoke HTTP/MySQL: editar/iniciar con cuenta inactiva devuelve 409; Activate/Reset aceptan tokens una vez y rechazan reutilización; Reset invalida refresh anterior y permite sólo clave nueva.
- bash -n y diff-check correctos. Suite y Actions integradas pendientes.

- Durante la fixture HTTP, TokenHash se normaliza a hexadecimal mayúsculo, igual a Convert.ToHexString; las contraseñas fixture se generan aleatoriamente.

## Actions — smoke de recuperación y ruta — 2026-11-22

- Action 45060714338, commit 14348f3: Release build, xUnit 100/100, smoke tenant HTTP/MySQL (activate/reset, tokens de un solo uso, revocación de refresh, edición con cuenta Driver inactiva), concurrencia y cleanup: success.

## Fixture Driver operativo adicional

- `scripts/run-e2e-local.sh` crea una cuenta Driver de uso exclusivo para la ruta operativa, separada del Driver de CRUD, y exporta `FLETFLOW_E2E_ROUTE_DRIVER_ID` junto con sus credenciales.
- Los fixtures se eliminan mediante `scripts/cleanup-e2e.sql`. La validación de sintaxis pasa localmente; falta confirmar provisioning, E2E y cleanup en la Action nueva.

- Backend Action `45061524504` pasó para SHA `3f87af8`: build/tests Release, smoke de tenant HTTP/MySQL, concurrencia de rutas y cleanup de contenedores.

- Vehicle validation/concurrency: Release xUnit **103/103**; Release build **0 warnings, 0 errors**. El primer intento tuvo 102/103 por fake mutable de EF, corregido y registrado en `ERROR_LOG.md`. Backend Action `45063488394` pasó e incluye smoke tenant HTTP/MySQL para límites 400 y placas duplicadas 409 en create/update.

- Cambio de rol con cuenta Driver inactiva: xUnit Release **104/104**; smoke HTTP/MySQL cubre Logistics→Driver y comprueba que el perfil siga inactivo. Validación integrada pendiente.
- Primera Action `45063802250` rechazó el cambio de rol con 409 porque había ruta pendiente; el test ya se ejecuta después de borrar esa ruta. También se corrigió orden de cleanup por FK restrictiva; Action nueva pendiente.
- Action `45064477054` pasó en `1534828`: build, xUnit **104/104**, HTTP/MySQL y cleanup.

- Login enum: xUnit verifica mismo 401/mensaje para email desconocido, contraseña incorrecta, cuenta inactiva y pendiente; smoke compara código y mensaje usando API/MySQL. Backend Action `45064942603` pasó build, xUnit **106/106**, smoke y cleanup.
- Registro público nuevo/duplicado: controller entrega payload neutral en ambos casos; E2E compara cuerpos, luego autentica la cuenta creada. Smoke HTTP/MySQL incluido. Local Release **107/107**, build 0/0 y syntax shell; Actions pendientes.
- Smoke de registro mide ambas respuestas >=450 ms (controlador fija piso de 500 ms); no certifica igualdad temporal bajo carga externa.
- Backend Action `45065747053` pasó build/xUnit **107/107**, alta nueva/duplicada y cleanup, antes de la adición del piso temporal. Validar esa medición en la siguiente Action.
- Backend Action `45066082294` pasó build/xUnit **107/107**, smoke HTTP/MySQL de alta nueva/duplicada, cuerpo neutral, tiempo >=450 ms y cleanup.
- `smoke-tenant-http.sh` local valida el ciclo Vehicle HTTP/MySQL de edición, estado operativo independiente, baja lógica y reactivación. MySQL efímero pasó; cleanup correcto; Actions del nuevo cambio pendientes.
- El smoke usa el SDK en `/home/leonardo/.dotnet/dotnet` porque el `dotnet` global del entorno no puede iniciar la API. No se mutó la DB persistente.
