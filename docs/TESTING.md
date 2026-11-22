# Pruebas backend

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
