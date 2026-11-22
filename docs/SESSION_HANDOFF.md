# Sesión actual — CRUD HTTP de Driver como Manager — 2026-11-22

- Rama `chore/stabilize-fleet-platform`, base HEAD `4718f04`; cambios locales sin commit/push, no tocar `main`.
- Sólo se modificaron `scripts/smoke-tenant-http.sh` y documentación. Fixture Manager usa un Driver/Vehicle dedicado y no reusa los recursos que soportan la ruta Manager ni las rutas Logistics.
- CRUD probado como Manager: edit HTTP 200; baja/reactivación HTTP 204; asignar/desasignar vehículo HTTP 204. Tras cada operación GET Driver/Vehicle y SQL comprueban estado. También se comparan por API y SQL snapshots de `manager_driver`, `manager_vehicle` y `manager_route` para detectar cambios colaterales.
- Build Release 0 warnings/0 errores; xUnit **82/82** con `/home/leonardo/.dotnet/dotnet`; `bash -n`, `git diff --check`, smoke completo con API local + MySQL 8 descartable exit 0. Cleanup confirma cero Driver y Vehicle CRUD Manager y limpieza general. Servicio/contenedor descartables detenidos.
- Las dos fallas de aserciones de desarrollo y sus arreglos están documentados en `docs/ERROR_LOG.md`. Actions backend aún pendiente; revisar diff y CI antes de integrar. No declarar terminada la misión general.

# Sesión actual — smoke tenant Users/asignaciones — 2026-11-22

- Rama backend: `chore/stabilize-fleet-platform`, base `d3ac953`; sin cambios en `main`, sin commit/push.
- Alcance implementado sólo en `scripts/smoke-tenant-http.sh` y documentación: listados Users/team de B excluyen todos los IDs A obtenidos de MySQL (`Id` raíz o `OwnerUserId` de A); opciones de ruta validan Driver propio B; cuatro intentos cross-tenant (assign/unassign Driver A, asociar vehículo A al Driver B y reactivar Vehicle A) esperan HTTP 404.
- Cada intento cross-tenant dispara inmediatamente consulta MySQL de `Drivers.VehicleId`, `Vehicles.IsActive` para vehículo asignable y vehículo dado de baja, y `OwnerUserId`; los valores deben coincidir con baseline. Cleanup confirma cero Drivers, Vehicles y miembro B creados por el escenario.
- Verificación local concluida: Release build 0 warnings/0 errores; xUnit **82/82** con `/home/leonardo/.dotnet/dotnet`; `bash -n scripts/smoke-tenant-http.sh`; `git diff --check`; smoke HTTP/MySQL 8 con exit 0. Cleanup informó cero fixtures A/B, rutas, posiciones y recursos Logistics/Manager; servicio API/contendor desechable detenido.
- El primer `dotnet` global falló por runtime ausente; se registró y resolvió usando SDK .NET 8 de usuario. Actions backend sigue pendiente; revisar el diff y ejecutar CI antes de integrar. No declarar terminada la misión global.

## Iteración actual — exclusividad de asignación al crear/editar ruta — 2026-11-22

- Auditoría read-only encontró que `CreateRouteCommand` y `UpdateRouteCommand` podían asociar un Vehicle todavía asignado a otro Driver, aunque la asignación directa ya devolvía conflicto.
- Ambos comandos ahora vuelven a comprobar `GetOtherAssignedToVehicleAsync` después del lock/reload y lanzan `BusinessConflictException` antes de modificar datos. xUnit Release **82/82** pasa; dos regresiones SQLite comprueban que creación/edición conflictiva no mutan ruta/asignaciones.
- `smoke-route-concurrency.sh` verifica HTTP 409 en ambas entradas y estado SQL intacto; `bash -n` y `git diff --check` pasan. Actions `45032172895` terminó success en backend `8e76f80`: Release build, xUnit **82/82**, tenant HTTP/MySQL y smoke de concurrencia; cleanup success.
- La primera suite completa falló 81/82 porque el caso de doble inicio construía dos conductores asignados al mismo vehículo, contrario a la regla documentada. Se actualizó el fixture a mismo Driver/Vehicle con agendas separadas; el resultado final fue 82/82. Registrado en `ERROR_LOG.md`.
- Actions `45031869323` pasó build/xUnit/tenant HTTP, pero el smoke de concurrencia falló por reutilizar v2/d2 asignados por un caso anterior. Script corregido para aislar recursos; repetición `45032172895` success, incluidos los dos conflictos HTTP/SQL y cleanup.
- Front también filtra la opción ya asignada y añade E2E de ciclo de visibilidad. Mantener ambas ramas aisladas y no cerrar la auditoría global.

## Trabajo actual — CRUD de perfil de miembro — 2026-11-22

