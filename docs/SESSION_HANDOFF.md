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
