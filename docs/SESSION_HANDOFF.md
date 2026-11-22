# Session handoff

## Ramas

- Backend: `chore/stabilize-fleet-platform`.
- Frontend hermano: `/home/leonardo/Front_gpsR`, rama `refactor/route-builder`.
- `main` intacta. Usuario autorizó los cambios, ramas separadas y documentación de errores.

## Estado cerrado en esta fase

- Seguridad base: registro no asigna rol privilegiado, recuperación no filtra token, reset revoca refresh tokens, errores 500 genéricos y CORS configurable.
- Owner multiempresa en Vehicles/Drivers/Routes/RoutePositions; `owner_id`, global filters, stamping, unicidad tenant y migración aplicada. Vehículo legado asignado al Admin raíz más antiguo aprobado por el usuario.
- Reglas de disponibilidad, asignación, solapamiento y transiciones.
- Cada punto conserva operación, estado y evidencia JSON. Endpoints `PUT /api/Routes/{id}/points/{index}/status` y `POST .../evidence`; conductor sólo opera su ruta. Para cerrar ruta, cada parada operativa necesita estado terminal y evidencia.
- Pausa de ruta libera vehículo si no hay otra ruta en curso.
- GPS valida rangos y consultas históricas están limitadas.

## Verificación

- `dotnet test BackEnd_Gps.sln --configuration Release --no-restore`: 45/45 aprobadas.
- API de desarrollo `http://127.0.0.1:5048`; Swagger 200, rutas sin sesión 401.
- E2E HTTP/MySQL: ruta con recogida y entrega, bloqueo sin evidencia, notas persistidas, posición GPS, cierre; limpieza SQL dejó cero filas QA.
- Tenant HTTP: Admin A lista 1, Admin B 0, consulta cruzada 404.
- Login Admin/password real comprobado en cuenta QA: `/Auth/me` devuelve Admin, `/Users/my-team` responde y contraseña incorrecta 401. La cuenta se activó en SQL sólo por SMTP no disponible y fue limpiada.
- Frontend del repo hermano pasa lint/build y `/dashboard` devuelve 200.
- Prueba Chrome UI completa del constructor: clics mapa origen/destino/parada, `PickupAndDelivery`, asignación, revisión y creación. Consulta autenticada verificó tres puntos con operación 3 (`PickupAndDelivery`); ruta y cuentas/recursos temporales limpiados con conteos MySQL a cero.
- CRUD Chrome como Admin: creó/editó vehículo, creó Driver con datos completos, UI notificó correo no enviado, asignó y desasignó vehículo. Un 415 en `PUT` vacío reveló middleware demasiado estricto; corregido y repetido con éxito. QA limpió todos los usuarios, vehículo y conductor (conteos cero).
- Roles: DB no tenía Logistics y rutas pedían Dispatcher; se corrigió seed/policies. Prueba HTTP con usuario Logistics: GET rutas 200, DELETE UUID inexistente 404; User obtiene 403. QA users eliminados.
- Driver E2E en Chrome móvil: ruta asignada, inicio, parada, evidencia nota persistente, completar parada/ruta; API confirma estado y vehículo disponible. Segundo Driver del mismo tenant: lista vacía, detalle 403 y cambio de estado 404. DTO incluye resumen del vehículo y UI lo muestra sin permitir listar toda la flota.

## Pendiente / limitaciones reales

1. Completar CRUD restante y ampliar matriz de permisos en navegador; flujos principales Admin y Driver, además de Logistics/User en API, ya están ejecutados.
2. SMTP necesita credenciales vigentes; no marcar entrega de correo como exitosa hasta tenerlas.
3. Rotar en entornos remotos las claves DB/JWT si se reutilizaron; las locales fueron rotadas en esta sesión.
4. Automatizar HTTP/MySQL E2E y trasladar fotos a object storage antes de producción a escala.

Errores de migración, herramientas, QA y soluciones están en `docs/ERROR_LOG.md`.

## Estado verificado 2026-11-21 (cierre del pase de auditoría)