- Backend añade campos nullable FirstName/LastName a User, DTO y mapper. Migración AddUserProfileNames copia los nombres existentes desde Drivers enlazados, sin reemplazar valores no vacíos.
- PUT /api/Users/{id}/profile valida rol, propietario, raíz, campos y longitudes <=100; bloquea la cuenta raíz. Email permanece inmutable. Los nombres cambian sincronizados User y Driver en una única unidad de trabajo.
- UpdateDriverCommand sincroniza User sólo si ambos registros comparten OwnerUserId.
- Build Release pasa 0 warnings/0 errores y xUnit 62/62. smoke-tenant-http.sh y smoke-route-concurrency.sh pasaron contra MySQL 8 efímero; el primero confirma cross-tenant 404, raíz 400 y cleanup sin residuos.
- Front implementa diálogo Editar miembro y expone updateUserProfile; E2E verifica persistencia en GET /Users/my-team y edición Driver->User y User->Driver.
- Dos corridas E2E iniciales dieron 16/18 y 17/18 por selectores/mocks frágiles, documentadas en ERROR_LOG. Tras corregirlas, la suite final pasó 18/18 en Chrome sobre API/MySQL real; run-e2e-local terminó código 0 y el esquema descartable se eliminó.
- Errores y soluciones están al final de docs/ERROR_LOG.md.
- Commit/push backend e54d3c8, Actions 45005400763 success en build, xUnit, tenant HTTP/MySQL y concurrencia. Front commit 948b2e3 y Actions integrada 45005400590 en ejecución al último corte. Misión general sigue abierta.

## Seguridad de perfil — continuación 2026-11-22

- Auditoría estática real (subagente independiente) no encontró bypass directo en `PUT /Users/{id}/profile`, pero recomendó defense-in-depth dentro del command. Implementado: comando filtra por `CurrentOwnerUserId` y agrega owner scope al Driver ligado.
- xUnit local Release **64/64**; smoke sintáctico y diff-check limpios. Actions backend `45007491905` confirmó build, 64/64, smoke HTTP/MySQL con límites de nombre/apellido y concurrencia; cleanup exitoso.
- Hallazgo de negocio abierto: Admin delegado puede asignar/promover rol Admin. No modificar esa capacidad hasta resolver la política raíz/delegado. El frontend también tiene esta duda pendiente.
- Error conocido y solución en `docs/ERROR_LOG.md`. Próximo: continuar auditoría de CRUD/roles; mantener sin resolver la promoción Admin hasta recibir decisión de producto.

## GPS y autorización Driver — backend corregido — 2026-11-22

- Auditor independiente (solo lectura) confirmó el ownership de lista/detalle/estado/paradas/evidencia y filtros tenant, pero halló que GPS no validaba `Driver.IsActive` y faltaban pruebas HTTP de ruta ajena por Driver para paradas/evidencia/GPS.
- Backend `2fdc9e0` centralizó GET/POST posiciones con `IsCurrentDriverAssigned`; `41c01e7` aplicó el mismo helper a `GET /Routes/{id}` para impedir lectura de detalle con perfil desactivado. Release/xUnit local 61/61; Actions `45002562048` pasó build, 61/61, smoke tenant/HTTP y concurrencia. El smoke existente no cubre por sí mismo el nuevo caso de perfil Driver inactivo.
- Front `refactor/route-builder` agregó el E2E de ruta ajena, paradas/evidencia/GPS y perfil inactivo/reactivado (lista/detalle/GPS); Actions `45002920723` pasó Playwright 18/18 con backend `41c01e7`, incluyendo persistencia sin mutaciones y cleanup. La segunda contiene el ajuste de assert de lista no vacía permitido.
- Al pasar Actions, registrar códigos exactos y cleanup. No cerrar criterio hasta que prueba HTTP/browser compruebe estado/evidencia/GPS persistentes tras denegación.

## Autorización Driver/GPS — validación pendiente — 2026-11-22

- Auditor independiente de solo lectura confirmó filtros tenant y controles Driver para detalle, estado, paradas y evidencia; faltaba cobertura HTTP automatizada de otros conductores en paradas/evidencia/GPS.
- Hallazgo concreto: `GET/POST /Routes/{id}/positions` omitían `Driver.IsActive`. `RoutesController` ahora carga ruta por repositorio tenant y aplica `IsCurrentDriverAssigned`, cerrando acceso GPS del perfil Driver desactivado.
- El E2E Front Driver se amplió para intentar lista, detalle, estado, parada, evidencia y GPS de una ruta de otro Driver del mismo tenant, y para comprobar que denegar no muta datos; también desactiva/reactiva el perfil con JWT vigente y valida bloqueo GPS.
- Próximo: build/xUnit backend, lint/build y Actions Front integrado con E2E real; no marcar autorización cerrada antes del resultado.

## Alta concurrente Driver verificada — 2026-11-22

- Backend `chore/stabilize-fleet-platform` corrigió el smoke para reflejar contrato actual: una solicitud de alta responde 200 (se admite 201 si cambia el endpoint) y la carrera perdedora 409. SQL exige exactamente una fila Users, Driver y PasswordResetToken.
- Actions `45001599626` pasó: build, xUnit 61/61, smoke HTTP/MySQL tenant/roles/alta Driver y concurrencia de asignación; cleanup también success.
- El test inicial falló sólo por esperar 201 cuando el controlador devolvió 200. La causa, 404 del primer dispatch y solución quedaron en `docs/ERROR_LOG.md`.
- HEAD backend `6408988`; rama sincronizada. Front `1c108cc` está validando E2E responsive Vehicle en Actions `45001695962`.

# Session handoff

## Onboarding público y alcance del rol Admin — validado

