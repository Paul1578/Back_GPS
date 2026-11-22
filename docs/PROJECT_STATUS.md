# Estado del proyecto

Fecha de corte: 2026-11-21.

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

1. Ampliar la matriz HTTP/MySQL de roles y permisos. El nuevo smoke de CI ya cubre aislamiento cross-tenant de Vehicles, Drivers, Routes y RoutePositions, pero no sustituye todos los flujos de rol.
2. Configurar SMTP vigente; la credencial depende del usuario/entorno. Rotar también credenciales equivalentes en cualquier entorno remoto.
3. Antes de producción, configurar dominio/proxy/certificado, almacenamiento de evidencia y rate limiting distribuido; el límite en memoria actual es por IP. Migrar tokens de `localStorage` a cookies HttpOnly requiere cambio coordinado backend/frontend.
4. Ejecutar la integración de E2E frontend/API/MySQL en GitHub Actions después de publicar las ramas; ampliar casos de permisos/roles y responsive que aún no estén cubiertos.

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