- Suite xUnit Release: 45/45; `CreateRouteCommandTests` verifica persistencia unitaria de ruta/vehículo, lifecycle valida timestamps y bloquea vehículo en mantenimiento.
- `npm run lint`, `npm run build`, `npm audit --audit-level=low`: correctos; 0 vulnerabilidades frontend.
- DB local: contraseña root y JWT rotados, refresh tokens revocados, `.env` 0600 e ignorado; MySQL recreado sin tocar el volumen; 22 migraciones, cero filas QA, cero asignaciones multirol y cero refresh tokens vigentes. Las sesiones de navegador deben iniciar sesión de nuevo.
- API local en Development `127.0.0.1:5048`: Swagger 200 y rutas protegidas sin token 401. Frontend `/dashboard` 200.
- No hay credenciales QA persistentes; el smoke HTTP y E2E visual focalizado de Driver/User se ejecutaron con cuentas temporales al cierre. Los flujos Chrome/API/MySQL siguen documentados.
- Móvil nativo se pospone por decisión del usuario. Responsive web sigue en alcance.
- Los audits independientes backend y frontend se usaron como revisión real; el límite de hilos no permitió crear cinco agentes. No afirmar que participaron cinco perfiles.
- E2E autenticado posterior a rotación realizado con cuentas QA temporales: registro ignora rol Admin, Admin puede crear/listar vehículo, tenant B obtiene 404/lista aislada y User obtiene 403. Cleanup borró cuentas, refresh tokens y vehículo.
- Chrome E2E posterior verificó editar/baja/filtro/reactivación de Driver, UI sin overflow a 390×844 y User sin controles de flota con 403 en Vehicles/Routes.
- Chrome E2E comprobó Manager→Logistics: conserva acceso a rutas y no muestra permisos de Equipo/Vehículos; API 200/403 respectivamente. El usuario QA se eliminó.
- Chrome E2E Vehicle verificó alta, búsqueda/vacío, edición y baja lógica; API conservó `isActive=false` fuera del listado activo. Fixtures de vehículo y cuenta eliminados.
- Tras un error del harness se rotó de nuevo la clave MySQL root local, se actualizó `.env` 0600 y se reinició API. DB conserva 22 migraciones, cero cuentas/Drivers QA y cero refresh tokens vigentes.
- Servicios locales están corriendo con la build actual: API `127.0.0.1:5048`, frontend `127.0.0.1:3000`; Swagger/dashboard 200 y API protegida sin token 401.
- Para continuar: revisar el smoke HTTP cuando se configuren credenciales QA, ampliar la matriz E2E visual, decidir object storage/rate limiting distribuido antes de escalar y configurar SMTP/hosts HTTPS de producción.

## Continuación de estabilización — 2026-11-21