- Hallazgo: `/register` dice “Registra tu equipo como Gerente” y el frontend enviaba `roleName=Admin`, pero `AuthController.Register` ignoraba el campo y asignaba `User`. La sesión se creaba, pero la cuenta no podía administrar la empresa.
- Cambio local en `chore/stabilize-fleet-platform`: el backend fija Admin como cuenta raíz de cada tenant nuevo; nunca usa el rol del request. `AuthService` emite `owner_id=user.Id` al no haber `OwnerUserId`, y el query filter de la flota confina operaciones a ese ID.
- xUnit controller solicita `Driver` y comprueba rol fijo Admin. Smoke HTTP/MySQL verifica rol Admin y listado de Vehicles propio vacío. El runner deja de reescribir manualmente a Admin el rol del tenant B.
- Front elimina roleName del payload, usa los nombres capturados para username y añade E2E UI de registro móvil: consulta `/Auth/me`, confirma rol Admin, flota 200 vacía y navegación a Vehículos/Rutas.
- Validación local: Backend Release 61/61; `bash -n` smoke/runner y `git diff --check` pasan. Actions backend `44999697740` success con build, xUnit 61/61, smoke tenant/registro y concurrencia MySQL.
- Front lint/build/discovery 18 pasan localmente; Actions cross-branch `44999716080` finalizó success usando Backend `3620cc2`: xUnit 61/61, audit/lint/build, responsive 1/1 y Playwright 18/18 con API/MySQL y cleanup success.
- Se habilitó `workflow_dispatch` en ambas CI para correr sobre las ramas aisladas; ambos workflows están en verde.
- El hallazgo/solución y errores de inspección de rutas se documentan en `ERROR_LOG.md` de Front y Back.

## Cobertura HTTP de ownership de usuarios — validada

- Auditor independiente sugirió probar aislamiento de estado/rol de usuarios, protección del propietario raíz y límites de Driver; no modificó archivos.
- Smoke HTTP pendiente de Actions: tenant B intenta cambiar estado/rol de un User de A, incluidos alias legado; Admin A intenta mutar su propia cuenta raíz. El script requiere 404/400 y verifica persistencia del rol/estado con SQL read-only.
- Actions `45000893415` pasó: build, xUnit 61/61, smoke HTTP/MySQL y concurrency success. Primer intento falló sólo porque MySQL formateó TINYINT como byte de control en el assert; CASE string corrigió el test, detalle en ERROR_LOG. Siguiente en ese punto: atomicidad Driver (validada después) y autorización Driver/GPS (ahora corregida en código, con E2E Front pendiente en Actions).

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

## Integridad de documento de Driver — 2026-11-22

- CreateDriver/UpdateDriver y `POST /Users` ahora devuelven HTTP 409 para documento duplicado, sin crear usuario parcial, en vez de 400. Mensaje uniforme en español.
- xUnit Release local 61/61; E2E Admin móvil agregado para mismo documento/otro correo: comprueba 409 y ausencia de email parcial. Front lint/build/discovery 16 correctos.
- Pendiente publicar ambas ramas y verificar Actions cross-branch, persistencia y cleanup.
- Actions `44997043103` para `a7f7c4e` success: build, xUnit 61/61, smokes tenant/concurrencia con MySQL efímero y cierre de servicios; la validación Front cross-branch sigue.
- Actions `44997054375` validó cross-branch con Front `1bb7d71`: backend `a7f7c4e`, xUnit 61/61, E2E 16/16, responsive 1/1 y cleanup completos. Se probó rechazo de documento duplicado 409 antes de crear cuenta. Continuar los criterios pendientes CRUD/roles/responsive.

## Alta Driver atómica bajo concurrencia — pendiente Actions

- Hallazgo: el comando de registro confirmaba Users antes de insertar Driver y PasswordResetToken; una carrera de documento podía persistir una cuenta sin conductor.
- `RegisterUserCommand.Execute` permite diferir el commit; UsersController crea user/Driver/token y confirma una vez. AuthService agrupa user/Driver antes del flujo de token. Unique-document conflict posterior al precheck se devuelve como 409.
- `smoke-tenant-http.sh` envía dos `POST /Users` paralelos con un documento y verifica HTTP 201/409 + conteos MySQL `1:1:1`; limpieza incluye ambos emails/driver.
- Release local 61/61, shell syntax y diff-check pasan. El smoke MySQL de Actions queda pendiente.


## Auditoría de dependencias repetida — 2026-11-22

- En `chore/stabilize-fleet-platform`, `dotnet list BackEnd_Gps.sln package --vulnerable --include-transitive` terminó con código 0; sin vulnerabilidades en Backend ni Tests.
- Quedan CRUD/roles por auditar; política de Admin delegado pendiente de respuesta. No marcar la misión como completa.


## Auditoría backend — siguiente bloque — 2026-11-22

- `scripts/smoke-tenant-http.sh` tiene cambios locales no publicados en `chore/stabilize-fleet-platform`: pruebas cross-tenant de actualización de estado/evidencia de parada y ciclo positivo HTTP/MySQL Driver (CRUD/status/filter/assignment).
- Validación local: Release 64/64; `bash -n`; MySQL 8 descartable; smoke completo aprobado y cleanup verificado. Publicar en la rama y confirmar Actions antes de contar el criterio.
- Decisión pendiente de producto: si Admin delegado puede promover a miembro a Admin. No alterar el permiso ni escribir expectativa de test hasta definirlo.


- Actions backend `45009348293` confirmó el smoke tenant/Driver y cross-tenant de paradas: `success`, xUnit **64/64**, build y smoke de concurrencia completos, limpieza correcta. HEAD de código `c6f74f8`.


## Siguiente validación backend — filtros temporales GPS — 2026-11-22

- Cambio local no publicado: smoke tenant ahora valida GET positions en intervalo inclusivo, `from`/`to` individual, sin coincidencias e invertido (HTTP 200 `[]` en rangos sin datos).
- Local Release 64/64, bash syntax, diff-check y smoke completo MySQL 8 efímero pasaron; cleanup correcto. Commit/push y Actions quedan pendientes.


