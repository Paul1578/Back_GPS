## Integridad de rutas heredadas — Backend CI aprobado

- La revisión encontró que `TenantFleetOwnership` dejaba `OwnerUserId=NULL` cuando Driver y Vehicle pertenecían a empresas diferentes; el filtro tenant ocultaba entonces la ruta.
- La migración ahora comprueba la discrepancia antes de actualizar rutas y aborta con un error explícito. Una migración adicional detecta rutas que ya quedaron sin propietario en entornos donde se aplicó la versión anterior, impidiendo que el API arranque y oculte el problema.
- También se corrigió `FleetMapper`: `PointsJson` inválido ya no degrada silenciosamente una ruta a origen/destino. El fallback se conserva sólo para el arreglo vacío heredado `[]`.
- Prueba focal FleetMapper: **4/4**; suite Release **155/155**, build **0 warnings/0 errores**. API arrancó contra MySQL 8 efímero desde DB vacía y aplicó todas las migraciones, incluidas ambas guardas. Una segunda verificación MySQL produjo SQLSTATE 45000 para owner discordante y confirmó la ruta y ambos propietarios intactos.
- Action Backend [`45185698388`](https://github.com/Paul1578/Back_GPS/actions/runs/45185698388) para `492f8f9` terminó success: build/test Release, smoke tenant HTTP/MySQL, concurrencia, rate limit y cleanup. La ejecución automática duplicada [`45185687229`](https://github.com/Paul1578/Back_GPS/actions/runs/45185687229) también terminó success.
- Front Action [`45185961950`](https://github.com/Paul1578/Front_gpsR/actions/runs/45185961950) validó `dff947f`: **53/53 E2E**, responsive, build/lint, Backend Release/xUnit, API/MySQL y cleanup.

## Tamaño total de evidencias — cobertura local aprobada

- Cada foto ya tenía máximo de 4 MB, pero una ruta podía acumular fotos/notas ilimitadas. `RouteE.AddPointEvidence` ahora limita a **50 evidencias y 8 MB de contenido total**, rechazando sin mutación.
- Pruebas `RoutePointTests`: **20/20**; suite Release **157/157**; build Release **0 warnings/0 errores**. Actions Backend [`45187302298`](https://github.com/Paul1578/Back_GPS/actions/runs/45187302298) y [`45187289527`](https://github.com/Paul1578/Back_GPS/actions/runs/45187289527) para `a07838d` terminaron success con Release, HTTP/MySQL, rate limit y cleanup. La verificación del HEAD documental `658858b` también pasó en [`45187657647`](https://github.com/Paul1578/Back_GPS/actions/runs/45187657647).

## Paginación de equipo y conductores — Action aprobada

- Se añadieron `GET /api/Users/my-team/page` y `GET /api/Drivers/page`, con `{ items, page, pageSize, totalCount, totalPages }`; los endpoints anteriores conservan arrays. Página por defecto 25, límite 100, búsqueda/rol/estado, orden determinista y offsets seguros ante overflow. Se mantienen filtro explícito de OwnerUserId en usuarios y filtro global tenant en Drivers.
- Backend Release local: **151/151 xUnit**, incluidos 7 tests nuevos de filtros, orden/páginas vacías, límite extremo, compatibilidad/ACL y aislamiento. `git diff --check` y `bash -n scripts/smoke-tenant-http.sh` pasan.
- El smoke HTTP/MySQL verifica conteo/tamaño, páginas 1/2, búsqueda del Driver inactivo por documento actualizado y búsquedas paginadas de Users/Drivers aisladas entre tenants. Action [`45178172092`](https://github.com/Paul1578/Back_GPS/actions/runs/45178172092) en `b98f8f9` pasó build Release, **151/151 xUnit**, smoke HTTP/MySQL tenant, concurrencia, login rate limit y cleanup completo.
- La desvinculación User–Driver quedó validada en Action [`45174526072`](https://github.com/Paul1578/Back_GPS/actions/runs/45174526072) (`08e14fa`): build, xUnit 144/144, API/MySQL incl. HTTP 409 con ruta Pending, concurrencia, rate limit y cleanup.

## Desvinculación User–Driver — smoke extendido en CI pendiente

- La Action Backend `45173035947` sobre `fd6ad1e` pasó build Release, **144/144 xUnit**, aislamiento HTTP/MySQL, concurrencia, login/rate limit y limpieza. Corrigió el fallo anterior de login del fixture (`45172421318`) estableciendo una activación real por token, sin marcar flags directamente.
- El smoke local en curso añade la regresión que faltaba: un `PUT /Drivers/{id}/unlink-user` responde HTTP 409 cuando hay ruta Pending; SQL confirma vínculo, TokenVersion, ruta y vehículo intactos. Después cancela ruta, libera vehículo y valida el unlink exitoso, revocación de sesiones, rol Driver y limpieza de cuatro fixtures.
- Pendiente ejecutar `bash -n`, diff-check, publicar el cambio y obtener Action verde que incluya la nueva cobertura. No se declara cubierto el HTTP 409 hasta esa ejecución.

# Historial de estabilización — 2026-11-23

## Desvincular cuenta del perfil Driver — implementación local en curso

- Decisión de usuario: se conserva el rol Driver, la cuenta y el perfil. La acción se rechaza si hay una ruta Pending/InProgress.
- Nuevo `PUT /api/Drivers/{id}/unlink-user`: bloquea Driver/Vehicle/cuenta en el orden de asignación, elimina sólo la relación, incrementa TokenVersion y revoca refresh tokens dentro de la transacción. Cinco xUnit focales pasan, incluido bloqueo de ambas clases de rutas, idempotencia, preservación de perfil y persistencia de `UserId=null` con el repositorio EF real.
- Suite completa Release: **144/144**; build ejecutado por `dotnet test`, sin warnings/errors observados. Smoke HTTP/MySQL ampliado para comprobar relación retirada, registros/rol conservados, TokenVersion incrementada y rechazo de tokens anteriores. Action `45172421318` encontró fixture de login aún inactivo con `MustChangePassword=true`; smoke corregido al cumplir precondición y repetición pendiente.

## Brechas smoke backend encontradas por auditoría independiente — en curso

- La edición concurrente ya daba un 200 y un 409, pero no comprobaba por API/MySQL el destino final de cada ruta ni `Driver.VehicleId`. Se añadieron asserts de lectura API+SQL: una sola ruta toma la nueva pareja; la perdedora y asignaciones previas quedan intactas.
- Baja lógica ya impedía borrar una ruta en progreso y permitía borrarla cancelada, pero no verificaba el `isActive=false` final. Ahora el smoke lee por GET, SQL y lista `onlyActive=true`.
- `.github/workflows/ci.yml` ya contiene smokes tenant, concurrencia y login con MySQL/API descartables; se amplió el disparador para que el push a `chore/stabilize-fleet-platform` también los ejecute automáticamente (antes sólo push a `main` o dispatch manual).
- Contrato observado en la edición: se asigna el Vehicle nuevo al Driver elegido; la relación explícita del Driver anterior se conserva. Está documentado y el smoke lo verifica.
- `bash -n` y `git diff --check` pasan. Falta la Action de Backend con los nuevos smokes para confirmar persistencia y cleanup.

## Snapshot histórico previo a la auditoría de rutas heredadas

- Front más reciente: Actions [`45138088658`](https://github.com/Paul1578/Front_gpsR/actions/runs/45138088658) (cambio de rol persistido por UI) y [`45139564823`](https://github.com/Paul1578/Front_gpsR/actions/runs/45139564823) (username largo responsive) pasaron **51/51 E2E**; API/MySQL, responsive, Release/xUnit y cleanup correctos.
- xUnit agrega ciclo de dominio con pickup, delivery y PickupAndDelivery: cierre rechazado mientras quede una parada pendiente y exitoso cuando las tres tienen evidencia. Action [`45141029124`](https://github.com/Paul1578/Back_GPS/actions/runs/45141029124) pasó build, **137/137 xUnit**, tenant HTTP/MySQL, concurrencia y login rate limit; cleanup correcto.
- QA detectó una carrera sin cobertura entre inicio de ruta y cambio de vehículo a mantenimiento. xUnit SQLite comprueba rechazo sin mutaciones; smoke HTTP/MySQL concurrente acepta sólo estados coherentes. Suite Release **138/138**, build limpio, test enfocado **1/1**, `bash -n` y diff-check pasan. Action [`45142742926`](https://github.com/Paul1578/Back_GPS/actions/runs/45142742926) en `7eab085` pasó smoke tenant HTTP/MySQL, concurrencia incluyendo Maintenance y login rate limit; cleanup correcto. Actions `45142003159`/`45142398227` detectaron antes límites de longitud del fixture, corregidos a `MRV`/`MRD` y documentados.
- QA confirmó que `CreateRoute`/`UpdateRoute` no tenían pruebas de las fronteras de horario. xUnit SQLite acepta intervalos contiguos, rechaza solapamientos y horario incompleto frente a una ruta activa; edición mantiene nombre/fechas previos ante conflicto. Focal **1/1**, suite **139/139**, Release build sin warnings/errores, bash syntax y diff-check pasan localmente. Backend Action [`45145214415`](https://github.com/Paul1578/Back_GPS/actions/runs/45145214415) pasó en `ca1a278`: **139/139**, Release, tenant HTTP/MySQL, concurrencia, login rate limit/proxy y cleanup.

- Revisión de seguridad independiente read-only sobre Backend `391792e` y Front `6555858`: no encontró un nuevo bypass tenant/roles/JWT ni XSS explotable dentro del alcance revisado. Confirmó filtros globales, root-only y `TokenVersion` en `OnTokenValidated`; las comprobaciones dinámicas permanecen en las Actions HTTP/MySQL citadas.

- Front pasó Action [`45131410815`](https://github.com/Paul1578/Front_gpsR/actions/runs/45131410815) (**51/51 E2E**, incluye ciclo Driver multi-parada) y las Actions [`45133416432`](https://github.com/Paul1578/Front_gpsR/actions/runs/45133416432) / [`45133412567`](https://github.com/Paul1578/Front_gpsR/actions/runs/45133412567) (**51/51** cada una, incluye edición UI de ruta Manager). Responsive, backend Release/xUnit, audit/lint/build y cleanup pasaron.

- HEAD vigente: Backend `chore/stabilize-fleet-platform` `ca1a278`, Action [`45145214415`](https://github.com/Paul1578/Back_GPS/actions/runs/45145214415) pasó Release, xUnit **139/139**, tenant HTTP/MySQL, concurrencia, login rate limit/proxy y cleanup. Front `refactor/route-builder` docs `58306ab`, Action [`45145157559`](https://github.com/Paul1578/Front_gpsR/actions/runs/45145157559) pasó **51/51 E2E**, responsive 1/1, audit/lint/build, Backend Release/xUnit y cleanup. Ninguna rama modifica `main`.
- Front Action [`45124271067`](https://github.com/Paul1578/Front_gpsR/actions/runs/45124271067) pasó el SHA `1d7b922`: Release/xUnit backend, audit, lint/build, responsive **1/1**, Playwright API/MySQL **51/51** y teardown correcto. Backend Action [`45119396803`](https://github.com/Paul1578/Back_GPS/actions/runs/45119396803) pasó `341161d`.
- Las rutas admiten sólo traslado o múltiples paradas de recogida, entrega o ambas. Ciclo operativo HTTP/MySQL con evidencia, GPS, finalización y vehículo disponible validado en Action `45108549336`; ciclo de traslado validado en `45119396803`.
- Confirmación vigente: smoke `smoke-tenant-http.sh:1140-1229` recorre una ruta con tres paradas (pickup, delivery y ambas), evidencia por parada, cierre y persistencia en MySQL. Backend `c42a306`, Action [`45132350399`](https://github.com/Paul1578/Back_GPS/actions/runs/45132350399): xUnit **136/136**, Release, smoke HTTP/MySQL y cleanup. El pendiente antiguo de ciclo multi-parada quedó resuelto; xUnit de dominio sólo modela casos menores con una parada.
- La experiencia Driver multi-parada tampoco está pendiente: Front `e2e/fleet-admin.spec.ts` prueba en sesión real dos paradas operativas, evidencia requerida (nota/foto), progreso secuencial, cierre, persistencia e Historial; Action [`45131410815`](https://github.com/Paul1578/Front_gpsR/actions/runs/45131410815) pasó **51/51 E2E** con backend/API/MySQL y cleanup.
- Seguridad vigente: claves nuevas mínimo 12 caracteres; login 10 intentos/minuto/IP; recuperación 5/10 min/IP; sin bloqueo por cuenta; secreto de reset 256 bits, TTL 60 min (rango 1–1440), URL HTTPS para reset/activación fuera de Development. Sólo la cuenta raíz puede crear/promover Admin.
- Smoke de login rate limit integrado: `scripts/smoke-login-rate-limit-http.sh` reinicia API contra la DB QA en `Staging` y prueba el límite por HTTP real: 10 respuestas 401 desde una IP, el intento 11 responde 429 + Retry-After, y el mismo usuario inicia sesión desde otra IP. MySQL disposable y prueba local pasaron; build Release 0 warnings/errores; xUnit **136/136**; `bash -n` y diff-check correctos. Action [`45126308692`](https://github.com/Paul1578/Back_GPS/actions/runs/45126308692) pasó en el HEAD de código `88f669d`: Release, xUnit 136/136, HTTP/MySQL tenant/concurrencia y login rate limit Staging; cleanup correcto.
- El middleware acepta IP reenviada sólo desde `ForwardedHeaders:KnownProxies` explícitas y por un salto. `KnownProxies` queda vacío por defecto. Smoke local y Action `45127502896` verificaron spoof ignorado y límite compartido entre dos proxies; faltan configurar las IP reales del ingress al desplegar.
- Pendientes productivos externos: SMTP con credenciales vigentes; contador rate limit distribuido; persistencia/rotación del key ring Data Protection; despliegue productivo no verificado. No se afirma envío de correo real.
- DB local preservada durante las pruebas recientes: el smoke de login usó únicamente MySQL 8 efímero y cleanup eliminó API, contenedor y certificados de prueba. La base local existente no se tocó.
- Misión global sigue abierta para continuar auditoría funcional, seguridad de despliegue y responsive. La app nativa móvil se pospuso por decisión del usuario.

## Historial de auditoría y fases anteriores

Las notas siguientes conservan evidencia histórica; pueden describir estados ya resueltos y no sustituyen el bloque vigente.

## Hallazgos de auditoría en curso — 2026-11-23

- No hay evidencia de defecto en aislamiento de tenant ni en la política root-only; el backend aprobó Action `45090274537` (build Release, xUnit 113/113, smoke HTTP/MySQL, concurrencia y cleanup).
- Historial supersedido: la prueba de ciclo multi-parada se añadió y CI validó posteriormente; consultar “Confirmación vigente” arriba y `docs/TESTING.md`.
- Decisión confirmada: se permiten rutas de traslado entre origen/destino sin recogida/entrega; el ciclo está cubierto por prueba de dominio.
- Seguridad confirmada: claves nuevas con mínimo 12 caracteres y login limitado a 10 intentos/minuto por IP, sin bloqueo de cuenta. Action [`45096399172`](https://github.com/Paul1578/Back_GPS/actions/runs/45096399172) pasó Release, xUnit 120/120, aislamiento tenant/concurrencia HTTP/MySQL y cleanup.
- Backend commit `33e2499` Action [`45096399172`](https://github.com/Paul1578/Back_GPS/actions/runs/45096399172) pasó Release, xUnit **120/120**, smoke HTTP/MySQL de aislamiento tenant y concurrencia, cleanup correcto.
- Data Protection persistente/compartido para reinicios y réplicas sigue pendiente de configuración y prueba de despliegue. Móvil nativo aplazado; SMTP real depende de credenciales.

- Corregida posible actualización perdida entre edición del perfil Driver y asignación Vehicle: el comando de edición bloquea/relee la fila antes de guardar. Release xUnit **113/113** local y Action `45084368819` success, incluida regresión interleaving y smoke HTTP/MySQL.
- Runner actualizado para exportar el UserId requerido por el nuevo E2E Manager mapa; `bash -n` y diff-check pasan, Action Backend `45084501616` success. La Action Front debe confirmar que el escenario no figure skipped.
- El E2E Manager mapa falló en Actions por uso del hamburger desktop y estado de rol residual; runner ahora crea Manager/Driver exclusivos con cleanup SQL. `bash -n` y diff-check pasan local; Front debe validar el flujo nuevo.
- Backend commit `1ce4d45` pasó Action `45085910280`: build Release, xUnit, aislamiento tenant por HTTP/MySQL y concurrencia de asignación por MySQL; cleanup y contenedores completados.
- Runner `2619531` entrega ahora un sufijo estable al E2E Manager para que Playwright limpie los mismos fixtures después de un reinicio de worker; `bash -n` y diff-check pasan, Action de integración posterior pendiente.
- El E2E anterior Front `45085920993` completó **39/39** y responsive **1/1**, pero aún usó el sufijo UUID. Runner actualizado en `2619531` pasó Action Backend `45086298118`; Front `104723a` repetirá el caso con sufijo compartido.
- Auditoría retiró `RoutesPRUEBAController`, que devolvía texto estático sin consumidores; Action `45086663424` validó HTTP 404 con token válido, build/xUnit, aislamiento tenant, concurrencia y cleanup.
- Política producto definida y validada: sólo cuenta raíz otorga Admin. Action `45088129188` probó creación/promoción permitida, tres denegaciones a delegado y no mutación SQL.
- El smoke de perfil User pasó HTTP/MySQL: actualización, GET y sincronización persistida con Driver enlazado. Resultado en Action `45088129188`.
- Front Actions `45082924174` (un worker) y `45083215716` detectaron la escritura perdida, y dos aserciones UI/E2E ya corregidas en rama Front. Ver `ERROR_LOG.md` en ambos repos.
- Ramas: `chore/stabilize-fleet-platform` y Front `refactor/route-builder`; main intacta. Misión abierta por cobertura CRUD/roles/responsive y SMTP externo; móvil nativo aplazado.

## Evidencia obligatoria para estados terminales — validado localmente y en CI

- Se corrigió API que permitía Completed/Skipped sin evidencia y dejaba la operación atascada. El dominio exige evidencia y DriverView deshabilita Omitir sin evidencia.
- Release xUnit 110/110, smoke HTTP/MySQL descartable y cleanup cero; 400 sin mutaciones para cierre de parada/ruta, y camino válido completo.
- `bash -n`, diff-check pasan. Backend Action `45070491409` sobre `f564aa3` pasó build, xUnit 110/110, smoke HTTP/MySQL y concurrencia; cleanup correcto.

## Vínculo manual User/Driver y CRUD negativo — local aprobado

- Smoke crea la cuenta User, la convierte al rol Driver y vincula después su perfil por `userId`; respuesta Driver, equipo y SQL verifican asociación única y tenant.
- CRUD inválido Vehicle/Driver responde 400 sin mutaciones. Smoke MySQL 8/API efímeros completo exit 0, cleanup confirma cero fixture manual.
- Action `45069402482` pasó Release/xUnit **107/107**, aislamiento HTTP/MySQL, concurrencia y cleanup del flujo manual `0:0`.
- No existe endpoint HTTP para desvincular User/Driver preservando ambos. Política de Admin delegado y necesidad de esa desvinculación están consultadas al usuario; no alterar hasta respuesta.


- Action `2045b18` [45050058627](https://github.com/Paul1578/Back_GPS/actions/runs/45050058627) pasó: Release build, xUnit **97/97**, smoke HTTP/MySQL tenant y concurrencia, incluye carrera de role/refresh y bloqueos con ruta Pending; cleanup sin fixtures.
# Estado actual — 2026-11-22

- Actualización vigente: `76c2cc8` pasó Action `45077658712`: build Release, xUnit **111/111**, smoke HTTP/MySQL tenant, concurrencia y cleanup. Incluye denegaciones Driver en APIs de flota y rutas; los POST rechazados dejaron 0 filas en MySQL.
- Backend `229076d` pasó Action `45078321298`: xUnit 111/111, Release build, smoke tenant HTTP/MySQL, concurrencia y cleanup; incluye 403 de User en Users/Roles y ninguna mutación.
- Cambio local en smoke cubre camino permitido Driver sobre su propia ruta (lectura, inicio, GPS, cancelación) y confirma `Cancelled`, vehículo `Available`, una posición y asignación persistida. La primera repetición expuso una expectativa obsoleta de lista vacía; corregida a aislamiento por Driver. Repetición MySQL 8 efímero exit 0, Action posterior pendiente.
- El smoke amplía también 403 de Manager sobre Users/Roles y de Logistics sobre PUT/DELETE/reactivar Vehicle y editar/desactivar/asignar Driver; snapshots SQL antes/después permanecen idénticos. Primera repetición falló en un helper que comparaba una ruta Manager ya editada/desactivada; se acotó al Vehicle/Driver relevante. Action `45080063668` para `fa16d05` pasó Release build, xUnit 111/111, smoke HTTP/MySQL, concurrencia y cleanup.
- Se añadió prueba unitaria positiva de asignación a Driver sin UserId; Release xUnit completo pasa **112/112**, Action actualizada pendiente.
- Front `refactor/route-builder` HEAD `2147be7`: Action `45076629351` pasó **37/37 E2E**, responsive **1/1**, audit/lint/build y cleanup.
- Misión sigue abierta; continúan matrices CRUD/roles/responsive. Admin delegado y desvinculación User↔Driver esperan decisión; SMTP/infraestructura productiva son externos y móvil nativo está aplazado.

- Backend `chore/stabilize-fleet-platform`: se corrigió la baja de cuenta vinculada a Driver durante rutas Pending/InProgress. Local: xUnit 111/111, Release build 0 warnings/0 errors, smoke HTTP/MySQL descartable completo y cleanup cero. Action `45071475764` sobre `87b208e` pasó build, xUnit 111/111, smokes y cleanup.
- Backend Action `45070491409` sobre `f564aa3` pasó (build, xUnit 110/110, tenant HTTP/MySQL, concurrencia y cleanup); docs publicadas en `777b905`.
- Front `refactor/route-builder`: Action `45070512838` pasó **36/36 E2E**, responsive **1/1**, backend **110/110**, audit, lint/build y cleanup sobre `96f7a21`. Commit `5d4e290` pasó Action `45071113746`: 36/36 E2E, responsive 1/1, backend 110/110 y cleanup.
- Backend docs commit `6a0cd6d` pasó Action `45072150441`. Front `340eb55` pasó Action `45072015727` (36/36 E2E, responsive 1/1, backend 111/111 y cleanup); Front `7f4841c` agrega manejo de errores de carga y su Action `45072706025` está ejecutando.
- Las políticas de Admin delegado y desvinculación User/Driver siguen sin decisión. App nativa está aplazada; SMTP/PFX e infraestructura productiva son externos. Misión abierta.

## Validación HTTP negativa Vehicle/Driver — local y CI aprobadas

- El smoke HTTP ahora verifica que ediciones inválidas de Vehicle y Driver no persistan parcialmente y que altas Driver inválidas no creen filas.
- MySQL 8/API efímeros pasaron; cleanup sin fixtures. `bash -n` y diff-check correctos.
- Action `45070491409` pasó el smoke HTTP/MySQL y xUnit 110/110 en el HEAD actual. Continuar por cobertura de CRUD/roles.

## Validación integrada de registro neutral — 2026-11-22

- Backend `52d7e46` pasó Action `45066082294`: Release build, xUnit **107/107**, smoke HTTP/MySQL, comparación del payload de registro nuevo/duplicado, espera mínima y cleanup.
- Front `57e44c6` pasó Action `45066249833` contra ese código backend: audit/lint/build, responsive 1/1, Playwright API/MySQL 33/33, cleanup.
- Backend documentación `029011e` pasó Action `45066471076`, incluyendo build/xUnit y smokes tenant/concurrencia.

## Cobertura de ciclo Vehicle en CI — local validado, Action pendiente

- El smoke tenant añade editar Vehicle con persistencia SQL/API, cambiar estado a Maintenance sin alterar `IsActive`, baja lógica y reactivación preservando `Status`.
- Release no se modificó. `bash -n`, `git diff --check` y smoke completo contra MySQL 8 efímero pasaron; cleanup eliminó las filas QA.
- El host global `dotnet` falla al iniciar en el entorno; el SDK local `/home/leonardo/.dotnet/dotnet` resolvió el smoke. No se utilizó MySQL persistente.
- Siguiente: publicar el smoke y docs, verificar Action; continuar aislamiento de vínculos manuales User/Driver y demás casos CRUD HTTP. Misión abierta.

- Rama `chore/stabilize-fleet-platform`; `main` intacta. Backend `22c2fb6` Action [`45058536309`](https://github.com/Paul1578/Back_GPS/actions/runs/45058536309) pasó build/xUnit, aislamiento tenant HTTP/MySQL, concurrencia y cleanup.
- Logout limita revocación al propietario autenticado del refresh. Validación local: Release build 0 warnings/errores, xUnit **99/99**, `smoke-tenant-http.sh` y `smoke-route-concurrency.sh` exit 0 con limpieza en MySQL 8 efímero.
- Front local relacionado pasó **33/33** E2E, lint/build/audit 0 vulnerabilidades y cleanup; incluye creación de ruta de Manager.
- Misión abierta: ampliar CRUD/roles/responsive. La política Admin delegado no está definida; SMTP/PFX/infraestructura productiva son externas. App nativa móvil aplazada.

# Estado histórico — 2026-11-22

- Rama `chore/stabilize-fleet-platform`; `main` intacta. Commit `27bf75a` pasó Action `45048795456`: build Release, xUnit **94/94**, smokes HTTP/MySQL de tenant/concurrencia y cleanup cero.
- Cambios locales pendientes: serializar cambio de rol con refresh; bloquear desasignación/baja/cambio de rol cuando Driver tiene ruta activa Pending; unit tests **97/97**, build Release **0 warnings/0 errors**, bash syntax/diff-check correctos. Action de smokes con estas adiciones pendiente.
- Persisten cobertura pendiente CRUD/roles/responsive y pregunta de producto sobre Admin delegado. SMTP/PFX/infra producción externos; app nativa aplazada.

# Estado actual — 2026-11-22

- Rama `chore/stabilize-fleet-platform`; `main` no se modifica.
- Último código publicado: `5c08dbc` pasó Action backend `45047639950` (Release xUnit 92/92 + smoke HTTP/MySQL y concurrencia/cleanup). Docs actuales `fcd7111` pasaron `45047712270`.
- Cambio local pendiente de publicar: bloqueo de reasignación Driver con ruta Pending/InProgress y validación Driver/Vehicle al iniciar. Release xUnit **94/94**, `bash -n` y diff-check correctos; smoke HTTP/MySQL de concurrencia y Action sobre el SHA nuevo pendientes.
- La protección de Outbox contra envío de reset ya inválido está implementada y cubierta por suite/CI previo.
- Misión abierta: continúan CRUD/roles/responsive y E2E; esperar decisión de política de Admin delegado. SMTP/PFX e infraestructura productiva son dependencias externas; app móvil nativa aplazada.

# Estado actual — auditoría y estabilización integral — 2026-11-22

- Backend branch `chore/stabilize-fleet-platform`; Front branch `refactor/route-builder`; `main` intacta.
- Backend `a207c08` pasó Actions `45047226328`: build Release, xUnit **91/91**, HTTP/MySQL tenant/flota, smoke de asignación concurrente y cleanup correcto. El HTTP smoke cubrió vehículo en Maintenance al cancelar ruta Pending con fixture independiente.
- Front `d05dcf8` pasó Actions `45046312974`: E2E API/MySQL **26/26**, responsive **1/1**, backend build/test, lint/audit y cleanup. Front docs `38c58d6` dispara integración contra backend a207; Action `45047502825` en progreso.
- Lote local backend sin commit: Worker de correo suprime token reset usado/expirado; tests SQLite worker **3/3**, suite Release **92/92**, build **0 warnings/0 errors**. Nueva Action backend y Front integrado tras este commit siguen pendientes.
- Criterios abiertos: más CRUD/roles y pruebas por viewport, política de creación/promoción Admin delegado (esperando decisión); SMTP/PFX e infraestructura productiva externas. App móvil nativa aplazada por el usuario.

# Estado actual de estabilización — 2026-11-22

- Rama `chore/stabilize-fleet-platform`; no se modifica `main`.
- Backend `190f284` pasó Actions `45045070288` (build/xUnit Release **88/88**, smoke tenant/flota, concurrencia y cleanup); documentación `e24e371` pasó `45045329470`.
- Lote local sin commit: corregida transición que liberaba Vehicle en Maintenance al cancelar Route Pending. SQLite test focal 1/1 y Release xUnit **89/89** pasan con SDK 8.0.428 aislado en `/tmp/fletflow-dotnet`; `bash -n`/diff-check pasan. La nueva verificación HTTP/MySQL está agregada al smoke; Actions posterior aún pendiente.
- Auditoría actual detectó asuntos de concurrencia en reset/refresh y GPS bajo reasignación; validar y corregir en próximos cambios. Cierre de Admin delegado requiere decisión de política; correo SMTP/PFX productivo e infraestructura externos. Mobile nativo aplazado.

# Estado de esta iteración — CRUD Driver para Manager — 2026-11-22

- En `chore/stabilize-fleet-platform` (base `4718f04`) se amplió sólo el smoke HTTP/MySQL y documentación. Sin cambios a código de producto ni a `main`.
- Driver de Manager verificado por HTTP/API/SQL en edición, baja/reactivación, asignación y desasignación de vehículo. Tras cada mutación se validan estado persistido y snapshot sin cambios de Driver/Vehicle/Route Manager preexistentes.
- Release build: 0 warnings/0 errores. xUnit **82/82**, `bash -n`, `git diff --check` y smoke completo con API/MySQL 8 desechables pasan; cleanup confirmó cero fixtures CRUD Driver/Vehicle. Actions pendiente para este diff; sin commit/push, pendiente revisión.

# Estado de esta iteración — cobertura tenant HTTP/MySQL — 2026-11-22

- Rama `chore/stabilize-fleet-platform`. Smoke amplía aislamiento de `GET /Users` y `/Users/my-team`, opciones de asignación y operaciones cross-tenant sobre Drivers/Vehicles.
- Verificado localmente: build Release limpio, xUnit **82/82**, sintaxis Bash/diff-check limpios y `smoke-tenant-http.sh` exit 0 con API y MySQL 8 desechables. Tras cada uno de cuatro intentos cross-tenant, SQL confirmó `VehicleId`, `IsActive` y ownership sin cambios; cleanup de fixtures A/B y demás recursos confirmó cero filas.
- Alcance pendiente: Actions/CI sobre este diff y revisión/integración. La misión global de FletFlow sigue abierta y SMTP real/app móvil están fuera de esta entrega.

## Iteración actual — exclusividad de asignación en rutas — 2026-11-22

- CreateRoute/UpdateRoute validan relación Vehicle/Driver bajo locks y devuelven 409 si el vehículo sigue asignado a otro conductor.
- Dos regresiones SQLite y suite Release completa pasaron: **82/82**. `bash -n`/diff-check pasan.
- Smoke HTTP/MySQL de creación/edición conflictivas y persistencia intacta pasó en MySQL descartable. Actions `45032172895`: Release build, xUnit **82/82**, smoke tenant y concurrencia completos; cleanup correcto.
- Primera ejecución `45031869323` había fallado por dependencia entre fixtures; `8e76f80` aisló los recursos por escenario y la repetición pasó.

## Contrato de filtros de GPS — Actions validado — 2026-11-22

- El smoke tenant fija `RecordedAt` conocido y consulta rango inclusivo con `from=to`, límite solo `from`, límite solo `to` e intervalo invertido. Incluye además rango sin resultados; las respuestas esperadas son HTTP 200 y colección vacía cuando el dato queda fuera del filtro o el intervalo está invertido.
- `bash -n`, Release xUnit **64/64** y el smoke completo en MySQL 8 efímero pasan; cleanup dejó cero recursos QA. Actions `45009987648` terminó success para `729f326`: build Release, xUnit 64/64, smoke tenant HTTP/MySQL con filtros GPS y concurrencia; cleanup completo.

## Cross-tenant de paradas y CRUD HTTP de Driver — validado local y en Actions — 2026-11-22

- El smoke tenant ahora intenta cambiar estado y agregar evidencia a una parada de tenant A usando token de tenant B; ambas respuestas deben ser 404 y una lectura posterior como A exige estado original/evidencia vacía.
- El smoke cubre el ciclo HTTP positivo de Driver: crear/leer/editar, baja y reactivación con `onlyActive=false`, asignar y desasignar vehículo; cada cambio se confirma por API.
- MySQL 8 efímero local pasó `smoke-tenant-http.sh`; limpieza comprobó cero rutas, posiciones y vehículos QA; el contenedor se eliminó. Release xUnit 64/64, shell syntax y diff-check pasan. Actions `45009348293` terminó success para `c6f74f8`: Release build, xUnit 64/64, smoke tenant HTTP/MySQL y concurrencia; cleanup completo.
- Se mantiene abierta la política de promoción de miembros a Admin: requiere decisión de producto antes de fijar el permiso esperado.

## Actualización — edición de perfil de usuarios — 2026-11-22

Backend implementa persistencia de nombres, endpoint Admin para edición de nombre/apellido/username, migración con backfill desde Drivers y sincronización bilateral User/Driver bajo tenant. Email sigue inmutable. Release build 0 warnings/errores; xUnit 62/62; smoke HTTP/MySQL de tenant y concurrencia pasan en MySQL efímero. Playwright integrado pasó 18/18 en Chrome sobre API/MySQL efímero y cleanup del runner. Actions backend 45005400763 success en el commit e54d3c8 (build, xUnit, smoke MySQL tenant/concurrencia). Auditoría integral abierta.

## Autorización de Driver cerrada por ejecución — 2026-11-22

- Backend `41c01e7` bloquea detalle y GPS a perfiles Driver inactivos; Release/xUnit 61/61 y Actions backend `45002562048` success.
- Front Actions `45002920723` probó el flujo completo en API/MySQL 18/18: Driver inactivo, ruta ajena, mutaciones de ruta/parada/evidencia/GPS denegadas y persistencia intacta. Cleanup completado.

## Actualización — autorización Driver/GPS — 2026-11-22

- Backend `2fdc9e0` exige Driver activo/asignado en GET/POST GPS; Release 61/61 y Actions `45002238853` success con smoke HTTP/MySQL.
- E2E Front de aislamiento por Driver e inactividad ya está agregado; Actions browser/API/MySQL `45002396041` pendiente.

## Actualización — autorización GPS Driver — 2026-11-22

- Se corrigió la autorización de detalle y lectura/escritura GPS para requerir que el Driver autenticado siga activo y asignado a la ruta, alineándola con el helper central. Pruebas automatizadas integradas para perfil inactivo y otro Driver están en preparación; CI pendiente.

## Actualización — 2026-11-22

- Alta de usuarios Driver concurrente probada de punta a punta: un ganador HTTP 200, perdedor 409 y persistencia única Users/Driver/PasswordResetToken. Actions `45001599626` success, xUnit 61/61, smoke HTTP/MySQL y concurrencia de asignación incluidos.
- Frontend amplió CRUD de vehículo a 320 y 768 px; lint/build/discovery 18 pasan localmente. E2E autenticado de Actions `45001695962` todavía en curso.

# Estado del proyecto

## Edición de perfil — defensa de tenant — 2026-11-22

- `UpdateUserProfileCommand` ya no depende exclusivamente del chequeo del controller: requiere propietario autenticado y filtra explícitamente User/Driver asociado por tenant.
- Tests Release local y Actions **64/64**; smoke shell syntax y diff-check correctos. Actions `45007491905` confirmó tenant HTTP/MySQL, límites de FirstName/LastName, concurrencia y cleanup.
- La revisión de seguridad no halló IDOR HTTP en el endpoint. Sigue pendiente definir privilegio de Admin delegado para asignar rol Admin.

Fecha de corte: 2026-11-22.

Actualización de estabilidad: duplicados de placa en creación/edición ahora se reportan como conflicto de negocio HTTP 409 con mensaje en español. `dotnet test` Release pasó 59/59 con el SDK local 8.0.428; CI integrado pendiente.

Nota de actualización: algunos resultados históricos de registro público más abajo describen el contrato anterior (rol User). Desde el cambio de onboarding del 2026-11-22, la expectativa vigente es una cuenta raíz Admin confinada a su tenant; Actions `44999697740` confirmó build, xUnit, smoke HTTP/MySQL de tenant y smoke de concurrencia en `3620cc2`.

## Onboarding público de empresa — validado — 2026-11-22

- La pantalla pública prometía registrar a la empresa como Gerente, pero AuthController forzaba `User`; una cuenta recién registrada no podía gestionar su flota.
- Corrección local: el servidor asigna siempre rol Admin como raíz de un tenant nuevo, sin aceptar `roleName` del request. El token usa su propio ID como `owner_id`, conservando filtros de aislamiento de datos.
- xUnit Release local y Actions 61/61; smoke HTTP/tenant y concurrencia MySQL Actions success. El test/runner ya no eleva manualmente el rol del segundo tenant para esconder el resultado del registro. Actions Front cross-branch `44999716080` confirmó alta móvil y consumidores API con Backend `3620cc2`: responsive 1/1, Playwright 18/18 y cleanup MySQL success.

## Seguridad de usuarios cross-tenant — validada

- Auditoría independiente confirmó que las rutas de usuario protegen membresía y cuenta propietaria, pero el smoke no las ejercitaba mediante HTTP/MySQL.
- `smoke-tenant-http.sh` ahora comprueba que tenant B no cambie estado/rol del User de A (incluye alias legado), y que Admin A no desactive ni cambie el rol de su propia cuenta raíz; SQL sólo verifica que estado/rol persistieron sin cambios.
- Actions `45000893415` pasó en `a5dbd4d`: Release build, xUnit 61/61, smoke HTTP/MySQL de tenant/usuarios/propietario, concurrencia y cleanup de contenedores.
- Abiertos de auditoría para próxima iteración: atomicidad de alta Driver ante dos documentos concurrentes; autorización HTTP de Driver contra ruta asignada a otro Driver; frontera de Admin raíz versus Admin delegado pendiente de política de producto.

## Estado operativo

- Rama: `chore/stabilize-fleet-platform`.
- .NET SDK local: 8.0.428 en `/home/leonardo/.dotnet` (usar `DOTNET_ROOT=/home/leonardo/.dotnet`).
- `dotnet restore`: correcto.
- `dotnet build BackEnd_Gps.csproj --no-restore -m:1`: correcto, 0 errores y 0 advertencias.
- `dotnet test BackEnd_Gps.sln --configuration Release --no-restore`: 48 aprobadas, 0 fallidas, 0 omitidas.
- MySQL: activo en `127.0.0.1:3306`.
- Swagger: `http://127.0.0.1:5048/swagger/index.html` responde HTTP 200 al iniciar en `Development`; endpoints protegidos responden HTTP 401 sin token.
- SMTP: pendiente por credenciales no actualizadas.
- MySQL tiene 22 migraciones aplicadas; API inicia en `5048`.
- Middleware de rate limit probado: 60 requests/min por IP fuera de Development; loopback de Development queda exento para evitar bloquear QA local.
- Playwright Chrome autenticado 5/5 contra API/MySQL: login inválido, CRUD vehículo, edición/cancelación de ruta y consulta de Historial por flujo progresivo a 390×844 con `PickupAndDelivery`, permisos User 403 y CRUD de Driver desde Equipo, incluida transición Driver→Logistics→Driver con perfil sincronizado.
- Flujo HTTP de registro/login/perfil probado: registro con elevación de rol ignorado, perfil devuelve rol User, endpoints de ruta/herramienta admin retornan 403.
- Evidencia y estado por parada están implementados y persisten en `PointsJson`; E2E MySQL verificó recogida, entrega, notas, GPS y finalización.
- Pausar/reanudar se comprobó por HTTP/MySQL: el vehículo pasó correctamente entre `OnRoute` y `Available`.
- Creación de usuario Driver comprobada sobre MySQL; documento/teléfono correctos, no duplica Driver y devuelve `invitationSent=false` cuando SMTP no está configurado.
- Login Admin por contraseña de cuenta QA probado: `/Auth/me` confirma rol Admin, acceso a equipo autorizado y clave incorrecta devuelve 401. Se habilitó temporalmente la cuenta en SQL por falta de SMTP y se borraron cuenta/tokens.
- Dashboard y vistas de flota siguen conectadas a datos reales; no se añadió telemetría simulada.
- Navegador Chrome Admin: alta/edición de vehículo, alta de Driver, asignación y desasignación de vehículo verificados; SMTP fallido se informó al usuario sin fingir envío.
- Logistics recibe las políticas de rutas correspondientes al rol real; el seeder crea los cinco roles. Verificados Logistics 200/404 según recurso y User 403.
- Driver E2E Chrome completado: inicio, recogida/entrega, nota persistente, cierre; otro Driver no pudo leer ni cambiar la ruta. Móvil 390 px sin overflow y muestra resumen del vehículo asignado.
- Conductores permiten editar y dar de baja/reactivar lógicamente; la baja durante ruta activa se rechaza y el Driver inactivo pierde acceso a sus rutas. API/MySQL real verificó crear/leer/editar/baja/reactivación y cleanup en cero.
- Roles se actualizan ahora con una sola operación transaccional; se elimina el riesgo de dejar una cuenta sin rol al fallar la segunda llamada.
- Suite backend: 45/45 en Release; incluye creación y vínculo atómico de ruta, asignación de vehículo en mantenimiento rechazada y tiempos de ciclo de ruta.
- NuGet vulnerable audit: sin paquetes afectados tras fijar MySqlConnector 2.6.2; frontend `npm audit`: 0 vulnerabilidades.
- Build Release: 0 warnings/0 errors; API ejecutándose contra MySQL en local con `.env` ignorado.
- Rotadas las credenciales locales MySQL root y JWT; se revocaron todos los refresh tokens locales para cerrar sesiones anteriores. `.env` quedó con permisos 0600. Docker Compose y `.env.example` ya no contienen contraseñas utilizables.
- Compose recreó MySQL preservando volumen y datos; migraciones 22, cero filas QA, cero asignaciones de más de un rol y cero refresh tokens activos.
- API Development Swagger 200, endpoint protegido sin token 401; frontend `/dashboard` 200.
- Los hallazgos del pase independiente de seguridad se corrigieron: se eliminaron logs de tokens, se exige un rol por cuenta, CORS corre en todos los entornos, HTTPS/HSTS se exige fuera de Development, activación revoca sesiones, no hay fallback de rutas por ID de usuario y no se asignan vehículos en mantenimiento.
- El backend persiste `StartedAt`/`EndedAt` de rutas mediante la migración aditiva `RouteLifecycleAndSingleRole`.
- Frontend final: lint, build de producción y `npm audit --audit-level=low` correctos; 0 vulnerabilidades npm.
- Backend: auditoría NuGet sin paquetes vulnerables; `git diff --check` y `bash -n scripts/smoke-http.sh` correctos.
- E2E API/MySQL autenticado en esta pasada: registro no permite elevar rol, login Admin y perfil 200, alta/listado vehículo 201/200, otro tenant no ve el vehículo y recibe 404 al pedirlo, rol User recibe 403. Usuarios y vehículo QA limpiados.
- MySQL y phpMyAdmin quedaron limitados a loopback; contenedores recreados preservando volumen, base con 22 migraciones y API/dashboard siguen respondiendo.
- Chrome E2E adicional: Driver editado, dado de baja, mostrado con el filtro y reactivado con persistencia confirmada; User sin controles Equipo/Rutas y API 403; 390×844 sin overflow.
- Chrome Logistics E2E: cambio Manager→Logistics; Rutas visible/200, Equipo y Vehículos ocultos, Vehicles API 403.
- Chrome Vehicle E2E: crear, filtrar, editar/persistir y baja lógica; inactivo fuera del listado activo y conservado para historial.
- Repetición de cierre: suite Release 45/45; frontend hermano pasó lint/build/npm audit (0 vulnerabilidades); 22 migraciones aplicadas y conteos QA/refresh/roles duplicados en cero.
- Chrome E2E frontend: Playwright autenticado pasó login negativo y CRUD de vehículo Admin (alta/edición/baja lógica) a 390×844; cleanup borró Admin/vehículo y API volvió a Development sin bootstrap.
- Playwright cubre edición de ruta pendiente con Driver de equipo visible, operación `PickupAndDelivery` persistida y acceso User denegado en UI/API; suite autenticada 4/4. Runner local con cleanup acotado quedó en `scripts/run-e2e-local.sh`.
- Verificación final tras reinicio persistente de API: Swagger 200 y Routes sin token 401; DB 22 migraciones, cero filas QA y cero refresh tokens activos.
- Cleanup SQL posterior a Playwright 5/5: cero usuarios, vehículos, conductores y rutas de QA; cero refresh activos y 22 migraciones.
- Cierre adicional de esta sesión: `dotnet test BackEnd_Gps.sln --configuration Release --no-restore -m:1` pasó 45/45; frontend asociado pasó lint/build/npm audit (0 vulnerabilidades) y Playwright autenticado 4/4.
- Consulta MySQL repetida usando el esquema real `RefreshTokens.RevokedAt`: cero cuentas/vehículos/conductores/rutas E2EQA, cero refresh activos, 22 migraciones.

## Pendientes prioritarios

1. Seguir ampliando la matriz HTTP/MySQL de roles y permisos. Actions `44989454133` pasó una matriz parcial User/Driver/Logistics/Manager (15/15 Playwright, xUnit 57/57), pero no cubre todas las operaciones permitidas/denegadas.
2. Configurar SMTP vigente; la credencial depende del usuario/entorno. Rotar también credenciales equivalentes en cualquier entorno remoto.
3. Antes de producción, configurar dominio/proxy/certificado, almacenamiento de evidencia y rate limiting distribuido; el límite en memoria actual es por IP. Migrar tokens de `localStorage` a cookies HttpOnly requiere cambio coordinado backend/frontend.
4. E2E frontend/API/MySQL ya corre en Actions y pasó; ampliar casos de permisos/roles y responsive que aún no estén cubiertos.

## Actualización de cobertura — 2026-11-21

- Suite xUnit Release: 48/48 aprobadas, 0 omitidas.
- Aislamiento EF InMemory ahora incluye Vehicles, Drivers, Routes y RoutePositions entre dos propietarios; nueva regresión valida roles declarados por Vehicles, Drivers y Users controllers.
- Frontend E2E hermano pasó 7/7 desde UI/API/MySQL, incluyendo alta y baja de ruta desde el constructor móvil.
- La auditoría encontró que falta automatizar en HTTP/MySQL la matriz de roles y escrituras cross-tenant. SMTP, object storage y despliegue siguen como pendientes externos/de producción.

## Verificación HTTP/MySQL cross-tenant — 2026-11-21

- Suite E2E de frontend hermano crea dos tenants Admin locales y prueba IDOR real en Vehicles, Drivers y Routes, incluidos listados, lecturas, ediciones, cambios de estado, bajas y altas de ruta con IDs ajenos.
- Tenant A crea ruta, la inicia y persiste GPS; A ve una posición y B obtiene `200 []`; B no puede registrar una posición (404).
- Suite autenticada final Chrome/API/MySQL: 8/8; sin datos QA ni refresh tokens activos al terminar.
- No se modificó el endpoint GET de posiciones: su colección filtrada con cero filas es `200 []`, contrato seguro y diferenciado de un recurso Route individual (404).

## Cierre verificado de esta continuación — 2026-11-21

- Release: `dotnet test` 48/48, `dotnet build` 0 warnings/0 errors; NuGet sin paquetes vulnerables.
- E2E externo desde el frontend hermano: Chrome/API/MySQL 8/8, con tenant cruzado en Vehicles, Drivers, Routes y RoutePositions; cleanup en cero.
- Swagger 200, API Routes sin JWT 401; MySQL con 22 migraciones, cero fixtures QA y cero refresh tokens activos.
- Queda pendiente integrar la matriz HTTP/E2E multi-repositorio en CI y cubrir más acciones responsive; no marcar misión completa.

## Integración API/MySQL en CI — 2026-11-21

- Workflow backend ampliado con MySQL 8 disposable, bootstrap Admin por ejecución y smoke HTTP real entre dos tenants.
- Script probado localmente en DB independiente: 22 migraciones, aislamiento Vehicles/Drivers/Routes/RoutePositions validado y fixtures eliminados; DB de validación descartada.
- Corregida precedencia DotNetEnv. Las ramas feature todavía no existen en `origin`, por lo que GitHub Actions remoto queda pendiente de publicar esas ramas/PR.
- Cobertura HTTP añadida para rol User: obtiene 403 en listados de Vehicles/Routes y alta de Vehicle; validación local en segunda DB MySQL temporal aprobada.
- Workflow frontend integrado con MySQL, backend de la rama `chore/stabilize-fleet-platform` y Playwright autenticado 8 casos. La suite pasó localmente 8/8; resultado Actions remoto pendiente de publicar PR.
- PRs publicados: frontend #2 y backend #5. Backend Actions pasó; primer frontend run reveló precheck de frontend antes de que Playwright lo iniciara. Corrección y upgrade de acciones Node 24 ya están en la rama, pendiente nuevo run.

## Estabilización de concurrencia y validación del Driver — 2026-11-21

- CreateRoute/UpdateRoute bloquean Vehicle y Driver por propietario bajo una transacción MySQL, en orden fijo, y vuelven a comprobar disponibilidad/agendas antes de guardar. Conflicto de agenda devuelve 409.
- Una ruta pendiente asignada a Driver desactivado no puede iniciarse; la transición falla con 409 y no altera la ruta ni el vehículo.
- Smoke concurrente agregado a CI: probó con MySQL real alta concurrente por mismo vehículo, mismo Driver y edición concurrente; cada pareja produjo un éxito y un 409. El smoke HTTP/MySQL de aislamiento también pasó localmente.
- Cobertura Release xUnit: 49/49. Audit NuGet transitoria detectó CVE-2025-6965 en SQLitePCLRaw 2.1.10; fijado a 2.1.14 y audit posterior sin paquetes vulnerables.
- Estos cambios aún no están publicados en PR; Actions y cobertura frontend deben repetirse después del push. Misión activa.
- Revisión adicional detectó inicio simultáneo de rutas sobre un vehículo; la transición también toma lock de ruta/asignación y comprueba las demás rutas InProgress. xUnit final 50/50 y smoke MySQL concurrente: un HTTP 204 + un 409.
- Smoke concurrente ampliado con verificación SQL: una ruta queda InProgress, la otra Pending y el vehículo OnRoute; carrera con baja de Driver deja exclusivamente estados coherentes. La base final `fletflow_ci_validation_14` pasó; cleanup descartó el esquema.
- Durante la inspección local se mostró accidentalmente la credencial MySQL root en salida de diagnóstico; se rotó inmediatamente para `%` y `localhost`, `.env` quedó 0600 y la conexión temporal se verificó sin exponer la nueva clave.
- El commit `90da59a` está en PR #5. GitHub Actions run `44976384961` completó exitosamente: restore, Release build, xUnit, smoke tenant HTTP/MySQL y smoke concurrente Create/Update/start/desactivación.

## Protección de sesión y consistencia operativa — 2026-11-21

- Cuenta desactivada revoca refresh, y JWT emitidos previamente dejan de autorizar solicitudes. Refresh retorna 401. Smoke HTTP/MySQL lo verificó.
- No se inicia ruta con vehículo inactivo; no se da de baja ruta en progreso ni Vehicle con conductor/ruta pendiente o activa; no se desasigna Driver durante ruta en progreso. Las escrituras concurrentes de dos paradas conservan ambos cambios.
- Vehículo conserva historial y puede reactivarse vía `POST /Vehicles/{id}/reactivate`; el smoke comprobó GET `isActive=false`, POST 204 y GET `isActive=true`.
- Release test local 50/50; Release build 0 warnings/0 errores. Smokes tenant y flota real en MySQL descartable pasaron. Estos cambios están locales en `chore/stabilize-fleet-platform`; Actions y commit/push pendientes.
- La matriz local HTTP autenticada se amplió: Manager 200 en Vehicles/Drivers/Routes; Logistics 200 en Routes y 403 en Vehicles/Drivers; User 403; Driver verificado por Playwright. Smoke MySQL temporal pasó tras la ampliación.

## Validación posterior de roles y concurrencia — 2026-11-21

- Cambio de rol sincronizado con inicio de ruta mediante locks de Vehicle/Driver; revoca refresh tokens y JWT anteriores con claim de rol obsoleto reciben 401.
- Backend Release xUnit **50/50**; paquete NuGet vulnerable: ninguno. Smoke MySQL de tenant/roles, cambio de rol concurrente, baja/reactivación y estados persistidos aprobados; DB y cuenta SQL temporales eliminadas.
- Build Release **0 warnings/0 errors**. Matriz HTTP de mutaciones pasó: Manager crea flota/ruta, Logistics modifica rutas y queda bloqueado en flota, User/Driver no crean rutas. Registro anónimo no eleva rol; logout-all revoca refresh.
- Runner frontend con API y MySQL reales: Playwright **14/14**, incluye rol Usuario UI, operaciones CRUD, responsive y aislamiento multiempresa.
- Falta publicar el lote y validar workflows remotos para sus SHA; continuar cobertura de CRUD/responsive no visitada. SMTP requiere credenciales vigentes; móvil nativo pospuesto por el usuario.
- Smoke HTTP/MySQL también confirmó que registro público no eleva el rol y que logout-all revoca refresh tokens.

## Seguridad de sesión por cambio de rol — 2026-11-21

- Corregida la invalidación inmediata: cambios efectivos incrementan `TokenVersion` en el mismo commit que reemplaza rol y revoca refresh tokens.
- Verificado que un JWT antiguo continúa rechazado tras el ciclo de cambio y restauración del rol. xUnit 57/57, build Release sin warnings y smoke HTTP/MySQL desechable aprobado.
- El primer smoke encontró que faltaba configurar el puerto MySQL; el script acepta ahora `MYSQL_PORT`. No se tocó la DB local.

## Recuperación y activación — 2026-11-21

- Cuatro pruebas nuevas cubren reset/activación: persistencia con BCrypt real, consumo de token, revocación de refresh tokens, limpieza de `MustChangePassword`, token usado y confirmación desigual.
- Suite backend Release: **54/54**; ambos workflows publicados terminaron `success`: backend `44982244087`, frontend `44982243409`.
- No se envió correo. SMTP sigue dependiendo de credenciales externas vigentes. Continúa pendiente ampliar CRUD/responsive; móvil nativo está pospuesto. La misión sigue activa.

## Invalidación de JWT tras cambio de contraseña — 2026-11-21

- Corregido hueco: la rotación de refresh no invalidaba access JWT ya emitidos. `TokenVersion` persistida incrementa en cambio/reset/activación; middleware JWT verifica claim y versión actual.
- Migración aditiva añade `Users.TokenVersion` con default 0; una comprobación al estilo de cambio de contraseña verifica 401 para token viejo y login con contraseña nueva. Smoke MySQL se ejecutará en Actions después del push.
- xUnit 54/54, Release build 0 warnings/0 errors; `bash -n` del smoke y `git diff --check` correctos. No marcar completo hasta validar integración CI y seguir cobertura restante.

- Resultado actualizado: Actions backend `44983337062` pasó con migración y verificación real HTTP/MySQL del cambio de contraseña. Access/refresh viejos 401; contraseña vieja rechazada y nueva aceptada. xUnit y build del workflow también verdes.

## Cobertura negativa de cambio de contraseña — 2026-11-21

- Dos pruebas nuevas cubren cambio exitoso con revocación/hash/version/commit y contraseña actual incorrecta sin efectos secundarios.
- Suite Release actualizada: **56/56**, 0 fallidas/omitidas. La integración MySQL/HTTP ya pasó en Actions `44983337062`; esta ampliación xUnit queda pendiente de CI en el próximo commit.

- CI confirmado tras esta ampliación: backend Actions `44983763612` success, xUnit **56/56**, smoke tenant/roles/sesiones y smoke concurrencia en MySQL desechable.

## CI de revocación tras cambio de rol — 2026-11-22

- Commit `3f3b004`, Actions `44986173700`: success. Build Release 0 warnings/0 errors y suite xUnit 57/57.
- Los smokes HTTP/tenant/roles y concurrencia pasaron con MySQL descartable. Se comprobó que access y refresh tokens viejos sigan rechazados al restaurar el rol original.

## Smoke de creación Logistics — 2026-11-22

- Cobertura HTTP ahora confirma creación positiva de ruta por Logistics con vehículo/Driver del mismo tenant creados por Manager, sin rutas pendientes simultáneas: Manager crea/edita/da de baja; luego Logistics crea, lee y valida persistencia, edita y vuelve a validar, y da de baja.
- El cleanup registra IDs separados para las dos rutas soft-deleted. Smoke aislado con MySQL 8 efímero y DB `fletflow_ci_*`: aprobado; contenedor eliminado. Primer fallo de cleanup y corrección registrados en `docs/ERROR_LOG.md`.
- Commit `43ca30c` está publicado y Actions `44988756481` terminó success; build, xUnit y smokes HTTP/MySQL de tenant, Logistics y concurrencia pasaron en recursos descartables.

## Operación Logistics de inicio a cierre — 2026-11-22

- Smoke HTTP ampliado localmente con paradas de tres puntos y operación PickupAndDelivery, transición InProgress, estado/evidencia de parada, GPS, Completed y devolución del vehículo a Available.
- API temporal + MySQL 8 efímero confirmaron persistencia y cleanup de cero rutas, posiciones y vehículos QA. `bash -n`, `git diff --check` y Release build 0 warnings/errores pasaron.
- El primer intento tuvo un falso fallo de verificación porque el warning MySQL contaminó stdout; corrección y repetición aprobada en `docs/ERROR_LOG.md`. Actions `44990525889` confirmó el ciclo y xUnit 57/57. Se añadió fallback de cleanup por nombre; Actions `44991032520` para `da53081` volvió a pasar build, xUnit 57/57 y smoke cleanup `0:0:0`.

- Actions `44995681296` para `6ec5315` terminó success: build, xUnit, smoke tenant/roles HTTP+MySQL y concurrencia; MySQL/servicios temporales se limpiaron.

- Integridad de conductores: crear Driver, alta de User con rol Driver y editar documento duplicado responden HTTP 409 con mensaje claro. Release local pasó 61/61; CI integrado pendiente.
- Actions `44997043103` para `a7f7c4e` terminó success: build, xUnit 61/61, smokes HTTP/MySQL tenant y concurrencia; servicios desechables se cerraron correctamente.
- La integración Front `1bb7d71` + Back `a7f7c4e` pasó Actions `44997054375`: 61/61 xUnit, responsive 1/1, 16/16 E2E y cleanup success; se verificó documento Driver duplicado como HTTP 409 sin cuenta parcial.

## Atomicidad de alta Driver concurrente — en validación

- Auditoría de backend detectó que `RegisterUserCommand` guardaba al usuario antes de insertar Driver y token, por lo que una carrera de documento duplicado podía dejar una cuenta huérfana.
- Se permite diferir el `SaveChanges` y se agrupan usuario/Driver/token en un solo commit. Si el unique index gana la carrera, se consulta el documento ya persistido y se convierte el error en conflicto 409. AuthService también agrupa user/Driver antes de emitir sesión.
- Smoke HTTP lanza dos POST `/Users` concurrentes con correos distintos y el mismo documento; espera un 201/409 y valida MySQL `Users:Drivers:ActivationTokens = 1:1:1`, con cleanup temporal.
- Release local 61/61; bash syntax y diff-check pasan. Actions con MySQL real pendiente.


## Seguridad de dependencias — 2026-11-22

- NuGet audit repetida sobre la solución: cero paquetes vulnerables en backend y proyecto de pruebas. La auditoría global de criterios funcionales continúa abierta.
# Iteración pendiente CI — opciones operativas para rutas — 2026-11-22

- Nuevo `GET /Routes/assignment-options`, autorizado para Admin/Manager/Logistics, retorna vehículos y conductores activos con campos mínimos. Filtros EF Core limitan la consulta al propietario autenticado; DTO no incluye documento ni teléfono del Driver.
- `scripts/smoke-tenant-http.sh` comprueba Logistics 200 con recursos propios, ausencia de PII, User 403 y tenant B sin recursos de tenant A.
- Local: Release build 0 warnings/0 errors; xUnit 64/64; shell syntax/diff-check correctos. Actions `45011796536` pasó build, xUnit, smoke tenant/Logistics API/MySQL y concurrencia; cleanup correcto.
# Siguiente hardening — opciones realmente asignables — 2026-11-22

- Auditoría encontró que `GET /Routes/assignment-options` podía ofrecer vehículo `OnRoute`/Maintenance y Driver ligado a usuario inactivo. El servicio de creación rechaza ambos casos.
- Implementación local: vehículos sólo `Available` y activos; Drivers activos sin vínculo o ligados a User activo del tenant. Query de Driver usa filtro SQL sin consultas N+1.
- Pruebas: Release build 0 warnings/0 errors; xUnit 65/65; Actions `45014146500` pasó filtros HTTP/MySQL, aislamiento/concurrencia y cleanup Driver/User sin residuos.

## Validación de límites de alta Driver — Actions validado

- `CreateDriverCommand` valida campos requeridos y limita documento/teléfono a 20 caracteres antes de consultar o persistir.
- Release build local: 0 warnings/0 errors. xUnit **72/72**, incluidos rechazo de 21 caracteres, aceptación del límite exacto y cuatro campos vacíos.
- Actions `45016639356` para `8c28ccb` pasó: build, xUnit **72/72**, smoke HTTP/MySQL de tenant/roles/operación y concurrencia; cleanup temporal correcto.
## Asignación exclusiva de vehículo a Driver — en validación

- Hallazgo de auditoría: faltaba impedir que dos Drivers conservaran el mismo vehículo. El comando consulta otras asignaciones del tenant bajo el lock de vehículo.
- xUnit y smoke de concurrencia añadidos para dos asignaciones paralelas, conflicto 409 y verificación SQL de una sola relación. Build/Actions pendientes.
- Release build local: 0 warnings/0 errors; xUnit 73/73; bash syntax y diff-check correctos. Smoke MySQL/Actions del nuevo SHA pendiente.
- Validación completada en Actions `45017642439` (`8d19118`): Release build, xUnit **73/73**, smoke tenant/roles/operación, dos PUT simultáneos contra MySQL con un éxito y un conflicto, relación única y cleanup sin residuos.

## Fecha de creación de rutas — en validación

- Hallazgo: el dashboard titulaba “Actividad Reciente” sobre un endpoint sin orden y Gestión de Rutas mostraba `PlannedStart` (o la hora actual inventada) como fecha de creación.
- Migración aditiva nullable `CreatedAtUtc`; nuevas rutas guardan UTC, las existentes quedan sin fecha histórica inventada. API y repositorio exponen/ordenan datos; update preserva creación. Front ordena recientes con fecha de creación y muestra “No registrada” en históricos sin fecha.
- Local Back Release build 0 warnings/errors, xUnit **73/73**, `bash -n` en ambos smokes; Front lint/build/discovery **18** y diff-check pasan. E2E/API/MySQL de Actions `45021088919` pasó.
- Primer Actions `45019828759`: build/xUnit pasaron; smoke encontró comparación string demasiado estricta entre precisión .NET y `datetime(6)`. Se normalizó a microsegundos y se verifica el valor MySQL antes/después. Actions `45020146775` pasó build, xUnit **73/73**, migración y smoke HTTP/MySQL más concurrencia; cleanup correcto.

## Seguridad de sesiones y catálogo global de Roles — validada

- Hallazgo: access JWT podía volver a funcionar después de reactivar una cuenta; ahora se incrementa `TokenVersion` en ambas transiciones y se revoca refresh al desactivar.
- Hallazgo: Admin tenant podía mutar `Roles` global. GET se conserva; POST/DELETE tenant quedan denegados (403) hasta que exista administración de plataforma.
- Local Backend Release xUnit **74/74**; smoke API/MySQL desechable pasó con token antiguo rechazado tras reactivación, nuevo login permitido, mutaciones de Roles denegadas y cleanup completo. Actions `45021088919` pasó.
- Actions `45021088919` pasó Release build/xUnit **74/74**, smoke tenant/estado/Roles HTTP/MySQL, concurrencia y cleanup.

## Continuación de auditoría — sesiones y errores de carga (2026-11-22)

- Backend tiene cambios locales pendientes de publicar: refresh atómico, logout-all invalida access JWT, y recuperación uniforme sin revelar expiración; manejo de error SMTP sin afirmar envío.
- Smoke HTTP/MySQL local pasó: refresh concurrente 200/401, token viejo 401, sucesor 200, logout-all invalida tokens existentes, login nuevo 200; cleanup limpio. xUnit local previo: 76/76; la repetición Release con runtime local debe anotarse tras concluir.
- Front tiene cambios locales pendientes: Gestión de Conductores hace visible el error de carga de Vehicles y permite reintento. E2E aislado pasó 1/1; lint/build/discovery 19 tests pasan.
- Acciones remotas/commits de estos lotes aún pendientes. Política de Admin delegado sigue abierta a decisión de producto; correo real depende de credenciales SMTP externas; aplicación móvil nativa está aplazada por el usuario.

- Validación backend final local (22-11-2026): `dotnet test -c Release --no-restore` **76/76**; `dotnet build BackEnd_Gps.sln -c Release --no-restore` **0 warnings/0 errores**. El smoke HTTP/MySQL de sesiones terminó exit code 0 con cleanup en cero; smoke de concurrencia de rutas en ejecución.
- Smoke adicional `scripts/smoke-route-concurrency.sh`: exit code 0; validó concurrencia de agenda/asignaciones, roles, rutas y paradas.

- Carrera refresh/logout-all corregida con lock común de fila User. xUnit Release 76/76 y smoke MySQL 8 real (lock deliberado, requests simultáneos, cero refresh activo después del logout y cleanup cero) pasaron.
- La validación JWT de `TokenVersion` ya está en `JwtBearerEvents.OnTokenValidated`; un finding del auditor que sólo examinó `JwtMiddleware` no se reprodujo y se documentó.
- Pendiente de seguridad: ForgotPassword devuelve mismo contenido, pero la duración todavía puede diferenciar correo existente por trabajo DB/envío; implementar/desplegar mitigación antes de cerrar auditoría.

## Recuperación SMTP con respuesta uniforme — lote local posterior a `bcf7bac`

- Nuevo worker bounded de proceso saca SMTP del request; both paths de recuperación responden el mismo JSON con mínimo 250 ms. Unit/HTTP smoke verifican payload y piso; smoke real vio `SmtpHost no configurado`, sin envío ni éxito simulado.
- La rama local tiene cambios aún sin publicar; revisar/commitear y volver a correr Actions.
- Riesgo restante: la cola actual no es durable y el tiempo puede variar si DB está lenta; un outbox persistente cifrado y credenciales SMTP son necesarios para garantía productiva. No considerar seguridad de enumeración totalmente cerrada.

## Actions de recuperación uniforme y sesiones — validación cerrada

- Backend `35ad393`: Actions `45024262135` success; Release build 0 warnings/0 errores, xUnit **76/76**, smoke HTTP/MySQL tenant y concurrencia aprobado, cleanup verificado `0 rutas / 0 posiciones / 0 vehículos`.
- El smoke incluye los asserts nuevos de refresh/logout-all y recuperación de password; no se usó SMTP real.

## CRUD/seguridad de alta de miembros — lote local pendiente de Actions

- User Create backend valida límites (100/200/100/100), trim y duplicate email race 409; Front aplica `maxLength` y valida trims en Gestión de Equipo.
- Smoke HTTP/MySQL real pasó: valores límite aceptados, límite+1 HTTP 400 sin filas, altas concurrentes email 200/409 y una sola User/role/token; Driver options 403; cleanup limpio.
- Front lint/build/discovery 19 pasó tras quitar dos props duplicadas detectadas en primera lint (error documentado). Integración navegador/API/MySQL y Actions para los HEAD nuevos pendiente.

## Outbox durable de recuperación — validación local 2026-11-22

- EmailOutbox cifrado y migración AddEmailOutbox; la API la aplicó al iniciar contra MySQL 8 descartable.
- Token de reset + mensaje outbox se guardan en el mismo commit; recuperaciones concurrentes serializadas mediante lock de User.
- Worker con claim atómico, lease/LockToken, backoff (máximo 6 h), dead-letter tras 12 fallos y purga de enviados a 30 días / dead-letter a 90 días.
- Release xUnit 77/77 y smoke HTTP/API/MySQL completo aprobado; payload no contiene correo en texto claro, requests simultáneos dejaron un token activo y tres mensajes.
- SMTP sigue requiriendo credenciales externas vigentes; configurar y persistir/compartir el key ring FLETFLOW_DATA_PROTECTION_KEYS_PATH en producción.
- Pendientes generales: decisión de producto de Admin delegado, continuar auditoría CRUD/responsive y confirmar Actions de este nuevo commit. No cerrar la misión.

- Verificación adicional post-reinicio: la fila cifrada sobrevivió al apagado de API/MySQL persistente y el proceso nuevo, con el key ring montado en la misma ruta, descifró el mensaje e incrementó AttemptCount; se observó el fallo esperado InvalidOperationException por SMTP sin configurar.
- smoke-route-concurrency.sh pasó también tras aplicar AddEmailOutbox en una DB MySQL 8 limpia.

## Outbox en Actions — 2026-11-22

- Commit 5049dea validado por Actions 45027017954: build Release 0 warnings, xUnit 77/77, smoke HTTP/MySQL tenant/outbox y concurrencia rutas success; cleanup de contenedor success.
- Front Actions 45027026835 también pasó en Front 82941b8 y backend feature branch; ese run es previo a los nuevos casos E2E de filtros/roles/responsive.
# Autorización Driver al cambiar estado — cerrada con regresión de carrera

- Auditoría independiente señaló una ventana TOCTOU: `RoutesController` verificaba asignación antes de llamar al command, mientras `ChangeRouteStatusCommand` bloqueaba y recargaba la ruta sin volver a validar al Driver actor.
- El command ahora recibe los IDs del Driver y usuario solicitantes; después del lock y reload verifica que el Driver siga activo y que continúe asignado, incluyendo compatibilidad con rutas históricas que guardan `UserId`. Rechazo uniforme mediante `KeyNotFoundException`.
- Nueva prueba `ChangeRouteStatusAuthorizationTests` sustituye la ruta al momento del lock para simular reasignación concurrente; demuestra que el Driver antiguo no cambia estado ni persiste modificaciones.
- Prueba enfocada 1/1; suite backend Release `dotnet test ... -m:1`: **78/78**, cero fallidas/omitidas; `dotnet build -c Release --no-restore -m:1`: 0 warnings/0 errors. Actions del commit con el cambio pendiente.
# Smoke de CreatedAtUtc: comparación estabilizada

- Actions `45029094282` falló sólo en el smoke HTTP que compara timestamp API/MySQL después de editar; build y xUnit pasaron. La prueba anterior normalizaba fracciones largas, pero no añadía ceros cuando JSON omitía ceros de precisión.
- El helper ahora normaliza siempre a seis decimales, como MySQL `datetime(6)`, y el fallo imprime los cuatro valores para diagnóstico.
- La repetición pasó en Actions `45029374847` para Backend `5103b51`: build Release, xUnit 78/78, smoke tenant/outbox HTTP/MySQL, concurrencia de rutas y cleanup success.

## Matriz HTTP de Driver contra ruta ajena del mismo tenant — Actions validado

- El auditor independiente confirmó que el E2E frontend cubre Driver ajeno, pero el smoke backend sólo probaba rutas de otro tenant.
- `smoke-tenant-http.sh` autentica el Driver creado por la carrera de unicidad y comprueba filtro/listado, detalle, estado, parada, evidencia y GET/POST GPS sobre una ruta asignada a otro Driver del mismo tenant.
- Admin vuelve a consultar ruta/GPS y confirma que estado, evidencia y posición persistida no cambiaron. Actions `45029798323` pasó en Backend `ec3ce52`: Release build, xUnit 78/78, smoke HTTP/MySQL, concurrencia y cleanup success (0 fixtures QA).

## Outbox: worker, entrega y dead-letter automatizados — local validado

- `EmailOutboxWorkerTests` ejecuta el BackgroundService contra SQLite y el sender fake, no mocks del flujo interno.
- Caso de éxito comprueba descifrado de correo/link/token, llamada al sender, `SentAtUtc` y liberación del lease.
- Caso de fallo inicia en el intento 11 y confirma llamada al sender, `AttemptCount=12`, dead-letter, failure type y lease liberado.
- Suite Release local: **80/80**; build Release 0 warnings/0 errors. Actions `45030208849` pasó para Backend `43c074d`: 80/80, HTTP/MySQL tenant/outbox/same-tenant Driver, concurrencia y cleanup success.

## Seguridad — revisión del 2026-11-22

- Corregidos localmente tres hallazgos: actualización no atómica del rate limit, endpoint de email diagnóstico para destinatarios arbitrarios y key ring de Data Protection sin cifrado explícito.
- Rate limiter ahora incrementa atómicamente; 200 requests paralelas producen 60 aceptadas/140 rechazadas. El endpoint test-email queda sólo en Development y restringido al correo autenticado. Production exige path persistente y PFX para proteger las claves.
- Backend Release tests 85/85 y build 0 warnings/0 errors. Validación Actions pendiente.
- Pendiente decisión de política: Admin delegado puede crear/promover Admin. También falta provisionar certificado/key ring en infraestructura productiva; el límite distribuido por IP requiere un servicio externo antes de escalar a varias réplicas.
- Cierre CI backend `94c3e90`: Actions `45037399332` pasó build (0 warnings/0 errors), xUnit 85/85, smokes tenant/roles/flota y concurrencia MySQL, cleanup success. Cambios de seguridad locales ya integrados.
- Seguridad Data Protection: prueba nueva verifica cifrado real del XML del key ring y descifrado tras reiniciar provider. Suite local backend 86/86; build 0 warnings/0 errors; CI de la prueba pendiente.
- Revalidación Front actual `45038687601` pasó con Backend exacto `b625c28`: xUnit 86/86, E2E API/MySQL 23/23, responsive 1/1, audit/lint/build y cleanup success.

## Subtarea de estabilidad backend — en verificación (2026-11-22)

- Rate limit: prueba sincroniza 200 solicitudes concurrentes por una IP, comprueba solapamiento y resultado 60/140.
- Data Protection: certificado previo opcional para descifrar key ring rotado; certificado actual sigue cifrando claves nuevas. Variables documentadas: `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PATH` y `FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PASSWORD`.
- Test de rotación cubre payload anterior y round-trip de payload nuevo con provider actual + anterior; no escribe XML cifrado a logs.
- Tests enfocados pasaron 2/2; rotación aislada 1/1; suite Release **86/86**; build Release **0 warnings/0 errores**. Actions `45040550853` pasó con HTTP/MySQL isolation, concurrencia de rutas y cleanup. Commit `b664b2c`; roles sin cambios.

## Cuenta Driver inactiva y ciclo de ruta — local validado, Actions pendiente

- Hallazgo: se podía iniciar ruta pendiente con `Driver.IsActive=true` y cuenta User vinculada `IsActive=false`; vehículo pasaba a OnRoute sin que Driver pudiera iniciar sesión.
- El start ahora bloquea/valida la cuenta User después de locks de ruta/vehículo/Driver. La baja de una cuenta vinculada toma los mismos locks y responde 409 si tiene ruta en progreso. La cuenta permanece activa y no se revocan sesiones en ese conflicto.
- xUnit cubre inicio de ruta denegado con cuenta inactiva y baja denegada durante ruta activa, sin mutaciones inconsistentes. `smoke-tenant-http.sh` añade verificación HTTP/MySQL de ambos caminos y limpieza de sus recursos.
- Validación local: Release xUnit **88/88**, restore al día, Release build **0 warnings/0 errores**, `bash -n` y diff-check correctos. Backend `190f284` pasó Actions `45045070288`: build/tests Release, xUnit **88/88**, smoke HTTP/MySQL tenant+flota, concurrencia de asignación y cleanup.
- Misión global sigue abierta: cobertura CRUD/responsive pendiente, política Admin delegado requiere decisión, credenciales SMTP/infraestructura PFX son externas; app móvil nativa aplazada por usuario.


## Worker de recuperación — suppress de tokens inactivos — 2026-11-22

- Backend valida vigencia del token reset inmediatamente antes de envío; un mensaje obsoleto no se presenta como email enviado ni se reintenta como fallo SMTP.
- Worker enfocado 3/3, suite local 92/92, build 0/0. Nuevo Actions backend y corrida Front integrada pendientes.
- Cierre CI backend `27bf75a`: Action [45048795456](https://github.com/Paul1578/Back_GPS/actions/runs/45048795456) pasó. Release build, xUnit **94/94**, aislamiento HTTP/MySQL, regresión de reasignación con ruta pendiente, concurrencia de rutas y cleanup; no quedaron fixtures de smoke.

## Estado actual — autorización Driver y recuperación de password — 2026-11-22

- CreateRoute y UpdateRoute validan cuenta enlazada al Driver después de locks; regresión SQLite evita edición parcial. Smoke tenant cubre edición con cuenta inactiva y endpoints activate/reset.
- Test focal CreateRouteCommandTests 10/10; bash syntax y diff-check correctos. Falta suite Release completa, API/MySQL efímera y Actions.
- Admin delegado sin decisión de producto. SMTP/PFX/infra productiva externas; móvil nativo aplazado.
- Misión integral abierta.

## CI más reciente — 2026-11-22

- Backend 53e2edc: Release build y xUnit 100/100 correctos. Action 45060482906 falló en smoke HTTP porque suffix estaba declarado después de usarlo. Corrección trasladada al inicio del script; nueva Action pendiente.
- No marcar terminada esta tanda hasta que MySQL smoke complete y cleanup quede verde.

## CI del smoke MySQL — 2026-11-22

- Action 45060714338 sobre Backend 14348f3 pasó Release build, xUnit 100/100, smoke tenant HTTP/MySQL y concurrencia de asignación; cleanup correcto.
- La inicialización temprana de suffix resolvió el fallo del Action 45060482906. El smoke ejercitó los nuevos endpoints de activación/restablecimiento y la edición/inicio con cuenta Driver inactiva.

## Fixtures E2E de conductor aislados — 2026-11-22

- El runner provisiona un Driver adicional exclusivo para el escenario operativo de Mi Ruta/evidencia; el fixture de Driver de gestión queda libre para CRUD. Los IDs JSON se extraen con Node sin introducir dependencia jq.
- La sintaxis del runner pasa localmente. Action integrado de backend pendiente para verificar provisioning y cleanup en MySQL efímero.

- Backend Action `45061524504` pasó para `3f87af8`: Release build, xUnit, smoke de aislamiento HTTP/MySQL, concurrencia de asignaciones y cleanup. El commit también contiene el fixture Driver adicional; la comprobación del runner dentro del flujo frontend sigue en curso.

## Validación y carrera de placas de Vehículos — CI integrado pendiente

- Auditoría detectó campos que podían exceder límites de persistencia y una carrera create/update de placa única que salía como 500. Contratos y comandos ahora validan en borde HTTP y de aplicación y traducen sólo duplicidad confirmada a 409.
- Se añadieron pruebas de entrada inválida y carrera duplicada. Release local con DOTNET_ROOT configurado; el host `/usr/bin/dotnet` del sistema estaba roto, la repetición usa `/home/leonardo/.dotnet`.

- Vehicle Release local: xUnit **103/103**, build **0 warnings/0 errors** con SDK `8.0.428` seleccionada desde `/home/leonardo/.dotnet`; `bash -n` smoke y diff-check pasan. Backend Action `45063488394` pasó en SHA `84e7c66`, incluyendo HTTP/MySQL (400 por placa/año inválidos y 409 en duplicados de alta/edición).

## Cambio de rol y cuenta Driver inactiva — CI integrado pendiente

- El análisis encontró que cambio de rol Logistics→Driver reactivaba el perfil vinculado sin activar la cuenta User. Se condicionó a `User.IsActive`; Release local 104/104. Smoke HTTP/MySQL comprueba que la cuenta inactiva no recupera un perfil operativo.
- La primera Action `45063802250` identificó que el smoke se ejecutaba antes de terminar la ruta y que cleanup borraba el vehículo antes del Driver (FK restrictiva). Ambos casos corregidos; nuevo CI pendiente.
- Backend Action `45064477054` pasó en commit `1534828`: build, 104/104 xUnit, smoke HTTP/MySQL del cambio de rol y cleanup.

## Seguridad — anti-enumeración en login y registro

- Login ahora responde uniformemente ante email inexistente, password incorrecto, cuenta inactiva o pendiente; BCrypt usa hash señuelo para el usuario inexistente. Registro público responde igual con email nuevo/duplicado, sin emitir sesión; Front lleva a Login.
- Backend Action `45066082294` pasó tras añadir hash antes del pre-check y piso 500 ms: build, xUnit **107/107**, HTTP/MySQL para email nuevo/duplicado, tiempos mínimos y cleanup.
# Actualización 2026-11-23 — Política Admin y perfil

- Confirmada la regla de negocio: sólo la cuenta raíz puede crear o promover usuarios a Admin. Backend aplica 403 a Admin delegado en `POST /Users`, `PUT /Users/{id}/role` y alias `POST /Users/{id}/roles/{name}`. La raíz conserva la operación.
- Prueba HTTP/MySQL desechable verifica denegaciones sin filas/roles parciales y promoción permitida por raíz. También cubre `PUT /Users/{id}/profile` y sincronización persistida del Driver vinculado.
- Backend Action `45088129188`: Release build sin warnings/errores; 113/113 xUnit; smoke tenant HTTP/MySQL, concurrencia de asignaciones y cleanup correcto.
- Hallazgo de prueba corregido: primer Action `45087881487` recibió 400 porque el POST de fixture Admin omitía Password; la prueba ahora proporciona Password y verifica el 403 de autorización. Ver `ERROR_LOG.md`.
- Front Action `45086827516`: 39/39 E2E y 1/1 responsive, audit/lint/build y cleanup. Revisar UX de selección de rol para que refleje la política de cuenta raíz.
- Misión no terminada; continuar CRUD, permisos y responsive. App nativa móvil pospuesta. Envío SMTP real y provisión productiva de PFX/key ring son limitaciones externas.
- `/Auth/me` devuelve `isRootAccount` calculado por backend; smoke exige true para raíz y false para Admin delegado. Action `45090274537` pasó Release build sin warnings, xUnit 113/113, HTTP/MySQL, concurrencia y cleanup.
- La Action Front `45090273209` sigue ejecutando E2E integrado contra este backend; esperar para confirmar la opción de rol de raíz en navegador real.

## Ciclo multi-parada — validación local 2026-11-23

- `scripts/smoke-tenant-http.sh` amplía el caso Logistics a cinco puntos: origen, recogida, entrega, parada combinada y destino. Verifica estados y evidencia persistidos en cada operación, GPS, cierre de ruta y vehículo Available.
- Build Release y smoke API/MySQL 8 desechables pasan localmente; Action `45093565790` también ejecutó el smoke actualizado y limpió fixtures.
- Se mantiene pendiente la política de rutas sin operaciones, consultada al usuario.

- Suite xUnit local con `/home/leonardo/.dotnet/dotnet test BackEnd_Gps.sln -c Release --no-restore -m:1`: **113/113**, 0 fallidas/omitidas. El primer intento apuntó al nombre supuesto `fletflow.sln` y falló por archivo inexistente; corregido al usar la solución real `BackEnd_Gps.sln`.

- Backend Action [45093565790](https://github.com/Paul1578/Back_GPS/actions/runs/45093565790) pasó en `e678e6c`: build Release, xUnit **113/113**, smoke HTTP/MySQL tenant y concurrencia; contenedor y fixtures limpiados.

- Auditoría de seguridad independiente (solo lectura) confirma controles de JWT/refresh/IDOR root-only revisados y sin bypass nuevo; quedan dos riesgos P2: contraseña mínima seis caracteres y rate limit de login sólo global 60/min por IP/proceso. Política de contraseña y defensa contra fuerza bruta por definir/implementar; ver `SECURITY.md`.

## Decisiones de producto y endurecimiento — 2026-11-23

- Usuario confirmó que se permiten rutas de traslado sin operaciones; backend domain test verifica Pending→InProgress→Completed sin paradas de operación.
- Contraseñas nuevas ahora requieren 12 caracteres (registro/cambio/reset/activación); las claves actuales no se invalidan. Login limita a 10 POST/min/IP sobre el límite general de 60/min y no bloquea cuentas.
- Release xUnit local **120/120**. HTTP smoke completo y Action sobre estos cambios pendientes de commit.

## E2E y limpieza MySQL — diagnóstico pendiente

- Front Action `45102498423`: los **43/43 E2E pasaron**, incluidos traslado, conductor ocupado, ruta multi-parada y confirmaciones de rol/estado; falló el cleanup de MySQL.
- Backend `scripts/run-e2e-local.sh` ocultaba el stderr del comando `mysql`. Se actualizó para mostrar el mensaje sólo ante fallo y borrar el archivo temporal. Causa SQL exacta aún pendiente de la siguiente Action.

## Cleanup QA corregido — validar en CI

- Front Action `45103134467`: **43/43 E2E** pasaron; cleanup falló con MySQL 1451 porque quedaba una ruta ligada al Driver QA bajo `ON DELETE RESTRICT`.
- `cleanup-e2e.sql` ahora elimina posiciones y rutas por nombre QA o por `Driver.DocumentNumber LIKE 'E2EQA%'`, antes de borrar los Drivers. Repetir Front Action para comprobar cleanup en DB efímera.


## Resultado CI de cleanup y estabilidad

- Backend `7ea14a6`, Action [`45103732510`](https://github.com/Paul1578/Back_GPS/actions/runs/45103732510): Release build/xUnit, smoke tenant HTTP/MySQL y concurrencia success.
- Front Action [`45103761122`](https://github.com/Paul1578/Front_gpsR/actions/runs/45103761122): 43/43 Playwright, responsive 1/1, cleanup MySQL success; valida backend `7ea14a6`.
- Root cause MySQL 1451 resuelto: SQL limpia Routes/RoutePositions relacionadas por Driver QA antes de borrar Driver. Misión general sigue abierta.

## Reasignación concurrente y baja lógica — VALIDADO EN CI — 2026-11-23

- Backend `f49ee13`, Action [45153378636](https://github.com/Paul1578/Back_GPS/actions/runs/45153378636): Release build, xUnit **139/139**, smokes HTTP/MySQL de tenant y concurrencia, login Staging y cleanup pasaron.
- La edición concurrente confirmó ganador/perdedor por API y SQL; la baja lógica quedó verificada por GET, `onlyActive=true` y persistencia MySQL.
- Rutas de traslado sin paradas permitidas; las paradas admiten recogida, entrega o ambas.