- Suite backend Release confirmada en 45/45; build Release previo sin advertencias. MySQL conserva 22 migraciones, cero usuarios/vehículos/conductores/rutas QA, cero refresh tokens activos y cero usuarios con más de un rol.
- E2E Chrome confirmados para Driver (edición, baja/reactivación), User (controles ocultos y 403), Logistics (Routes 200/Vehicles 403) y Vehicle (alta/búsqueda/edición/baja lógica); fixtures eliminados.
- El frontend mantiene `127.0.0.1:3000` HTTP 200 y Swagger backend `127.0.0.1:5048` HTTP 200.
- Se documentó que `/api/Dashboard` no es un endpoint real: la vista web agrega recursos de Vehicles/Routes/Drivers. El 404 del sondeo manual fue un error de diagnóstico, no una falla del backend.
- Pendiente alcanzable: ampliar CRUD/permisos visuales y automatizar E2E en CI con secreto QA; externos de despliegue incluyen SMTP vigente y dominio/TLS. Almacenamiento de evidencia y rate limiting distribuido son decisiones de producción.
- Playwright del frontend hermano pasó 2/2 contra esta API/MySQL con Admin temporal: login inválido y ciclo de vehículo por UI a 390×844; datos eliminados físicamente. API se reinició sin bootstrap y Swagger confirmó 200.
- Playwright ahora pasa 4/4: login negativo, vehículo Admin, edición de ruta con Driver de equipo vinculado y recogida+entrega preservada, y rol User sin acceso a UI/API de flota. Runner crea User/Driver temporales y limpia las cuentas y recursos E2EQA; conteos en cero.
- Runner reforzado: acepta únicamente web/API locales, exige cuenta temporal QA y `FLETFLOW_E2E_ALLOW_MUTATIONS=true`; sin variable aborta 2 antes de DB. `bash -n` y `git diff --check` pasan.
- Cierre repetido: backend Release 45/45; frontend lint/build/npm audit sin vulnerabilidades; Playwright autenticado 4/4. Swagger 200 y endpoint protegido 401 sin sesión.
- Conteo SQL de cierre: 0 cuentas, vehículos, conductores y rutas E2EQA; 0 refresh activos y 22 migraciones. Una primera lectura consultó por error `IsRevoked`; se inspeccionó el esquema, se cambió a `RevokedAt` y la lectura completa pasó (detalle en `ERROR_LOG.md`).
- Pendiente alcanzable: ampliar UI E2E a los CRUD/pantallas restantes y conectar el flujo de frontend+API+MySQL a Actions. SMTP y preparación de despliegue quedan externos; móvil nativo está pospuesto por decisión del usuario.
- Nueva cobertura UI: Playwright ya pasa 5/5 con alta de Driver desde Equipo, persistencia de edición, baja/reactivación y respuesta clara si SMTP no entrega. 390×844 no desborda; cleanup confirmó cero fixtures.
- Se detectó que el rate limiter de 60/min por IP bloqueaba QA loopback en Development. Loopback queda exento sólo en Development; xUnit confirma que Production aún limita a 60 y suite total 47/47.
- Validación repetida: frontend lint/build/npm audit (0 vulnerabilidades); MySQL 22 migraciones, cero usuarios/vehículos/conductores/rutas QA y cero refresh activos.
- La UI E2E comprueba Driver→Logistics→Driver: el perfil pasa inactivo/activo y desaparece/reaparece en la lista de conductores. Se corrigió el refresco frontend al cambiar hacia/desde Driver.

## Continuación de cobertura — 2026-11-21

- Frontend hermano completó Playwright autenticado 7/7 Chrome/API/MySQL: alta de ruta móvil desde mapa, dos operaciones, asignación, persistencia de cuatro puntos y baja visual; fixtures retirados.
- Backend amplió filtro de tenant InMemory a Vehicles, Drivers, Routes y RoutePositions y agregó aserciones de roles para controllers Vehicles/Drivers/Users.
- `dotnet test BackEnd_Gps.sln -c Release --no-restore -m:1`: 48/48. Release build también pasó: 0 warnings, 0 errors.
- Informe independiente backend: matriz completa HTTP por rol y escrituras cross-tenant para Route/Driver/Position aún requieren pruebas con proveedor relacional; CI no contiene aún E2E autenticado multi-repo.
- Mantener rama `chore/stabilize-fleet-platform`. No declarar completa la misión. SMTP válido, dominio/TLS de despliegue y almacenamiento de fotos a escala siguen pendientes externos/de producción.

## E2E de aislamiento HTTP — 2026-11-21

- El runner local provisiona dos Admin raíz con propietarios separados. El E2E Chrome/API/MySQL pasó 8/8.
- Se probaron listados, lecturas, edición, estado y baja cross-tenant en Vehicle/Driver/Route, creación de ruta con IDs ajenos y registro/lectura GPS.
- GPS: A tiene posición persistida; B obtiene `200 []` del endpoint colección y no puede POST (404). No se encontró exposición, sólo una expectativa inicial de status incorrecta.
- Release xUnit 48/48; build Release 0 warnings/0 errors; auditoría NuGet sin vulnerables. Pendiente conectar este E2E en Actions con MySQL y secretos QA.