- Filtros GPS completados en Actions: run `45009987648`, commit `729f326`, success; Release/xUnit 64/64, smoke tenant y concurrencia limpios. Caso `from > to` documentado como HTTP 200 `[]`.
# Handoff — Endpoint de opciones de asignación para Logistics — 2026-11-22

- Rama `chore/stabilize-fleet-platform`, HEAD antes de esta iteración `36e55cb`; cambios locales pendientes: RoutesController, DTO/query nuevos, smoke y documentación.
- Causa: el frontend de Rutas pide recursos a `/Vehicles` y `/Drivers`, prohibidos para Logistics aunque puede crear rutas. Endpoint GET `/Routes/assignment-options` expone sólo campos mínimos y roles Admin/Manager/Logistics.
- Tenant: `FleetFlowDbContext` query filters por `CurrentOwnerUserId` en Vehicle/Driver/Route; smoke verifica Logistics propietario y tenant B vacío, así como User 403 y no exposición de documento/teléfono.
- Verificación local con `/home/leonardo/.dotnet/dotnet` y `BackEnd_Gps.sln`: Release build 0 warnings/0 errors, xUnit 64/64, `bash -n`, diff-check. Commit `6def8fa` publicado; Actions `45011796536` success: smoke tenant/Logistics y concurrencia, cleanup correcto.
- Front `d41a07b` ya está publicado y corre Actions `45011806008` con Playwright completo usando backend `6def8fa`; verificarlo y documentar el resultado.
- No marcar auditoría global como completa: CRUD/roles/responsive siguen progresivos; política Admin delegado pendiente; SMTP externo; app nativa aplazada.
# Handoff — elegibilidad real de opciones de asignación — 2026-11-22

- `chore/stabilize-fleet-platform`, HEAD de código `648b23f`; `IDriverRepository.GetAssignableAsync` filtra elegibilidad por estado y cuenta activa dentro del tenant.
- Hallazgo: endpoint exponía vehículos OnRoute/Maintenance y Drivers con User inactivo, aunque `CreateRouteCommand` los rechaza. La traza del agente está en contexto del coordinador; documentada en ERROR_LOG.
- Local: `/home/leonardo/.dotnet/dotnet build BackEnd_Gps.sln -c Release` 0 warnings/errors; xUnit 65/65; bash -n/diff-check correctos.
- Actions `45014146500` success en `648b23f`: smoke HTTP/MySQL confirma sólo opciones asignables, aislamiento/concurrencia y cleanup de cero rutas/posiciones/vehículo/Driver/User QA.
- Actions Front integrada `45014430986` success en `7bf1955` con Back `648b23f`: backend Release/xUnit, audit/lint/build, responsive 1/1, E2E 18/18 y cleanup completo.
- Continuar auditoría; Manager y Logistics ahora tienen rol UI separado. Otros CRUD/responsive pendientes, Admin delegado sin política aprobada, SMTP externo y app nativa pospuesta.

## Alta Driver: límites documento/teléfono — 2026-11-22

- Rama `chore/stabilize-fleet-platform`: modificación local en `CreateDriverCommand` normaliza y valida campos requeridos y límite de 20 antes de persistir; `DriverTests` suma casos de 20/21 caracteres.
- Validación local actual: Release 0 warnings/0 errors; xUnit 68/68; `git diff --check` pasa. Actions/smoke HTTP/MySQL pendiente después del commit.
- Actions `45016257332` confirmó el SHA base `24a9703`: build/test y smokes tenant/concurrencia pasan (xUnit 68/68). Después agregué la normalización null-safe de documento y cuatro casos de campos obligatorios; repetir build/test y Actions con este diff.
- Repetición local para `8c28ccb`: Release build 0 warnings/0 errores y xUnit **72/72**. Actions `45016639356` pasó build/tests y smokes HTTP/MySQL de aislamiento, roles, operación Logistics, concurrencia y cleanup.
## Asignación única de vehículo a Driver — 2026-11-22

- Auditor backend detectó una violación reproducible: dos Drivers podían apuntar al mismo vehículo porque el command tomaba lock pero omitía consulta de ocupación.
- Cambio local: nuevo método tenant-filtered de IDriverRepository, llamado después de adquirir lock; conflicto explicativo si hay otro Driver asignado. Regla documenta desasignar antes de reasignar.
- Cobertura: xUnit y smoke smoke-route-concurrency.sh con dos PUT simultáneos y SQL que exige una relación. Ejecutar Release/test, shell syntax, smoke local efímero y Actions; registrar errores.
- Estado local: Release build 0 warnings/0 errors; xUnit 73/73; script pasa bash -n y diff-check. Ejecutar Actions sobre este cambio para la prueba concurrente MySQL y registrar resultado.
- Cerrado en `8d19118`; Actions `45017642439` success: Release/xUnit 73/73, smokes HTTP/MySQL de tenant, roles y operación, asignación simultánea con unicidad persistida y cleanup completo. Próximo: mantener auditoría abierta y cubrir brechas restantes.
- Trabajo local nuevo: fecha real `CreatedAtUtc` aditiva (nullable para legacy), UTC al crear, preservada al editar, DTO/repository ordenan datos. `smoke-tenant-http.sh` exige timestamp en respuesta/MySQL y compara respuesta antes/después de editar. Release 73/73; Front lint/build/discovery 18. Publicar ambos y exigir Actions integrado antes de cerrar.
- Actions `45019828759` detectó comparación string falsa negativa por precisión: .NET puede devolver ticks de 100 ns, MySQL `datetime(6)` persiste microsegundos. El smoke ahora normaliza API a seis decimales y compara además `DATE_FORMAT` en DB antes/después de editar. Repetir `bash -n`, commit/push y Actions.


