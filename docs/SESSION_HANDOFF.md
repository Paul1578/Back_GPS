# Handoff vigente — 2026-11-23

## Login rate limit HTTP — CI verificada

- Backend branch `chore/stabilize-fleet-platform`, commit de código `88f669d`; `main` intacta y el árbol quedó limpio tras registrar el resultado.
- El middleware omite rate limiting en Development loopback por diseño. El script se protege con `CI=true` y DB `fletflow_ci*`, detiene API Development y reinicia el mismo Release binario en Staging sobre la DB descartable. Genera key ring/certificado Data Protection efímeros, valida 10 login HTTP 401, intento 11 HTTP 429 + Retry-After y acceso 200 a la misma cuenta desde IP `127.0.0.3` tras exceder el límite con `127.0.0.2`; elimina API, keyring/cert y fixtures temporales al salir.
- Verificación local real: MySQL 8 en contenedor disposable, API y migraciones reales; el script pasó. Fixture usado sólo en `fletflow_ci_rate_limit`; contenedor y procesos se retiraron. Release build 0 warnings/errores, xUnit **136/136**, `bash -n` y `git diff --check` pasan.
- Primeros arranques fallaron antes de ejecutar el test por el `dotnet` de PATH local sin runtime; quedó corregido al priorizar `/home/leonardo/.dotnet` en PATH. Detalle y prevención en `ERROR_LOG.md`.
- Action [`45126308692`](https://github.com/Paul1578/Back_GPS/actions/runs/45126308692) pasó: Release build, xUnit **136/136**, tenant HTTP/MySQL, concurrencia y smoke Staging: diez 401, 11.º 429 con Retry-After, login válido de la misma cuenta desde otra IP. Cleanup/stop API/containers correctos.
- Front actual `refactor/route-builder` `2cc3ee5`, con código `f0acce7` y SHA documental `1d7b922` verificados por Actions `45123444413` y `45124271067` (51/51 E2E y responsive 1/1). Backend `88f669d` y Action `45126308692` success.
- Persisten pendientes de producción: SMTP, rate limiter distribuido/proxies confiables, key ring Data Protection persistente/cifrado y despliegue. No completar toda la misión por cerrar sólo este lote.

---

# Handoff vigente — 2026-11-23

- Fixture Backend `c32febe` prepara un Admin delegado temporal bajo el tenant raíz para el E2E Front real; activa sólo ese usuario y limpieza por prefijo existente. `bash -n` y diff-check pasan; validar con Action Front contra branch actualizado.
- La prueba Front prevista usa credenciales de fixture, verifica `/Auth/me` (`isRootAccount=false`), UI sin rol Admin, 403 real en creación/promoción y persistencia del miembro como User. Front `a510021` pasó localmente lint/build y Playwright discovery **51 tests**; E2E integrado pendiente de Action.
- Nuevo lote Backend `32c435b` agrega una prueba de login con clave heredada corta y amplía `smoke-tenant-http.sh`: rechaza claves menores a 12 en registro/cambio/activación/recuperación sin mutar usuario/token y ejecuta un traslado HTTP completo sin paradas operativas, verificando estado SQL y vehículo disponible.
- Verificación local de este lote: Release xUnit **136/136**, `bash -n`, `git diff --check`, `smoke-tenant-http.sh` y `smoke-route-concurrency.sh` pasaron contra API + MySQL 8 descartables. Cleanup confirmó cero fixtures. El contenedor se detuvo y eliminó; la DB local no se utilizó. Acción de CI para `32c435b` se ejecutará al publicar.
- Error de preparación resuelto: el primer runner usó el host global `dotnet` sin runtime; el proceso terminó antes de iniciar API. Repetición con `DOTNET_ROOT=/home/leonardo/.dotnet` y `/home/leonardo/.dotnet/dotnet` completó ambos smokes; detalle en `ERROR_LOG.md`.
- Próximo lote de mayor valor: unir política root-only con sesión Front real; verificar UI del constructor con un OSRM exitoso; agregar verificación HTTP de login rate-limit en pipeline no Development y conservar cobertura repetible independiente de proveedor.
- Backend `chore/stabilize-fleet-platform` HEAD `ec15ea6`; Front `refactor/route-builder` HEAD `d0a9342`. Ambas ramas separadas de `main` y limpias.
- Backend Action [`45115773126`](https://github.com/Paul1578/Back_GPS/actions/runs/45115773126), commit documental `ec15ea6`, pasó. El código backend `6f48f06` también pasó Release/xUnit **135/135**, smoke HTTP/MySQL tenant, concurrencia y cleanup en Action [`45113924558`](https://github.com/Paul1578/Back_GPS/actions/runs/45113924558). Front Action [`45115772569`](https://github.com/Paul1578/Front_gpsR/actions/runs/45115772569), commit `d0a9342`, pasó audit/lint/build, responsive **1/1**, backend Release/xUnit, API/MySQL **50/50** y cleanup; la Action [`45114492930`](https://github.com/Paul1578/Front_gpsR/actions/runs/45114492930) ya había validado el cambio de código funcional.
- Front `f71ae7d` añade comprobación E2E de carga inicial del Dashboard y configura `webServer` de Playwright con el puerto de `PLAYWRIGHT_BASE_URL`; con servidor limpio :3010, focal 4/4 y discovery 46 local. El error por servidor Next obsoleto/puerto fijo y su prevención están en Front `docs/ERROR_LOG.md`.
- Cleanup FK 1451 resuelto en Backend `7ea14a6`: borrar puntos/rutas ligadas al Driver QA antes del Driver. Confirmado por Front Action `45103761122` y repetición documental `45104448741`.
- Seguridad implementada: 12 caracteres claves nuevas, login 10/min/IP, reset 5/10 min/IP sin bloqueo por cuenta, token 256 bits, TTL 60 min por defecto (1–1440), URLs HTTPS en Production.
- Riesgos productivos restantes: SMTP credenciales; rate limiter distribuido e IP confiable con proxy; Data Protection persistente; despliegue productivo no validado. Misión amplia sigue abierta.
- Decisiones de usuario: sólo raíz asigna Admin; traslados sin paradas sí; una parada puede ser pickup/delivery/ambas; móvil nativo aplazado.
- Las notas históricas debajo pueden estar superadas. Usa este resumen y `PROJECT_STATUS.md` vigente para continuar.

## Auditoría de seguridad de datos heredados — 2026-11-23

- La revisión estática identificó precondiciones de migración; el preflight read-only local ya confirmó 5 usuarios, 2 Admin sin `OwnerUserId`, 0 usuarios con múltiples roles, 0 rutas sin propietario y 0 discrepancias de propietario. Las dos cuentas Admin raíz coinciden con la regla informada por usuario. Un vehículo sin conductor ya pertenece al Admin raíz más antiguo elegido.
- Causa del acceso denegado: `.env` local tenía `DB_PASSWORD` distinta de `MYSQL_ROOT_PASSWORD`; el contenedor conserva una contraseña de inicialización antigua. Se sincronizó DB_PASSWORD en el archivo local ignorado, sin imprimir credenciales. No se editaron filas.
- Al iniciar API local se aplicaron cuatro migraciones pendientes revisadas como aditivas: TokenVersion, nombres User/Driver, CreatedAtUtc en Routes y tabla EmailOutbox. Migraciones aplicadas **26/26**; conteo Users se mantuvo en 5 y EmailOutbox está vacía. Swagger HTTP 200. Release build 0 warnings y xUnit 135/135.
- Otros riesgos de despliegue confirmados por revisión estática: IP de rate limit detrás de proxy y límite en memoria por proceso; rate específico/antiabuso de registro; posible diferencia temporal en recuperación. No se probó configuración de proxy ni una instancia de producción.
- Auditoría Front independiente: exportación, responsive secundario y ciclo real reset/activación están validados. Persistencia del tema se añadió en Front `f859b19`; Action `45113527889` está pendiente.
- Backend `585fcb0` implementó límite público 5/10 min/IP. Action `45110342238` confirmó build Release, xUnit **135/135**, smoke tenant HTTP/MySQL y concurrencia, cleanup limpio.
- Front `f1e1ea7`, Action `45110885218`: descarga JSON verifica usuario real de empresa, listas, fecha; suite 46/46, responsive 1/1, build/lint y cleanup pasan.
- Runner `96b50cc` crea usuarios dedicados de reset/activación y tokens aleatorios; sólo persiste hashes SHA-256 en DB QA descartable. Front Action `45112755472` usó formularios reales, confirmó nueva password, clave anterior rechazada, replay 401 y cleanup. No hay entrega SMTP simulada.

## Verificación de ruta multi-parada — 2026-11-23

- Backend `a29a2fa` y Action `45108549336`: smoke de ciclo completo API/MySQL descartable; evidencia de pickup, delivery y operación combinada se guardó en las tres paradas; la ruta terminó Completed y el vehículo Available. xUnit 133/133; cleanup sin datos QA residuales.
- Errores encontrados al hacer determinista el smoke: MySQL CLI entrega `BIT` como byte y una repetición sobre el mismo proceso reusaba rate limit/outbox. Se normalizó `IsActive` con `CAST(... AS UNSIGNED)` y se repitió con DB/API efímeros limpios; detalles en `ERROR_LOG.md`.

---

# Handoff — 2026-11-23

## Forwarded Headers bajo allowlist — CI pendiente

- Backend `chore/stabilize-fleet-platform`; HEAD anterior `3887201`, cambios locales en `Program.cs`, `appsettings.json`, `scripts/smoke-login-rate-limit-http.sh` y documentación. No tocar `main`.
- `UseForwardedHeaders` procesa XFF/XFP antes del rate limiter, un salto, simetría obligatoria y sólo IPs explícitas configuradas en `ForwardedHeaders:KnownProxies`; default vacío. En despliegue, definir `ForwardedHeaders__KnownProxies__0=<IP ingress>` con las IP reales y confiables.
- Validación local real con API Staging y MySQL descartable: XFF falsificado desde origen no confiable ignorado; 5 intentos desde cada uno de dos proxies confiables sobre la misma IP cliente producen 10 HTTP 401; el 11.º responde 429 + Retry-After; otra IP cliente autentica con HTTP 200. Release build y xUnit 136/136 pasaron antes del smoke; `bash -n` y `git diff --check` deben repetirse tras cerrar cambios.
- Action `45126308692` valida la versión previa del smoke; CI para forwarded headers aún pendiente. Rate limiter continúa en memoria. SMTP, key ring persistente y despliegue productivo siguen pendientes.

## Estado vigente — seguridad de recuperación en trabajo local

- Rama backend: `chore/stabilize-fleet-platform`, base `729d56a`; cambios locales integrados y listos para commit/publicación de la rama de trabajo.
- Cambio local: límite de `POST /api/Auth/forgot-password` de 5/10 min/IP, token de 32 bytes, TTL default 60 min (override 1–1440) y validación Production HTTPS para enlaces reset/activación; Development admite HTTP loopback. Se valida explícitamente antes de sembrar DB; Production debe sustituir enlaces localhost de appsettings.
- `SimpleRateLimitMiddleware` usa memoria por proceso y `RemoteIpAddress`; no hay middleware `ForwardedHeaders`. Para varias réplicas hace falta rate limit distribuido; sólo aceptar IP reenviada tras configurar proxies confiables.
- Primera compilación de tests falló por una llamada fixture con 10 argumentos a `RouteE.Create`, que acepta 9; se quitó el argumento extra y quedó registrado en `ERROR_LOG.md`.
- Tests focalizados Release pasaron 17/17; suite completa Release `BackEnd_Gps.sln` pasó 133/133, 0 fallidos y 0 omitidos; build Release 0 warnings/0 errores. SMTP no se simula ni se informa como enviado.
- Continúa abierto el trabajo general del proyecto; este lote no verifica credenciales SMTP, proxy real ni persistencia del key ring en un despliegue productivo.

## Estado vigente — 2026-11-23

- HEAD Backend `df88238` (`chore/stabilize-fleet-platform`) y Front al actualizar este registro `992053d` (`refactor/route-builder`), ramas separadas de `main`.
- Action Backend `45090274537` pasó Release build (0 warnings/0 errors), xUnit 113/113, aislamiento/permisos y perfil HTTP/MySQL, concurrencia y cleanup. `df88238` registra esa evidencia.
- Política Admin decidida por usuario y validada: sólo cuenta raíz puede crear/promover Admin. El primer Action `45087881487` falló porque el POST de fixture omitió Password; corregido y registrado en `ERROR_LOG.md`.
- Front `9d84562` pasó Action `45091340306`: backend, audit/lint/build, responsive 1/1 y E2E API/MySQL 40/40. Se revisó la traza de la Action anterior `45090273209`: el botón Gerente estaba visible, pero `exact: true` no contemplaba la descripción accesible.
- Front commit `992053d` mejora revisión de paradas y cancela búsquedas obsoletas; Action `45092540906` está en curso para validar búsqueda Nominatim, búsqueda vacía/error/tardía y reintento de guardado tras 503.
- Auditoría backend read-only no encontró defecto en root-only ni aislamiento tenant. Quedan pendientes prueba de ciclo logístico de varias paradas y configuración/prueba de Data Protection persistente.
- Decisión confirmada: se permiten traslados sin recogida/entrega; la prueba de ciclo domain valida inicio y finalización.
- Decisión confirmada: claves nuevas de mínimo 12 caracteres y 10 intentos de login por minuto/IP, sin bloquear cuentas. Código y pruebas locales agregados; publicar ambos branches y verificar Actions.
- Commit Backend `33e2499` pasó Action `45096399172`: build Release, xUnit 120/120, aislamiento tenant y concurrencia Vehicle/Driver por HTTP/MySQL, limpieza correcta.
- Siguiente paso: terminar CI de los cambios de esta fase; después completar criterios funcionales restantes y seguir auditando CRUD, seguridad y responsive.
- Misión sigue abierta. SMTP requiere credenciales externas; Data Protection PFX/key ring requiere infraestructura persistente; app móvil nativa aplazada.
- E2E Front Action `45088879212` detectó que root autenticado no veía opción Admin aunque la API aceptaba la política. Se agregó `isRootAccount` backend-derived a `/Auth/me`, consumido por Front. Backend Action `45090274537` pasó: 113/113, build limpio, smoke HTTP/MySQL, concurrencia y cleanup. Front Action `45090273209` aún valida navegador integrado.

## Estado vigente — actualización concurrente Driver

- Rama backend `chore/stabilize-fleet-platform`; Front `refactor/route-builder`; no tocar main.
- Front Actions `45082924174` y `45083215716` vieron SQL `Drivers.VehicleId=NULL` tras asignación exitosa. La causa fue `UpdateDriverCommand` guardando una lectura vieja mientras `DriverRepository.UpdateAsync` copia el `VehicleId`. Fix local usa lock compartido, relee Driver y hace rollback en error.
- Regresión xUnit simula una asignación ocurrida después de la lectura inicial y antes del lock; el nuevo reread conserva la relación. Release xUnit **113/113** con `/tmp/fletflow-dotnet/dotnet`; el dotnet global de esta máquina carece de runtime.
- `scripts/run-e2e-local.sh` ahora exporta `FLETFLOW_E2E_DRIVER_USER_ID` junto al DriverId para el E2E de Manager mapa. En la Action Front confirmar que el test no fue omitido.
- Backend `f8edbcd` pasó Action `45084368819` (Release build/xUnit 113/113, smoke tenant HTTP/MySQL y concurrencia); backend runner/docs `387b62f` pasó Action `45084501616`. Pendiente ver resultado Front autenticado y confirmar el E2E Manager mapa en ejecución.
- Front Actions `45084368342`/`45084699175` pasaron 38/39; Manager mapa quedó como el único fallo, por navegación mobile-only y rol residual después de timeout. Runner actual crea fixtures Manager y Driver aislados; limpiar con SQL y repetir Front Action.
- Backend commit `1ce4d45` validado por Action `45085910280`: Release build, suite xUnit, smoke HTTP/MySQL de aislamiento de tenant y concurrencia de asignación; resultado `success`, cleanup incluido. Queda pendiente la Action Front `45085920993` para el E2E aislado Manager.
- Runner `2619531` exporta `FLETFLOW_E2E_MAP_FIXTURE_SUFFIX` estable para que un restart de worker reutilice el nombre durante cleanup; `bash -n`/diff-check pasan localmente, debe verificarse en la siguiente Action Front.
- Action Backend `45086298118` para `2619531` pasó. La Front Action anterior `45085920993` terminó 39/39 antes de consumir esta variable; la nueva Front `104723a` valida el sufijo estable.
- Auditoría retiró `RoutesPRUEBAController` (tres endpoints estáticos, sin consumidores); backend Action `45086663424` pasó el smoke tenant, incluida comprobación 404 autenticada, build/xUnit, aislamiento y concurrencia.
- Usuario definió que sólo cuenta raíz puede crear/promover Admin. Controller restringe POST Users, PUT role y alias; smoke crea Admin delegado, verifica 403 y SQL intacto, luego prueba promoción permitida desde raíz. Validación local/Action pendiente.
- Gap de auditoría de perfil cubierto en smoke: PUT exitoso sobre User con Driver asociado, respuesta y GET Driver, SQL verifica ambos nombres/owners y vínculo único. El smoke aún debe correr después del commit.
- No marcar objetivo completo; continuar matriz CRUD/roles/responsive después del resultado Front.

- `76c2cc8` pasó Action `45077658712`; `229076d` pasó Action `45078321298` (Release build, xUnit 111/111, HTTP/MySQL tenant, concurrencia y cleanup), incluidos los 403 y la ausencia de mutaciones User en Users/Roles.
- Durante la revisión estática se detectó una comilla faltante en el JSON del POST Driver usado por el nuevo smoke; se corrigió antes de ejecutar la prueba. Detalle y prevención en `docs/ERROR_LOG.md`.
- `fa16d05` publica el camino Driver propio (SQL `3:0:1:<VehicleId>`) y límites Manager/User y Logistics en endpoints de flota. Primera repetición de Manager llamó un helper que esperaba nombre/estado inicial de una ruta ya editada/baja legítimamente; ahora compara solo Vehicle/Driver. Action `45080063668` pasó build, xUnit 111/111, smoke HTTP/MySQL, concurrencia y cleanup.
- xUnit añade cobertura positiva del comando `AssignDriverToVehicle` con Driver sin `UserId`; Release **112/112** pasa localmente, Action de este nuevo caso pendiente.
- Front `refactor/route-builder` HEAD `2147be7` pasó Action `45076629351`: E2E **37/37**, responsive 1/1, build/lint/audit y cleanup. Estado documentado en el repo Front.
- Nunca ejecutar pruebas destructivas sobre el MySQL persistente; las validaciones locales usaron MySQL 8 temporales en puertos 13316/13317/13319/13321 y DB `fletflow_ci_*`, eliminados al finalizar. Próximo: continuar responsive Front y seguir la Action integrada.

- Branch backend `chore/stabilize-fleet-platform`; nuevo cambio local bloquea la desactivación de cuenta User vinculada a Driver con rutas activas `Pending`/`InProgress`, bajo locks de asignación y usuario. Test/domain y smoke HTTP/MySQL ya pasaron localmente; Action aún no lanzada hasta publicar el cambio.
- Validación: Release xUnit 111/111, Release build 0 warnings/0 errors, `smoke-tenant-http.sh` completo sobre API + MySQL 8 descartable `fletflow_ci_user_deactivation`, cleanup correcto, bash syntax/diff-check correctos.
- El contenedor `fletflow-route-deactivation-mysql` y API local se detuvieron y eliminaron; MySQL persistente no se tocó.
- Backend commit `87b208e` pasó Action `45071475764`: xUnit 111/111, Release build, smoke HTTP/MySQL tenant, concurrencia y cleanup.
- Front `5d4e290` Action `45071113746` pasó: 36/36 E2E, responsive 1/1, backend 110/110, audit/lint/build y cleanup. Front `340eb55` pasó `45072015727` con 36/36 E2E, responsive 1/1, backend 111/111 y cleanup. Front `7f4841c` está publicado; Action `45072706025` ejecuta la cobertura de errores de carga.
- Continúa pendiente: política para otorgar Admin y requisito de desvincular User/Driver; seguir cobertura CRUD/responsive y pruebas reales. Móvil nativo pospuesto.

# El estado vigente se mantiene al inicio de este handoff; las notas posteriores son registro histórico.

## Estado vigente — política de Admin y edición de perfil validadas — 2026-11-23

- Ramas activas: Backend `chore/stabilize-fleet-platform` en `9e43bcd`; Front `refactor/route-builder` en `99ec46b`. Ambas limpias y sincronizadas con origin; `main` intacta.
- Usuario definió que sólo la cuenta raíz de la empresa puede crear/promover a Admin. La API ya deniega con 403 la creación y las dos rutas de promoción cuando la petición viene de Admin delegado.
- Action Backend [`45088129188`](https://github.com/Paul1578/Back_GPS/actions/runs/45088129188) pasó: Release build 0 warnings/0 errors, xUnit 113/113, smoke real HTTP/MySQL de aislamiento/roles/flota, concurrencia de asignaciones y cleanup. El smoke valida Admin delegado sin cambios SQL, promoción desde raíz y edición de perfil con User/Driver enlazados y persistencia sincronizada.
- Action anterior `45087881487` falló por el fixture de smoke de creación Admin que omitía el campo requerido Password y obtenía 400 antes de autorización. Se añadió contraseña de prueba; la repetición `45088129188` pasó. Causa y prevención registradas en `ERROR_LOG.md`.
- Siguientes pasos: revisar y alinear UX Front para no ofrecer creación/promoción Admin a Admin delegado; continuar la matriz de flujos CRUD, permisos y responsive de criterios alcanzables; actualizar estado con cada Action.
- Misión global continúa abierta. App nativa móvil quedó aplazada por el usuario; SMTP real y configuración productiva PFX/key ring requieren credenciales/infraestructura externas.

## Hallazgo y corrección — cerrar parada sin evidencia — 2026-11-22

- Defecto confirmado: HTTP permitía `Pending→Skipped` y `InProgress→Completed` sin evidencia; luego la ruta no podía cerrarse y tampoco se podía añadir evidencia.
- Backend `Route.UpdatePointStatus` ahora requiere evidencia antes de Completed/Skipped. Dos casos theory cubren rechazo y un caso permite Skip con nota. Release xUnit **110/110**.
- Smoke HTTP/MySQL 8 descartable probó HTTP 400 para Skip/Complete/cierre ruta sin evidencia; compara SQL (status, EndedAt, PointsJson, Vehicle.Status) intacto en cada intento; camino válido registra nota/completa ruta y cleanup cero. Salida exit 0.
- Front deshabilita Omitir sin evidencia; E2E Driver intenta ambos endpoints, verifica persistencia y sigue al cierre positivo. Lint/build/audit/discovery 36 pasan local; Front Action `45070512838` sigue pendiente de E2E al momento de esta nota.
- Cambios locales modificados en `fletflow/Domain/Fleet/Entities/Route.cs`, `BackEnd_Gps.Tests/RoutePointTests.cs`, `scripts/smoke-tenant-http.sh`; Front `DriverView.tsx` y `fleet-admin.spec.ts`. Documentar/correr Actions, corregir fallos y mantener ramas aisladas.

## Cobertura manual User/Driver — 2026-11-22

- Smoke crea User normal, valida `driverId=null`, lo promociona a Driver, crea luego el perfil con `userId` y comprueba GET Driver, `my-team` y columnas SQL/tenant.
- Pruebas negativas de PUT Vehicle/Driver y POST Driver exigen HTTP 400 y snapshot SQL idéntico/sin fila.
- MySQL/API 8 descartables: `SMOKE_COMPLETO_OK`; cleanup explícito cero User/Driver manual y cero fixtures restantes; script syntax/diff-check correctos. El Action previo pasó antes de esta ampliación.
- Commit backend `16c8b1a` publicado en la rama. Action `45069402482` pasó build Release, xUnit **107/107**, smoke tenant HTTP/MySQL, concurrencia y cleanup, incluido el fixture manual.
- El backend no soporta desvincular manteniendo User y Driver; pregunta funcional enviada. Segunda pregunta enviada: rol habilitado para otorgar Admin. Esperar respuestas y continuar cambios independientes.

# Continuación — validaciones negativas CRUD Driver/Vehicle — 2026-11-22

- Rama Backend `chore/stabilize-fleet-platform`; Front `refactor/route-builder`; `main` intacta.
- `scripts/smoke-tenant-http.sh` añade prueba HTTP + snapshot MySQL para PUT inválido Vehicle (año/placa), POST inválido Driver (requerido/documento/teléfono) y PUT inválido Driver (apellido/documento/teléfono).
- Ejecución completa con API y MySQL 8 efímeros terminó `SMOKE_COMPLETO_OK`, cleanup cero; `bash -n`/diff-check pasan. Primera ejecución con dotnet global falló por host incompleto, repetición con `/home/leonardo/.dotnet/dotnet` pasó; ver `ERROR_LOG.md`.
- Solo script modificado, sin código C#; revisar, registrar y publicar y validar Action. No tocar main.
- Continuar cobertura positiva/negativa CRUD, roles y responsive. Misión no completa; Admin delegado espera decisión, SMTP/PFX/infra productiva externos y móvil nativo pospuesto.

## Smoke de ciclo Vehicle y anti-enumeración — 2026-11-22

- Rama `chore/stabilize-fleet-platform`, Front relacionado `refactor/route-builder`; `main` intacta.
- Backend code `52d7e46` Action `45066082294` success: Release/xUnit 107/107, registro neutral, latencia mínima y cleanup. Docs `029011e` Action `45066471076` success (tenant HTTP/MySQL, concurrencia).
- Front `57e44c6` Action `45066249833` success: backend 107/107, lint/build/audit, responsive 1/1, E2E 33/33, cleanup.
- Smoke local amplía el ciclo Vehicle HTTP/MySQL: PUT exitoso, GET/SQL persistidos, Maintenance sin cambiar `IsActive`, baja y reactivación conservando estado. `bash -n`, diff-check y MySQL efímero pasan; Actions para esta ampliación pendientes.
- Primer start con `/usr/bin/dotnet` falló por instalación incompleta; el SDK local `/home/leonardo/.dotnet/dotnet` resolvió. Base persistente de puerto 3306 no fue usada.
- Reauditoría: falta smoke HTTP/MySQL de vínculo manual User↔Driver y decisión de política para Admin delegado. Seguir cobertura CRUD/roles/responsive; no cerrar misión.

# Continuación — Logout autenticado y smokes MySQL — 2026-11-22

- Branch backend `chore/stabilize-fleet-platform`, frontend `refactor/route-builder`; `main` intacta.
- Seguridad: `/Auth/logout` compara dueño del refresh con `NameIdentifier`; foreign token no se revoca y token propio sí. Regresión xUnit agregada.
- Validación local: Release build 0 warnings/errores, **99/99** xUnit, `smoke-tenant-http.sh` y `smoke-route-concurrency.sh` exit 0; ambos usaron MySQL 8 efímero y limpiaron recursos QA. Action [`45058536309`](https://github.com/Paul1578/Back_GPS/actions/runs/45058536309) pasó en `22c2fb6`.
- Front relacionado: **33/33** E2E API/MySQL, build/lint, audit 0 vulnerabilidades; manager constructor responsive y CRUD actualizado.
- Error de autorización de logout y errores de ejecución del E2E Front registrados en `docs/ERROR_LOG.md`.
- Próximo paso: continuar auditoría CRUD/roles/responsive y mantener los pendientes delimitados. Admin delegado requiere decisión de producto, SMTP/PFX/infra productiva externos, nativo móvil aplazado.

# Continuación histórica — Sesión y Driver con ruta Pending — 2026-11-22

- Rama Backend `chore/stabilize-fleet-platform`, Front `refactor/route-builder`; no main.
- Backend `27bf75a` pasó Action `45048795456`: build, xUnit 94/94, tenant HTTP/MySQL, route concurrency y cleanup cero.
- Lote backend local: cambio de rol toma row lock compartido con refresh; desasignación/baja/cambio de rol bloquean Driver con ruta Pending/InProgress. Suite Release 97/97, build 0 warnings/errors, bash syntax y diff-check. Smoke actualizado necesita Action.
- Front Action `45048803279`: 26/28; ambos fallos por viewport desktop y locator hamburger móvil. Fix de viewport y los E2E de Mi Ruta nuevos aún deben pasar lint/build/integrated Action.
- Seguir con auditorías CRUD/roles/responsive; no cerrar misión. SMTP/PFX externas; mobile aplazado; Admin delegado espera decisión.

# Continuación — Validación de asignación Driver/ruta — 2026-11-22

- Backend rama `chore/stabilize-fleet-platform`; Front `refactor/route-builder`; sin cambios en `main`.
- Backend `5c08dbc` pasó Action `45047639950`: 92/92 xUnit, smoke HTTP/MySQL y concurrencia, cleanup; docs `fcd7111` pasó `45047712270`.
- Fix backend local sin publicar: bloquea reasignar Driver mientras tenga ruta activa Pending/InProgress y valida coincidencia del vehículo al iniciar. Suite Release **94/94**; smoke HTTP/MySQL y CI del nuevo SHA pendientes.
- Front: E2E agregados para logout-all en dos sesiones y baja desde Equipo con revocación de refresh/access; logout-all ahora es visible sin expandir el formulario de contraseña. Fixture de baja independiente agregado al runner descartable. Lint y discovery pendientes de cierre; E2E integrado después de publicar ambos commits.
- Seguir con el smoke, build y E2E, registrar cualquier falla y solución, actualizar estado y ejecutar Actions en ambas ramas.
- Misión global sigue abierta. SMTP/PFX e infraestructura productiva externas; app nativa pospuesta.

# Continuación — Estado actual — 2026-11-22

- Backend branch `chore/stabilize-fleet-platform`; Front branch `refactor/route-builder`; `main` intacta.
- Backend `a207c08` pasó Actions `45047226328`: xUnit 91/91, Release build, smoke HTTP/MySQL tenant/flota + concurrencia y cleanup. El SDK aislado 8.0.428 está en `/tmp/fletflow-dotnet` porque el runtime global no está instalado.
- Front `d05dcf8` pasó Actions `45046312974`: E2E 26/26, responsive 1/1, audit/lint/build y cleanup. Front `38c58d6` lanzó Action integrada `45047502825`, contra backend a207, aún debe verificarse.
- Backend `5c08dbc` suprime emails de recuperación si el token ya no está activo; worker focal 3/3, full suite Release 92/92, build 0 warnings/errors local. Actions backend y Front integrada contra 5c08dbc pendientes.
- Próximo: revisar Action Front 45047502825; verificar backend CI 5c08dbc; después commit Front de documentación y disparar nueva integración. Reparar fallos de pruebas/cleanup antes de seguir.
- Races previamente corregidas: refresh vs revocación, consumo de reset token, GPS tras reasignación y cancelación Pending que preservaba Maintenance.
- Política de Admin delegado requiere decisión de producto; SMTP/PFX e infraestructura de producción externas; mobile nativo aplazado.

# Continuación — Backend y autorización operacional — 2026-11-22

- Rama `chore/stabilize-fleet-platform`. Cambio no publicado: arreglar Cancelled de ruta Pending que liberaba vehículo Maintenance; modifica `fletflow/Aplication/Fleet/Commands/ChangeRouteStatusCommand.cs`, test `BackEnd_Gps.Tests/CreateRouteCommandTests.cs` y smoke `scripts/smoke-tenant-http.sh`. Local: test focal 1/1, Release xUnit 89/89, `bash -n` y diff-check correctos. El run integrado posterior y cleanup aún deben confirmarse.
- El SDK de sistema `/usr/bin/dotnet` no encuentra runtime en `/usr/lib64/dotnet/host/fxr`; instalé .NET SDK 8.0.428 de forma aislada en `/tmp/fletflow-dotnet`. No cambiar setup global.
- Backend code previo `190f284` pasó Actions `45045070288` (xUnit 88/88 + smoke tenant/concurrency); docs `e24e371` pasó `45045329470`.
- Auditoría de solo lectura sugiere posibles carreras: refresh token vs cambio contraseña/baja; reset token reutilizado concurrentemente; registro GPS de Driver después de reasignación. Revisar estado de archivos/locks actuales antes de cambios: varios hallazgos históricos resultaron obsoletos porque ya estaban implementados.
- Antes se confirmó la validación post-lock de Driver para cambio de estado, el control same-tenant en `smoke-tenant-http.sh`, tests worker outbox/rotación de certificado y sincronización real del rate limit. Verificar HEAD antes de actuar.
- Actualizar documentación tras cada lote. Pendiente de producto: política de Admin delegado. SMTP real/PFX e infraestructura productiva dependen de configuración externa; mobile nativo aplazado.

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

## Registro neutral — validación de ramas 2026-11-22

- Backend `52d7e46` en `chore/stabilize-fleet-platform`: Action `45066082294` success; build Release, xUnit 107/107, smoke HTTP/MySQL con alta nueva y duplicada, respuestas neutrales iguales, piso >=450 ms y cleanup.
- Front `57e44c6` en `refactor/route-builder`: Action integrada `45066249833` aún corría al último sondeo. Consultar el resultado antes de actualizar el cierre del frontend.
- Documentación backend de este lote actualizada en CHANGELOG, ERROR_LOG, PROJECT_STATUS, SECURITY, TESTING y este handoff.
- Siguiente: esperar/verificar Action Front; registrar brechas pendientes de CRUD/roles/responsive y seguir implementando en las ramas existentes. No cerrar misión.

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

## Seguridad adicional implementada — pendiente CI — 2026-11-22

- Refresh: `ChangePasswordCommand`, `ResetPasswordCommand`, `ActivateAccountCommand`, `AuthService.ResetPasswordAsync` y `UpdateUserCommand` toman el lock de User antes de modificar password/estado y revocar. Se comparte con el lock de `TryRotateAsync`; Accounts con Driver siguen usando lock conjunto ruta/usuario.
- Reset token: repositorio tiene consumo compare-and-set por `Id`, activo y no expirado; flows vuelven a leer token bajo lock y abortan si otro ya lo usó. Test SQLite de 1er consumo/2do rechazo.
- GPS: `RegisterRoutePositionCommand` bloquea ruta/asignación y valida Driver solicitante después de recargar. `GET positions` mantiene lock y revalida hasta terminar la lectura. Test falso reasigna al adquirir lock, espera excepción, 0 posiciones y sin commit.
- Validación local posterior: xUnit Release 91/91, build 0 warnings/0 errores. Backend smoke último Action `45046324866` falló por fixture de ruta de mantenimiento compartiendo agenda; script corregido a Vehicle/Driver propios, con limpieza y conteo SQL. Próximo paso: commit/push de todos los cambios, inspeccionar Actions y reparar cualquier fallo; luego disparar Front integrado contra backend nuevo.- Backend Action `45048795456` pasó para `27bf75a`: build, xUnit **94/94**, smoke tenant HTTP/MySQL y concurrencia de rutas; cleanup cero fixtures. La integridad de Driver/ruta Pending queda validada.
- Backend Action `45050058627` pasó para `2045b18`: build Release, xUnit 97/97, tenant/MySQL y route concurrency; cleanup cero. El lock de cambio de rol y las protecciones de Driver Pending quedan validados.

## Continuación — cuenta Driver enlazada y password HTTP — 2026-11-22

- Rama chore/stabilize-fleet-platform; main intacta.
- CreateRoute y UpdateRoute validan la cuenta User enlazada después de locks de asignación y la bloquean owner-scoped en la misma transacción.
- smoke-tenant-http.sh prueba edición con User inactivo y activate/reset, consumo único, revocación refresh y nuevos logins. Verificar el comportamiento y cleanup.
- Validación: CreateRouteCommandTests 10/10, bash -n y diff-check. Falta suite Release, build, smokes HTTP/API MySQL y Actions.
- Error del patrón shell con backticks documentado en ERROR_LOG; no hubo cambios ni datos tocados.
- No cerrar misión. Después validar Front actualizado en CI integrada. Admin delegado sin respuesta; SMTP/PFX/despliegue externos.

## Reintento requerido — fixture HTTP de activate/reset

- Action backend 45060482906 validó build y xUnit 100/100, pero falló tenant smoke con suffix unbound variable. Ya se movió inicialización del sufijo al comienzo de scripts/smoke-tenant-http.sh.
- Bash syntax y diff-check se deben repetir. Publicar corrección y confirmar Actions: activate/reset, edición con Driver vinculado inactivo, concurrencia y cleanup.
- El Front 526c07b sigue en Actions 45060482928; verificar su resultado y si valida la rama backend actual.

## Backend smoke validado — 2026-11-22

- Action 45060714338 pasó en commit 14348f3: Release build, 100/100 pruebas, tenant HTTP/MySQL, rutas concurrentes y cleanup.
- La ampliación activate/reset y el rechazo de editar rutas para cuentas Driver desactivadas quedan verificadas en MySQL desechable.
- Front Action 45060482928 sigue ejecutando los 33 E2E autenticados; revisar resultado y corregir fallos antes de cerrar este lote.

## Continuación — aislamiento de fixtures Driver para Playwright — 2026-11-22

- El E2E frontend descubrió colisión de agenda al compartir Driver operativo entre escenarios paralelos. El runner ahora crea y exporta un Driver adicional específico para Mi Ruta/evidencia; extrae ambos IDs con Node y activa ambas cuentas.
- Fixtures de runner se limpian con el patrón existente `fletflow.e2e.driver.*` / documentos `E2EQA%`; confirmar cleanup en Action.
- `bash -n` y diff-check pasan. Backend Action `45060714338` validó el SHA anterior; ejecutar CI al publicar este cambio.

- Backend Action `45061524504` terminó success en `3f87af8`: build/tests Release, smoke HTTP/MySQL y concurrencia/cleanup. Front Action integrada `45061522878` sigue ejecutando E2E Playwright con ambos branches actualizados.

## Continuación — validación de Vehicle y colisiones únicas

- Backend commit `adcc286` publicado en `chore/stabilize-fleet-platform`: DataAnnotations y reglas comunes (Plate 20, Brand/Model 100, Description 1000, Year 1900–2100) en Create/Update Vehicle; mapeo condicional `DbUpdateException` a conflicto sólo al confirmar placa existente; pruebas Create/Update con colisión concurrente y límite inválido.
- Release local 103/103 y build 0 warnings/0 errors con `DOTNET_ROOT=/home/leonardo/.dotnet`. Action backend `45063399400` sigue en progreso e incluye HTTP/MySQL smoke para rangos y duplicados. El host `/usr/bin/dotnet` roto está documentado en `ERROR_LOG.md`.

## Continuación — límites de vehículo y carrera de placa

- Auditoría backend read-only detectó límites de Vehicle ausentes y colisión índice único no traducida. Create/Update request usan DataAnnotations; comandos normalizan/validan igual en aplicación (Plate 20, Brand/Model 100, Description 1000, Year 1900–2100). `DbUpdateException` sólo se traduce a conflicto si la reconsulta confirma placa de otro vehículo.
- Se extendió `VehicleCommandTests` para input inválido, create race y update race; smoke HTTP/MySQL verifica 400 y 409 con persistencia de la placa original intacta.
- Primera suite Release fue **102/103**: el fake de Update devolvía misma instancia mutable. Fake ajustado a copia no rastreada; suite **103/103**, build **0 warnings/0 errors**, bash -n/diff-check pasan con `/home/leonardo/.dotnet`.
- Backend Action `45063488394` confirma build, 103 tests y smoke Vehicle en MySQL efímero. Otro P3 sobre cambio de rol fue confirmado y corregido en el bloque siguiente; revisar pendientes de seguridad.

## Continuación — perfil Driver con cuenta desactivada

- Subagente confirmó el bug real y corrigió `ChangeUserRoleCommand`: cambiar a Driver ya no reactiva el perfil vinculado cuando `User.IsActive=false`. Test nuevo; suite Release local **104/104**.
- Smoke `scripts/smoke-tenant-http.sh` ampliado para ejecutar Logistics→Driver en API/MySQL real y asegurar `Driver.isActive=false`. Error/causa/prevención en `docs/ERROR_LOG.md`; suite 104/104, bash syntax y diff-check pasan. Commit y Actions nuevas pendientes.
- Action `45063802250` confirmó causa de test inválido: el cambio de rol se hizo con ruta pendiente (409 esperado). El ciclo se movió después de cancelar ruta; cleanup ahora elimina el Driver vinculado antes del vehículo para respetar FK. Action `45064477054` pasó el smoke y el cleanup.

## Continuación — registro y login neutrales

- Seguridad read-only encontró enumeración por mensajes/estados distintos en login. `LoginUserCommand` normaliza a 401/mensaje genérico para desconocido, contraseña incorrecta, cuenta inactiva y pendiente; ejecuta BCrypt con hash señuelo si no hay usuario. Se añadió xUnit y smoke HTTP que compara código/mensaje.
- Registro público ahora no emite tokens; email nuevo/duplicado recibe el mismo 200/payload. `RegisterForm` muestra aviso neutral y manda a Login; E2E compara respuestas y autentica cuenta nueva. Login también usa 401 neutral + hash BCrypt señuelo; smoke compara respuestas y bloquea cuentas inactivas/pendientes.
- `RegisterUserCommand` hashea antes de consultar existencia y controller espera piso 500 ms; el smoke mide >=450 ms para ambos resultados. Es mitigación temporal, no garantía si storage/red tienen carga variable.
- Backend Action `45065747053` pasó neutralización de registro (HTTP/MySQL), 107/107 y cleanup, aunque precede a la protección temporal añadida al final de esta fase. Nueva Action tras commit.
- Backend Action `45066082294` pasó la versión final del lote `52d7e46`, incluidos timing smoke >=450 ms, neutralidad del cuerpo, 107/107 y cleanup.
- Local: backend Release build 0 warnings/0 errors, xUnit **107/107**, bash syntax y diff-check; Front lint/build y discovery **33**. Código/docs todavía sin commit en esta fase; publicar ramas y confirmar Actions integradas. Revisar recovery bajo carga después.
- Backend Action `45064942603` validó login anti-enumeración en SHA `30bcb2f` (106 tests, HTTP/MySQL y cleanup); registro neutral es posterior y requiere nueva Action.

## 2026-11-23 — Ciclo operativo de recogidas y entregas

- Rama `chore/stabilize-fleet-platform`; `main` sin tocar.
- `scripts/smoke-tenant-http.sh` actualizado para cubrir origen + tres paradas (pickup, delivery y ambas) + destino, evidencia individual, rechazo por saltar secuencia/sin evidencia, persistencia de estados, GPS, fin de ruta y vehículo Available.
- Build Release local pasó sin warnings/errores; smoke completo pasó en API y MySQL 8 efímeros, cleanup correcto. `bash -n` y diff-check limpios. xUnit Release **113/113** pasó localmente; registrar evidencia en la Action al publicar.
- Primer intento no inició API porque `nohup` fue terminado junto con shell efímero; repetición en sesión persistente pasó. El `dotnet` del PATH también apunta a host incompleto; usar `/home/leonardo/.dotnet/dotnet`. Ambas incidencias están en ERROR_LOG.
- Próximo: finalizar suite xUnit, publicar smoke + docs, inspeccionar Action backend. Política de ruta sin operaciones aún espera respuesta.

- Action backend `45093565790` pasó para `e678e6c`: Release, 113/113, tenant HTTP/MySQL y concurrencia; cleanup exitoso. El log de Action confirma ejecución del `scripts/smoke-tenant-http.sh` actualizado y su resumen incluye cierre de recogida/entrega con evidencia/GPS; el ciclo ampliado quedó cubierto por CI.

- Backend Action `45093565790` success (`e678e6c`): build Release, 113/113, smoke tenant HTTP/MySQL + concurrencia, cleanup. Documentación y registro de hallazgos P2 pendientes de commit. El job CI sí ejecutó el smoke actualizado: el log de Action confirma `bash scripts/smoke-tenant-http.sh` y resultado exitoso con ciclo Logistics de recogida/entrega, evidencia/GPS y cleanup.
- La revisión independiente no detectó bypass nuevo en JWT/refresh/reset/owner scope; registra contraseña mínima 6 y rate limit general 60/min por IP/proceso como pendientes P2.

## 2026-11-23 — Política de traslado y seguridad de login implementadas localmente

- Usuario confirmó: permitir rutas sólo de traslado. Test `TransportRouteWithoutOperationalStops_CanStartAndComplete` verifica viaje sin operaciones/evidencias.
- Decisión de claves confirmada: 12 caracteres para toda clave nueva; claves actuales no se invalidan. Endpoint de cambio/reset/activación y validadores actualizados; Front registro/perfil/activate/reset muestra mismo requisito.
- Rate limit de login: 10 POST/min/IP y 429 + Retry-After encima del límite general; sin bloqueo de cuenta. En-memory por proceso, proxy forwarding requiere configuración segura para despliegue.
- Backend Release local **120/120**. Front lint/build pasan; targeted Historial 2/2 tras arreglar su fixture. Nuevo commit y Action integrados pendientes.

## Última Action: E2E pasan, cleanup reporta fallo

- Front `45102498423`: 43/43 Playwright y responsive pasaron; el job acabó fallido al limpiar fixtures MySQL.
- Diagnóstico: `scripts/run-e2e-local.sh` suprimía stderr de mysql. Backend tiene ahora un cambio que captura y muestra el error concreto al fallar, luego elimina el archivo temporal; siguiente Action deberá revelar/confirmar el cleanup.
- No ejecutar `cleanup-e2e.sql` en la DB local persistente. La repetición debe usar el MySQL descartable del workflow.

## Causa del cleanup hallada

- Front `45103134467` ejecutó 43/43 tests; cleanup produjo MySQL 1451 en `Routes.DriverId` al borrar Driver QA. El nombre de una ruta ligada no coincidía con prefijo `E2E QA ruta`.
- Backend `cleanup-e2e.sql` corregido para borrar RoutePositions/Routes asociados por Driver QA (`DocumentNumber E2EQA%`) antes de borrar Driver. El test siguiente debe verificar cleanup en MySQL efímero. No tocar la DB local persistente.


## Cleanup QA verificado

- Backend `7ea14a6` Action `45103732510`: success build/xUnit + smoke tenant/concurrency MySQL.
- Front `85da39c` Action `45103761122`: success, 43/43 E2E, responsive 1/1, cleanup MySQL.
- FK restrict resuelta con limpieza por `Driver.DocumentNumber E2EQA%`. Rama limpia, no tocar main.