## Confirmación final — 2026-11-21

- Backend Release tests 48/48, build 0 warnings/0 errors, análisis de paquetes sin vulnerables y `git diff --check` limpio.
- E2E cross-tenant completo en el frontend hermano 8/8 contra Chrome/API/MySQL; SQL de cierre sin fixtures ni refresh activos, 22 migraciones.
- Swagger 200, endpoint protegido 401 sin JWT. Mantener pendientes: integrar E2E autenticado a CI y cerrar cobertura residual; SMTP y despliegue permanecen externos.

## CI backend con MySQL real — 2026-11-21

- `.github/workflows/ci.yml` ahora ejecuta tests xUnit, MySQL 8 como service, inicia API Development con JWT/Admin efímeros y ejecuta `scripts/smoke-tenant-http.sh`.
- El script se probó contra la DB local descartable `fletflow_ci_validation_02`: migraciones 22, API HTTP cruzada pasó para Vehicles/Drivers/Routes/GPS y limpieza cero. DB destruida al terminar.
- Se detectó y corrigió `Env.Load()` sobrescribiendo variables del proceso con `.env`; ahora `Program.cs` llama `Env.NoClobber().Load()`. Un Admin QA fue creado en `fletflow` por el primer arranque errado; se eliminó sólo esa cuenta, sin tokens/recursos, y la consulta confirmó cero. Nunca se ejecutó smoke contra la DB principal.
- Un error inicial del runner system `dotnet` se corrigió invocando el SDK local documentado. Un agente encontró `MYSQL_PWD` faltante y limpieza por prefijos; ambos problemas quedaron corregidos.
- Las ramas `refactor/route-builder` y `chore/stabilize-fleet-platform` aún no están publicadas en origin, así que falta ejecutar el workflow en GitHub. No hacer push sin instrucción.
- Se amplió el smoke para verificar permisos HTTP del rol User (`GET /Vehicles`, `POST /Vehicles`, `GET /Routes` → 403). Repetido con éxito en una segunda DB temporal; fixtures y esquema fueron eliminados.
- El workflow frontend ahora extrae esta rama backend, crea MySQL/API descartables y ejecuta `scripts/run-e2e-local.sh`. El runner usa el cliente MySQL host, admite variables CI sin leer `.env` por encima de ellas y restringe `CI=true` a esquemas `fletflow_ci*`.
- E2E Chrome/API/MySQL ejecutado localmente con el runner adaptado: 8/8. Al finalizar el esquema temporal se descartó; la base principal mantiene 22 migraciones y cero usuarios QA. La API local fue restaurada y Swagger 200.
- El workflow remoto sigue pendiente de publicación/PR; repos frontend/backend son públicos y la rama frontend ya apunta explícitamente a `chore/stabilize-fleet-platform` para el checkout cruzado.
- Ramas publicadas con PRs borrador: frontend #2, backend #5. Backend Actions completó verde (restore/build/xUnit/HTTP MySQL smoke). Primer frontend run falló en precheck prematuro de Next; ya se corrigió. Se reemplazaron actions que generaron advertencia Node 20 por versiones Node 24; repetir CI.

## Estado conjunto tras estabilizar E2E frontend — 2026-11-21