## Fecha real de creación — backend validado — 2026-11-22

- Backend `e31e3cc` corrige el test de precisión; Actions `45020146775` terminó success: Release build, xUnit **73/73**, migración `CreatedAtUtc` aplicada en MySQL, smoke HTTP/API contra valor persistido, edición preserva timestamp, concurrencia y cleanup.
- Error del primer run `45019828759` documentado y resuelto normalizando a microsegundos según `datetime(6)`.
- Front `cacb72e` ya publicado; Actions integrada `45020301540` ejecuta audit/lint/build, responsive y E2E Chrome/API/MySQL. Revisar resultado antes de cerrar fecha real en frontend.
- Continúa pendiente política de Admin delegado; trabajar brechas independientes.

## Seguridad: rotación de access JWT y Roles globales — 2026-11-22

- Auditoría independiente encontró access JWT que podía revivir tras reactivar una cuenta: `UpdateUserCommand` revocaba refresh al desactivar, pero no cambiaba `TokenVersion`. Se corrigió incrementando la versión en cada transición efectiva; repetir estado no la cambia.
- `RolesController` permitía POST/DELETE a Admin de tenant sobre tabla global. No existe rol de plataforma y Front no consume mutaciones; GET queda disponible y POST/DELETE ahora deniegan 403.
- xUnit Release **74/74**; API + MySQL 8 efímero pasó `smoke-tenant-http.sh`, incluyendo mismo token viejo 401 post-reactivación y nuevo login 200, y protecciones Roles. Cleanup verificado cero rutas/posiciones/vehículos; contenedor debe detenerse al finalizar sesión.
- Actions backend de este SHA aún pendiente. Código actual en cambios locales: `UpdateUserCommand.cs`, `RolesController.cs`, `UpdateUserCommandTests.cs`, `smoke-tenant-http.sh`.
- Backend `eaeb495` quedó validado por Actions `45021088919`: Release/xUnit 74/74, smoke API/MySQL (incluye token viejo tras reactivación y Roles) y concurrencia, cleanup correcto.
- Auditoría Front independiente encontró brechas del formulario/selector duplicado en TeamManagement; se está agregando validación y filtrado y el E2E API/MySQL correspondiente.
- La política de promoción a Admin delegado sigue pendiente de respuesta; no cambiar esa regla.

## Continuación — sesión, recuperación y carga de flota — 2026-11-22

- Trabajo activo en `chore/stabilize-fleet-platform` y `refactor/route-builder`, sin tocar main.
- Backend cambios locales: rotación atómica concurrente de refresh, logout-all invalida access JWT mediante TokenVersion, ForgotPassword uniforme y tolerante a fallo SMTP. xUnit previo 76/76; smoke HTTP/MySQL 8 local aprobado y cleanup 0 rutas/posiciones/vehículos. Repetición de xUnit con runtime correcto está en curso.
- Front cambios locales: error de `/Vehicles` visible en Gestión de Conductores con botón Reintentar; nuevo E2E prueba los errores 503 de Drivers/Vehicles y la recuperación. El E2E enfocado pasó 1/1; lint/build y discovery 19 pasaron.
- Próximo: capturar final del xUnit; revisar diff y documentación; validar shell/diff; commit/push de ambas ramas; observar Actions backend y Front cross-branch y corregir cualquier regresión.
- Pendiente de decisión, no modificar sin respuesta: si Admin delegado puede promover a Admin o si sólo cuenta raíz puede hacerlo. SMTP real requiere credenciales externas; móvil nativo está pospuesto.
- Errores operativos de esta iteración y remedios constan en `ERROR_LOG.md` (runtime .NET del sistema y hunk de script rechazado).

### Resultados locales cerrados — 2026-11-22

- Backend Release xUnit **76/76**, 0 omitidas; Release build **0 warnings/0 errores**; sintaxis smoke y diff-check limpios.
- Smoke HTTP/MySQL de tenant, sesión, refresh concurrente y operación de flota: exit code 0; exacto cleanup `0 rutas, 0 posiciones GPS, 0 vehículos Logistics/Manager`.
- Front lint/build Next 16 pasaron, Playwright discovery **19 casos**, E2E focal nuevo **1/1**.
- Smoke adicional de concurrencia de rutas se está ejecutando; guardar resultado antes del commit.
- Smoke `scripts/smoke-route-concurrency.sh` finalizó exit code 0: agenda/asignaciones y ciclos de rutas/paradas concurrentes mantienen integridad.

### Cierre de hallazgo de logout-all concurrente — 2026-11-22

- `IUnitOfWork.LockUserSessionAsync` bloquea User en transacción; `TryRotateAsync` toma el mismo lock antes de rotar refresh. Incremento TokenVersion + revocación refresh + commit forman una operación serializada.
- Auditoría de `Program.cs` confirmó que JwtBearer `OnTokenValidated` compara TokenVersion/rol/estado; el finding de ausencia de validación surgió al leer solamente `JwtMiddleware`. Smoke real comprobó access anterior 401.
- Smoke tenant/http reforzado bloquea fila User y lanza refresh + logout-all al mismo tiempo; quedó exit 0 con cero refresh activos, sucesor rechazado si lo hubo y cleanup cero.
- xUnit Release después de cambio: 76/76; smoke de concurrencia de rutas también exit 0. Front lint/build pasaron y E2E de gestión recupera vehículo seleccionable; su última corrida enfocada pasó 1/1.
- Pendiente antes de completar seguridad: timing de enumeración en ForgotPassword (mismo body, trabajo/respuesta temporal aún distintos); además sigue abierta decisión sobre promoción a Admin delegado. SMTP externo requiere credenciales vigentes; app nativa sigue pospuesta.

## Próxima iteración — worker de correo y timing de recuperación — 2026-11-22

- Backend último commit remoto `bcf7bac`; Actions `45023615413` terminó `success`. Front último commit de producto `42b1b4b`, Actions `45023625523` terminó `success`; commit documental siguiente `bb028a9` publicado.
- Cambios backend locales sin publicar: `EmailDeliveryQueue` bounded con background worker, SMTP ya no bloquea ForgotPassword, respuesta JSON igual y mínimo 250 ms en cuenta conocida/desconocida; smoke HTTP compara códigos/payload y exige >=200 ms.
- Validación local posterior: Release xUnit 76/76, Release build 0 warnings/errors, smoke API/MySQL 8 exit 0; el worker registró SMTP sin configurar, y no se afirmó envío. Repetir Actions con SHA nuevo antes de cerrar.
- Riesgo abierto en ese corte: cola en memoria y piso temporal. El outbox protegido quedó implementado en la iteración más reciente de este handoff; timing sigue mitigado parcialmente y SMTP real requiere credenciales externas.
- Otros pendientes: decisión del usuario sobre promoción de Admin por cuentas delegadas, cobertura CRUD/responsive restante, y no cerrar misión.
- Backend commit `35ad393` quedó validado por Actions `45024262135`: Release build 0 warnings/0 errores, xUnit 76/76, smoke MySQL/API success y cleanup `0 rutas/posiciones/vehículos`.

## Nueva iteración — altas robustas y matriz Driver — 2026-11-22

- Auditoría multiagente independiente halló P2: email duplicado concurrente podía caer en 500, límites User/Email/FirstName/LastName no validaban y faltaba prueba Driver de assignment-options.
- Backend local: RegisterUserCommand trim/limits 100/200/100/100; UserController 400 para límites y 409 sólo al confirmar email duplicado tras DbUpdateException. Smoke MySQL real verifica límites, no persistencia inválida y carrera 200/409 con exactamente User/Role/PasswordResetToken; Driver options 403. Cleanup de fixtures completo.
- Front local: `TeamManagement` maxLength y validación client-side trim/longitud; E2E de TeamManagement verifica atributos. `npm run lint`, `npm run build`, discovery 19 pasaron después de quitar props duplicadas detectadas por lint.
- El primer smoke falló por una expectativa vieja que asumía username/nombres de fixture cortos; expectation ajustada a valores máximos, repetición completa exit 0. Error registrado.
- Pendiente en ese corte: browser E2E y Actions para esos SHA. El outbox quedó implementado después; la validación Actions del nuevo commit sigue pendiente. Política Admin delegado aún abierta.

## Outbox durable SMTP — lote local 2026-11-22

- HEAD previo: backend 8d3cb32 (chore/stabilize-fleet-platform), frontend 000d6cb (refactor/route-builder); ambos Actions previos 45025304021 y 45025319275 terminaron success.
- Backend local cambia EmailDeliveryQueue a escritor scoped que agrega payload cifrado al AppDbContext; ForgotPasswordAsync agrega token y mensaje y hace commit conjunto, con lock User para serializar requests simultáneos.
- EmailOutboxWorker reclama filas con update condicional/lease/LockToken, SMTP fuera de transacción, backoff 2^attempt capped 6h, dead-letter a 12 y purga de enviados 30d/dead-letter 90d. Nueva migración EF AddEmailOutbox.
- Configuración: FLETFLOW_DATA_PROTECTION_KEYS_PATH; en producción montar key ring persistente y compartido por réplicas con ApplicationName FletFlow. SMTP real externo pendiente; at-least-once permite duplicado en crash tras aceptación del proveedor.
- Tests locales después del lote: Release xUnit 77/77; smoke API+MySQL 8 real aprobado con cifrado, dos requests reset simultáneos, token activo único y cleanup completo. bash -n y diff-check correctos. La corrida usó el SDK explícito /home/leonardo/.dotnet/dotnet.
- Errores de migración (namespace faltante), runtime global incorrecto y orden de limpieza rechazada están en ERROR_LOG con prevención.
- Próximo: auditar diff completo/worker, ejecutar dotnet build Release y smoke de concurrencia de rutas en MySQL; publicar cambios backend en la rama; esperar Actions. Luego continuar pendientes CRUD/responsive front y backend. No cerrar misión. Política de promoción de Admin delegado continúa sin respuesta.

- Verificación adicional ya terminada: restart API con DB + key ring persistentes confirmó que EmailOutbox se descifra y se reintenta después del reinicio; sender llegó al fallo esperado SMTP no configurado. Smoke separado de concurrencia de asignación de rutas también exit 0.
- El primer assert de restart esperaba un contador fijo y falló; se cambió por baseline comparativo y la repetición pasó. ERROR_LOG incluye ambos resultados.