- El workflow backend de `chore/stabilize-fleet-platform` completó verde: Release build/tests y smoke HTTP/MySQL cross-tenant.
- El workflow frontend aún debe repetirse: el E2E móvil expuso una aserción débil y clics calculados antes de desplazar el mapa al viewport. Front corrigió la verificación y la selección; suite local Chrome/API/MySQL **8/8** en DB efímera descartada.
- Ramas y PRs separados: frontend `refactor/route-builder` (#2), backend `chore/stabilize-fleet-platform` (#5). Este hallazgo no requiere cambios backend.
- Pendientes: Actions frontend con la corrección, más acciones CRUD/vistas responsive. SMTP válido es externo; la app nativa está pospuesta por el usuario.

## Continuación de estabilización — 2026-11-21

- Rama backend `chore/stabilize-fleet-platform`; frontend `refactor/route-builder`. No trabajar en `main` ni fusionar PRs.
- Detectado y corregido race condition de agenda: `UnitOfWork.LockRouteAssignmentAsync` abre transacción relacional, bloquea filas owner-scoped Vehicle→Driver con SQL parametrizado `FOR UPDATE` en MySQL y mantiene locks hasta commit. CreateRoute/UpdateRoute revalidan agenda/estado bajo locks; conflictos van a HTTP 409.
- Se detectó que Pending→InProgress admitía conductor desactivado. Ahora el comando valida Driver activo y regresa 409 sin mutaciones; test SQLite cubre status/ruta/vehículo.
- Smoke real MySQL local: parejas concurrentes Create same vehicle, Create same Driver y Update same vehicle+Driver dieron cada una exactamente 1 éxito + 1 HTTP 409. Smoke tenant HTTP/MySQL pasó.
- Suite backend Release 49/49. Auditoría NuGet inicialmente detectó SQLitePCLRaw.lib.e_sqlite3 2.1.10 (CVE-2025-6965) añadido por provider de pruebas; pin 2.1.14 agregado. Nueva auditoría reporta cero vulnerables.
- Pendiente inmediato: completar Release build, revisar formato/diff, commit/push a la rama y comprobar ambos workflows remotos. Luego continuar cobertura restante CRUD/responsive y registrar pendientes SMTP/infra externos.
- Frontend completó local lint/build/audit y Playwright autenticado 12/12; GitHub Actions run `44974312596` pasó para commit `4b60bf1`, pero debe repetirse al actualizar checkout/backend con estos cambios.
- Limitaciones externas: SMTP requiere credenciales válidas. App nativa está pospuesta por el usuario; responsive web sigue en alcance.
- Auditoría independiente encontró la carrera entre dos inicios sobre un vehículo. Cambio ampliado: locks de la fila Route y asignación owner-scoped; UpdateRoute bloquea asignación anterior y nueva en orden global; start verifica `otherInProgressRoutes`. Prueba MySQL real concurrente dio 204/409. Suite final ahora 50/50.
- Preparación QA adicional con `fletflow_ci_validation_11`: `smoke-tenant-http.sh` y `smoke-route-concurrency.sh` pasaron; el `trap` detuvo API y descartó el esquema. `MYSQL_ROOT_PASSWORD` local se rotó después de una salida accidental, `.env` conserva modo 0600.
- Repetir Release build, NuGet audit, smoke MySQL tras los últimos cambios y luego push; revisar los dos workflows remotos antes de continuar con CRUD/responsive restantes.
- Un segundo pase del revisor pidió verificar persistencia además de 204/409. Se añadieron asserts MySQL para estados Route/Vehicle en dos starts y estados Route/Driver/Vehicle en la carrera con desactivación; aún falta repetir el smoke con esos asserts.
- Repetición final en `fletflow_ci_validation_14` pasó tenant HTTP y todos los escenarios de concurrencia con asserts de persistencia; `trap` eliminó la DB. Release build 0 warnings/0 errors, tests 50/50 y audit NuGet sin vulnerables.
- Acciones remotas completadas: backend run `44976384961` (commit `90da59a`) verde en PR #5, incluidos los nuevos smokes MySQL. Frontend run `44976408960` para `1c8856a` está ejecutando en PR #2 y ya pasó backend restore/build/tests, npm audit, lint; espera build Next, responsive y Playwright autenticado.

## Continuidad — seguridad de sesiones y estado de flota — 2026-11-21

- Código local pendiente de commit en `chore/stabilize-fleet-platform`: baja de usuario revoca refresh; JWT y refresh bloquean IsActive=false; start valida Vehicle activo; DeleteRoute/Vehicle y unassign Driver toman locks y previenen inconsistencias; updates de status/evidence de puntos serializan por Route; nuevo `POST /Vehicles/{id}/reactivate`.
- Evidencia: release xUnit 50/50; build release 0/0 warnings/errors; smoke local tenant y escenarios de concurrencia/status en MySQL temporal aprobados; scripts validan dos updates de paradas simultáneos. La DB quedó descartada. Los últimos resultados van documentados en SECURITY/BUSINESS_RULES/ARCHITECTURE/TESTING/ERROR_LOG.
- Diferencias del entorno local: dotnet de sistema no tiene runtimes; usar `/home/leonardo/.dotnet/dotnet`. `.env` guarda `DB_PASSWORD` distinto de `MYSQL_ROOT_PASSWORD`; para los smokes exportar el valor activo a `DB_PASSWORD` sólo en el proceso. No imprimir secretos. Ambas causas fueron registradas.
- Próximo: revisar diff, publicar rama y esperar Actions; revisar/repetir frontend Actions al backend nuevo. Aún faltan pruebas exhaustivas Manager/Logistics/Driver en HTTP, cobertura completa restante y los externos SMTP/producción. Nativo móvil queda aplazado. No cerrar objetivo.
- Ampliación posterior de smoke real de roles también pasó: Manager 200 Vehicles/Drivers/Routes; Logistics 200 Routes y 403 Vehicles/Drivers; User 403; Driver se cubre con Playwright. DB `fletflow_ci_validation_roles20261121` descartada.

## Continuación — cambio de rol seguro durante operaciones — 2026-11-21

- Rama backend: `chore/stabilize-fleet-platform`. `ChangeUserRoleCommand` comparte los locks de asignación con el inicio de ruta, vuelve a leer Driver/usuario bajo el lock, revoca refresh tokens al cambiar el rol y revierte ante conflicto.
- JWT valida en cada request que el usuario esté activo y que su único rol actual coincida con el claim firmado. Un rol anterior deja de autorizar de inmediato.
- MySQL descartable: smokes HTTP/tenant/roles y concurrencia aprobados. La carrera de cambiar Driver→Logistics contra inicio sólo acepta `InProgress+Driver activo+Vehicle OnRoute` o `Pending+Logistics+Driver inactivo+Vehicle Available`. Reactivar Vehicle con ruta en progreso da 409 sin alterar la ruta.
- `dotnet test` Release **50/50**; auditoría NuGet sin vulnerables; validaciones `bash -n` y `git diff --check` limpias. Playwright integrado del repo frontend pasó **14/14** contra API/MySQL reales; se descartó el schema.
- El runner `run-e2e-local.sh` ahora normaliza `FLETFLOW_API_URL` con o sin `/api`; Actions anterior verde corresponde al SHA previo, así que falta publicar el lote y revisar ambos runs nuevos.
- Errores/harness corregidos en `ERROR_LOG.md`: la invocación del wrapper `dotnet run` conectó con usuario DB inesperado; el binario directo con usuario DB temporal funcionó. Scripts sin permiso ejecutable se invocan con `bash`. Dos expectativas de Playwright se alinearon al DOM/DTO real.
- Matriz HTTP de mutaciones MySQL: Manager crea Vehicle/Driver/Route y edita Route; Logistics edita/elimina Route y recibe 403 al crear Vehicle/Driver; User y Driver no pueden crear Route. Smoke pasó y limpió schema/fixtures.
- Registro público con `roleName=Admin` persistió un único rol User y recibió 403 en Vehicles. Logout-all real devolvió 200 y el refresh anterior dio 401.
- Build Release final: 0 warnings/0 errors; documentación actualizada. Próximo: commit/push en la rama existente y esperar CI. Mantener misión activa: queda cobertura de CRUD/responsive no recorrida; SMTP externo y decisiones de despliegue delimitables; móvil nativo aplazado por decisión del usuario.

## Handoff posterior: recovery y CI — 2026-11-21

- Rama `chore/stabilize-fleet-platform`; añadido `BackEnd_Gps.Tests/PasswordRecoveryCommandTests.cs`. Suite Release completa: **54/54**.
- Actions backend `44982244087` y frontend `44982243409` finalizaron `success` para los SHA publicados `4cf62a3` y `6e37c4d`.
- `/usr/bin/dotnet` no tiene runtime; usar `/home/leonardo/.dotnet/dotnet` (SDK 8.0.428). No se envió SMTP.
- Próximo: continuar la cobertura real CRUD/responsive restante y actualizar esta documentación. No declarar completo; SMTP requiere credenciales externas y app nativa se aplazó por decisión del usuario.

## Handoff — revocación inmediata de access JWT — 2026-11-21

- Rama `chore/stabilize-fleet-platform`; cambios locales no publicados: `User.TokenVersion`, migración `InvalidateAccessTokensAfterPasswordChange`, claim JWT `token_version`, verificación por request, incremento transaccional en los dos métodos de cambio de password, y smoke HTTP de cambio/login/refresh.
- Suite xUnit Release 54/54; build 0 warnings/0 errores; script smoke pasa `bash -n`. Siguiente: revisar diff, publicar backend y esperar CI (migración MySQL + smoke tenant), corregir/documentar fallos y reflejar el resultado también en el workflow frontend.
- Front UI lifecycle añadido en `96caf15`: Actions `44982723150` passed; E2E integrado Chrome/API/MySQL ejecutado correctamente.

- Acciones terminadas: backend run `44983337062` success para `cff33c6`; migración `TokenVersion` y smoke MySQL de cambio de contraseña pasaron. Access/refresh anteriores 401, login previo 401 y nuevo 200. Workflow frontend `44983346765` aún en curso al último chequeo; verificar antes de continuar.

- Tras el smoke MySQL exitoso (`44983337062`), se añadieron pruebas unitarias para cambio de contraseña correcto e incorrecto. Suite local actual **56/56**. Estos tests aún están sin commit; publicar y esperar Actions backend/frontend.

- Último estado: backend Actions `44983763612` pasó para `afa1108`: xUnit 56/56, restore/build, smokes tenant/roles/password-session y concurrencia MySQL. La migración `TokenVersion` quedó aplicada sólo a la DB desechable CI. Branch aún requiere seguir cobertura CRUD/responsive restante.

## Continuación: invalidación de JWT por cambio de rol — 2026-11-21

- Rama `chore/stabilize-fleet-platform`; sólo backend modificado. `main` intacta.
- `Program.cs` ya validaba `token_version` y roles actuales; el cambio de rol revocaba refresh, pero faltaba incrementar la versión. Un JWT viejo podía volver a autorizar si se restauraba su rol antes de expirar.
- `ChangeUserRoleCommand` incrementa `TokenVersion` sólo si cambió el rol. Actualización y revocación se persisten en el mismo commit. Tests cubren cambio real, no-op y rechazo de cambio con ruta activa sin modificar la versión.
- `smoke-tenant-http.sh` prueba Logistics→User→Logistics y reenvía tokens originales; ambos siguen respondiendo 401. Se añadió `MYSQL_PORT` para DB temporales.
- Validación local: suite Release 57/57; build 0 warnings/0 errores; smoke real API + MySQL 8 con DB `fletflow_ci_*` aprobado. El contenedor se eliminó.
- Error del primer intento: el runner omitía el puerto y conectaba al 3306 local con credenciales temporales no válidas; no se ejecutó SQL ni se modificó DB local. Ver `docs/ERROR_LOG.md`.
- El smoke de creación por Logistics quedó publicado en `43ca30c` y pasó Actions `44988756481`; sin migración. La iteración actual extiende operaciones de estado/evidencia/GPS y tiene su propia validación local documentada abajo.

## Verificación remota de cambio de rol — 2026-11-22

- Commit backend `3f3b004` publicado en `chore/stabilize-fleet-platform`; Actions `44986173700` terminó `success`.
- CI: Release build 0 warnings/0 errors, xUnit **57/57**, smoke tenant/roles/MySQL y smoke de concurrencia aprobados.
- Confirmado en API con MySQL desechable: tokens access y refresh previos siguen en 401 tras Logistics→User→Logistics. La DB y el contenedor temporales se eliminaron.
- La rama está limpia y sincronizada con origin; `main` intacta.

## Smoke de creación de rutas como Logistics — 2026-11-22

- Rama backend `chore/stabilize-fleet-platform`. `scripts/smoke-tenant-http.sh` cubre Logistics creando una ruta con el mismo vehículo y Driver del tenant que creó Manager. Manager crea/edita y da de baja primero; después Logistics crea (201), obtiene el recurso (200) y valida ID/nombre/IDs de asignación/paradas, edita (200), vuelve a leer para verificar nombre persistido y da de baja (204).
- El primer intento detectó un defecto del propio cleanup: el ID Logistics sobrescribía el Manager aunque la baja de Manager es lógica. Se mantienen ahora ambos IDs y el trap limpia ambas rutas; error y solución en `docs/ERROR_LOG.md`.
- Repetición completa aprobada únicamente con MySQL 8 en contenedor y DB desechable `fletflow_ci_*`; cleanup sin fallos, contenedor eliminado y DB local intacta. `bash -n` y `git diff --check` correctos. No se ejecutaron otras suites.
- El commit `43ca30c` ya está publicado en `chore/stabilize-fleet-platform`; Actions `44988756481` completó en success. Build, xUnit y smokes HTTP/MySQL de tenant/Logistics y concurrencia pasaron. `main` sigue intacta y la misión de estabilización global continúa abierta.

## Operación Logistics de inicio a cierre — 2026-11-22

- Diff local pendiente de commit en `scripts/smoke-tenant-http.sh`: extiende la ruta Logistics con operación PickupAndDelivery, transición InProgress, estado/evidencia de parada, posición GPS, Completed y verificación del vehículo Available.
- Smoke pasó contra API temporal en 5058 y MySQL 8 efímero en 13306, DB `fletflow_ci_lifecycle_*`; 201 alta, 200 lectura/edición/parada/evidencia/GPS, 204 inicio/cierre/baja. Cleanup comprobó cero rutas, posiciones y vehículos QA.
- Primer intento falló sólo en el assert de cleanup porque warning MySQL contaminaba stdout; se separó stderr y repetición pasó. Release build 0 warnings; `bash -n` y `git diff --check` limpios. xUnit no se ejecutó en esta iteración.
- `d58c5ab` pasó Actions `44990525889`: build, xUnit 57/57 y smoke tenant/Logistics/concurrencia; el log confirmó operación Logistics y cleanup `0:0:0`. El fallback de cleanup por nombre QA quedó en `da53081`; Actions `44991032520` confirmó build, xUnit 57/57, smoke y cleanup `0:0:0`. El fallback especial cuando falta el ID no se indujo directamente; sí se probó el cleanup normal con el fallback instalado.

## Cambio en curso — placa duplicada y feedback UI — 2026-11-22

- Rama `chore/stabilize-fleet-platform`: CreateVehicle y UpdateVehicle responden conflicto de negocio 409 en lugar de InvalidOperation/400 para placas duplicadas; mensajes en español.
- Añadidas dos pruebas unitarias que confirman rechazo sin agregar/actualizar ni hacer commit. Ejecutado Release con SDK `/home/leonardo/.dotnet`: **59/59**.
- El host `/usr/bin/dotnet` falló inicialmente porque `/usr/lib64/dotnet/host/fxr` está vacío; se repitió correctamente con SDK 8.0.428 desde `/home/leonardo/.dotnet`.
- Frontend añadió E2E móvil que intenta crear una segunda unidad con la misma placa, espera HTTP 409, verifica el mensaje y que quede una sola tarjeta. Lint/build/discovery 16 pasaron; CI integrado sigue pendiente.
- Siguiente: revisar diff, publicar backend y verificar Actions; publicar luego el E2E frontend y comprobar ambos branches integrados con API/MySQL.
- Actions `44995681296` terminó success en `6ec5315`: build, xUnit y smokes tenant/concurrencia HTTP+MySQL completados y recursos temporales detenidos.