- CI backend cerró success después de la publicación: Actions 45027017954 para 5049dea, Release build sin warnings, xUnit 77/77, smoke tenant/outbox y concurrencia de rutas completos; cleanup de servicio MySQL descartable success.
- Actions Front 45027026835 fue success en 82941b8 y Backend 5049dea, con backend/lint/build/responsive/E2E integrados. No incluye los nuevos cambios Front aún locales.
# Continuación — revalidación de Driver bajo lock — 2026-11-22

- Auditoría independiente de RoutesController encontró ventana TOCTOU en PUT `/Routes/{id}/status`: controller autorizaba con la asignación anterior, command bloqueaba y recargaba sin comparar nuevamente al Driver actor.
- Backend cambia `ChangeRouteStatusCommand.Execute` para recibir el Driver/User solicitante y repetir la autorización post-lock/post-reload, incluyendo Driver activo. Nueva prueba simula reasignación justo al adquirir lock y afirma que no hay cambio de estado.
- Suite Release local 78/78, test nuevo 1/1 y build Release 0 warnings/0 errors. Error inicial CS1061/CS9113 del fixture corregido y documentado en ERROR_LOG. Actions `45029374847` validó Backend `5103b51` con HTTP/MySQL y cleanup.
- Front relacionado está limpio en `505d6c3`; Actions integrada `45028577152` pasó previamente en `da70521` con backend `f2f18cd`, E2E 20/20. El commit Front posterior sólo actualizó documentos.
- Misión activa: continuar criterios restantes de CRUD/roles/responsive y validación externa del worker outbox. Política Admin delegado sigue pendiente de decisión; SMTP requiere configuración externa y app móvil nativa está aplazada.
- Outbox automatizado adicional: dos pruebas SQLite ejecutan EmailOutboxWorker real con sender controlado y verifican entrega/descifrado, marcación enviada y dead-letter en intento 12. Actions `45030208849` pasó para `43c074d`: suite **80/80**, build, HTTP/MySQL y cleanup.
- Actions `45029094282` (Backend `813cedb`) detectó el normalizador frágil de timestamp del smoke `CreatedAtUtc`: build/tests pasaron, HTTP smoke falló al comparar luego de editar. Script canoniza seis decimales y agrega diagnósticos; Backend `5103b51`, Actions `45029374847` pasó build, xUnit 78/78, HTTP/MySQL y concurrencia, cleanup success.
- Actions `45029798323` validó el smoke ampliado en Backend `ec3ce52`: actor Driver de mismo tenant fue denegado en rutas/estado/paradas/evidencia/GPS ajenos; Admin confirmó que no hubo mutaciones. Release, xUnit 78/78, smoke y cleanup success.

## Hallazgos de seguridad para el siguiente corte — 2026-11-22

- Backend `chore/stabilize-fleet-platform` tiene cambios locales sin commit para tres findings: rate limit race, `Auth/test-email` arbitrario y cifrado del key ring. xUnit Release 85/85, build 0 warnings/0 errors, test enfocado de 200 requests concurrentes pasó 60 accepted/140 throttled; revisar diff, ejecutar smoke/Actions y documentar antes de publicar.
- Production ahora requiere key ring persistente + certificado PFX (env `FLETFLOW_DATA_PROTECTION_KEYS_PATH`, `FLETFLOW_DATA_PROTECTION_CERT_PATH`, opcional password); registrar que la provisión de secretos/volumen depende del entorno externo.
- Auditoría subagente de solo lectura: encontró permiso Admin delegado sin política resuelta; `GET Auth/test-email` de destinatario arbitrario (fix local); rate limit counter race (fix local); keys Data Protection sin protección explícita (fix local). No detectó IDOR cross-tenant en superficies inspeccionadas; JWT/refresh/CORS tienen controles revisados.
- Pregunta de política enviada al usuario: si sólo cuenta raíz puede otorgar Admin o cualquier Admin. No modificar UserController hasta respuesta; continuar trabajo independiente.
- Front: `a75914f` publicado. Actions `45036249291` pasó para `4e3ddc8` (E2E 21/21); `45036661541` valida `a75914f` con el nuevo E2E de error/retry dashboard Logistics, aún en curso al último chequeo.
- No marcar misión completa; seguir con Actions, seguridad, CRUD/responsive pendiente. SMTP real necesita credenciales externas y móvil nativo fue aplazado.

- Revisión local posterior al primer commit de seguridad agregó saturación del contador en 61 para evitar overflow bajo una ráfaga extrema; Release xUnit **85/85** y build **0 warnings/0 errors** volvieron a pasar. Esta pequeña corrección está pendiente de commit y de la próxima Action.

- Corrección de saturación incluida y publicada en Backend `94c3e90`; el código validado localmente es 85/85 y build limpio. Actions del SHA `94c3e90` debe confirmarse antes de usarlo como evidencia remota.
- Verifiqué en local el guard de arranque `Production`: al configurar key ring sin certificado, la API rechaza el inicio con el mensaje requerido y no llega a DB. El primer assert de shell buscó el mensaje en inglés y falló; se corrigió a la frase real en español y pasó, documentado en ERROR_LOG.
- Actions `45037399332` confirmó Backend `94c3e90`: build Release 0 warnings/errors, xUnit 85/85, tenant/roles/flota HTTP/MySQL, concurrencia y cleanup success. El run validó la corrección de saturación del rate limiter. Documentación de esta evidencia queda por publicar.

## Test del key ring Data Protection

- Nueva prueba `BackEnd_Gps.Tests/DataProtectionKeyRingTests.cs`: certificado autofirmado efímero, persistencia cifrada `<encryptedSecret`, restart provider y recuperación del payload. Limpia directorio aislado en `finally`.
- Local después del test: Release 86/86, build 0 warnings/0 errors y diff-check limpio. Falta Actions del commit; el último CI validó 85 tests y no incluía esta prueba.
- Front Actions `45038687601` validó Front `f972e19` con checkout Backend `b625c28`: Release/xUnit 86/86, responsive 1/1, E2E API/MySQL 23/23, audit/lint/build y cleanup. Incluye el E2E de error/retry Vehicles y test backend de key ring cifrado.

## Subtarea backend — concurrencia real y rotación de certificado — 2026-11-22

- Scope exclusivo: Backend `/home/leonardo/Back_GPS`, branch `chore/stabilize-fleet-platform`; no push ni cambios frontend.
- `ConcurrentRequests` alinea 200 solicitudes a una barrera async por una misma IP, mide solapamiento en handlers y exige 60 aceptadas / 140 rechazadas.
- `Program.cs` acepta opcionalmente `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PATH` y `_PASSWORD` para descifrar las claves previas; el certificado de `FLETFLOW_DATA_PROTECTION_CERT_PATH` protege claves nuevas. Variables vacías añadidas a `.env.example`.
- La prueba de rotación escribe key ring/payload con certificado anterior y abre provider con actual + anterior; verifica payload legado y round-trip nuevo.
- Política Admin delegado sigue pendiente; esta subtarea no cambia roles.
- Un primer escenario de test intentó leer el key ring mixto usando sólo certificado actual y falló por no cargar el fallback; corregido para el provider actual + anterior. Se retiró `Console.WriteLine` del XML cifrado.
- Commit backend `b664b2c` publicado en `chore/stabilize-fleet-platform`. Pruebas enfocadas 2/2; suite Release **86/86**; build Release 0 warnings/0 errores; diff-check limpio. Actions `45040550853` pasó: Release/xUnit, tenant isolation HTTP/MySQL, concurrencia de asignación de rutas y cleanup.
- La primera ejecución local con `dotnet` del PATH no inició porque `/usr/lib64/dotnet/host/fxr` está vacío. Solución: usar el SDK instalado en `/home/leonardo/.dotnet/dotnet`.
- Una primera prueba de suite durante la iteración del test de rotación devolvió `CryptographicException` con mapeo de línea distinto al fuente actual; el test enfocado serializado y luego la suite completa pasaron. Evitar builds/tests simultáneos que escriban los mismos `bin/obj`; repetir con `-m:1` para confirmar.
- Se consultó el Actions `45039605708` desde el checkout Backend, donde `gh` apunta a `Paul1578/Back_GPS`; devolvió 404 sin modificar estado. Solución: consultar desde `/home/leonardo/Front_gpsR`, repo dueño de esa corrida.

## Continuación — cuenta vinculada a Driver y ruta activa — 2026-11-22

- Rama Backend `chore/stabilize-fleet-platform`; árbol con cambios locales no publicados. Auditoría independiente read-only encontró que se podía iniciar ruta de un Driver cuyo perfil estaba activo pero su cuenta User enlazada desactivada; el vehículo quedaba OnRoute para una cuenta sin acceso.
- `ChangeRouteStatusCommand` valida `User.IsActive` después de obtener locks de asignación y bloquea el User vinculado bajo el mismo transaction/orden Driver→User. `UpdateUserCommand` bloquea Vehicle/Driver/User, relee relación y rechaza la baja con 409 si hay ruta InProgress. Cuenta/token no cambian ante el conflicto.
- `IUnitOfWork.LockLinkedUserForRouteAssignmentAsync` ejecuta `SELECT ... FOR UPDATE` owner-scoped dentro de la transacción de asignación MySQL; para providers sin bloqueo de filas conserva ejecución funcional sin SQL específico.
- xUnit: test start rechaza cuenta inactive dejando Route Pending y Vehicle Available; test user status rechaza baja durante ruta InProgress y conserva User activo, Route InProgress, Vehicle OnRoute y refresh token repository sin revocación.
- Smoke tenant HTTP añade ciclo desactivar→409 inicio y persistencia `Pending/Available/Inactive`; reactivar→iniciar; baja en ruta→409 y persistencia `InProgress/OnRoute/Active`; cancelar, desactivar, unassign y cleanup. El cleanup verifica IDs y fallbacks por nombre/placa.
- Local: pruebas enfocadas 3/3; suite Release **88/88**, restore success, build Release 0 warnings/0 errors, `bash -n scripts/smoke-tenant-http.sh` y diff-check correctos. Backend commit `190f284` pasó Actions `45045070288`: build, xUnit **88/88**, smoke HTTP/MySQL tenant/flota, concurrencia de rutas y cleanup completo.
- Errores de exploración/build docs: se intentó abrir `AppDbContext.cs` y `fletflow/Program.cs` por rutas supuestas; clase real está en `Context/FleetFlowDbContext.cs`, entrypoint fuera de esas rutas. `git diff --check` encontró línea vacía extra al final de CHANGELOG; EOF recortado y diff-check pasó. Quedó documentado en ERROR_LOG.
- Próximo: commit/push de estos docs; disparar revalidación Front integrada contra el backend nuevo `190f284` y registrar ambos runs. Front Action `45043898907` ya confirmó Driver tablet 26/26 y responsive 1/1 para SHA Front `0dd5099` contra backend anterior `a18a34f`.
- No cerrar misión: CRUD/responsive residual, Admin delegado sin policy, SMTP y provisionamiento PFX/key ring productivo externos; móvil nativo diferido.
